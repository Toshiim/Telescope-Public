using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareLib.Entities;
using ShareLib.Settings;
using SqlStorage.DbServices;
using TdLib;
using System.Linq;


namespace Telescope
{
    public class PublicationParser : BackgroundService, IPublicationParser
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TdClient client = new TdClient();
        private static readonly List<string> channelUsernames = new() { };
        private RabbitMQProducer _producer = null!;
        private readonly Dictionary<long, string> channelIds = new();
        private readonly int ApiID;
        private readonly string ApiHash;
        private readonly ILogger<PublicationParser> _logger;
        private DBChannelService _dbChannelService = null!;

        public PublicationParser(IOptions<AppSettings> options, ILogger<PublicationParser> logger, IServiceScopeFactory scopeFactory)
        {
            ApiID = options.Value.TelegramUserApi.ApiId;
            ApiHash = options.Value.TelegramUserApi.ApiHash;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            client.UpdateReceived += async (sender, update) => await OnUpdateReceived(update);

            // Устанавливаем параметры TDLib
            await client.ExecuteAsync(new TdApi.SetTdlibParameters
            {
                DatabaseDirectory = "tdlib",
                UseMessageDatabase = true,
                UseFileDatabase = false,
                UseChatInfoDatabase = false,
                UseSecretChats = false,
                ApiId = ApiID,
                ApiHash = ApiHash,
                SystemLanguageCode = "en",
                DeviceModel = "PC",
                ApplicationVersion = "1.0"
            });

            await client.ExecuteAsync(new TdApi.SetLogVerbosityLevel { NewVerbosityLevel = 0 });

            bool shouldContinue = await CheckExistingAuthorizationAndPromptAsync(stoppingToken);
            if (!shouldContinue)
            {
                _logger.LogInformation("Остановка PublicationParser по выбору пользователя.");
                return;
            }

            TdApi.AuthorizationState? state = await client.ExecuteAsync(new TdApi.GetAuthorizationState());
            if (!(state is TdApi.AuthorizationState.AuthorizationStateReady))
            {
                await Authorize();
            }

            await LoadChannelsFromDatabaseAsync();
            await FindChannelsIds();

            if (channelIds.Count == 0)
            {
                _logger.LogWarning(" Ни один канал не найден.");
                return;
            }

            _logger.LogInformation(" Отслеживаем каналы:");
            foreach (var (id, name) in channelIds)
            {
                _logger.LogInformation("  - {ChannelName} (ID: {ChannelId})", name, id);
            }

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("PublicationParser остановлен.");
            }
        }

        /// <summary>
        /// Проверяет текущий статус авторизации. Если авторизация уже готова, показывает информацию о текущем аккаунте
        /// и ждёт решения пользователя: продолжить, logout или exit.
        /// Возвращает true — продолжать запуск сервиса (включая возможную повторную авторизацию),
        /// false — завершить ExecuteAsync (остановить сервис).
        /// </summary>
        private async Task<bool> CheckExistingAuthorizationAndPromptAsync(CancellationToken stoppingToken)
        {
            try
            {
                var state = await client.ExecuteAsync(new TdApi.GetAuthorizationState());

                if (state is TdApi.AuthorizationState.AuthorizationStateReady)
                {
                    // Получаем информацию о текущем юзере
                    TdApi.User? me = await client.ExecuteAsync(new TdApi.GetMe());

                    if (me == null)
                    {
                        _logger.LogWarning("GetMe вернул null.");
                        return true; // продолжим процедуру авторизации как обычно
                    }

                    // Формируем отображаемое имя
                    string displayName = $"{me.FirstName ?? ""} {me.LastName ?? ""}".Trim();
                    if (string.IsNullOrEmpty(displayName))
                        displayName = "(no name)";

                    // Username может быть в Usernames.EditableUsername или в ActiveUsernames[0]
                    string username = "(no username)";
                    if (me.Usernames != null)
                    {
                        if (!string.IsNullOrEmpty(me.Usernames.EditableUsername))
                            username = me.Usernames.EditableUsername;
                        else if (me.Usernames.ActiveUsernames != null && me.Usernames.ActiveUsernames.Length > 0)
                            username = me.Usernames.ActiveUsernames.FirstOrDefault() ?? "(no username)";
                    }

                    string phone = string.IsNullOrEmpty(me.PhoneNumber) ? "(no phone)" : me.PhoneNumber;
                    long id = me.Id;

                    _logger.LogInformation("Текущая авторизация: {Display} (id: {Id}, username: {Username}, phone: {Phone})",
                        displayName, id, username, phone);

                    Console.WriteLine("--------------------------------------------------");
                    Console.WriteLine($"Текущий аккаунт: {displayName}");
                    Console.WriteLine($"Id: {id}");
                    Console.WriteLine($"Username: {username}");
                    Console.WriteLine($"Phone: {phone}");
                    Console.WriteLine("--------------------------------------------------");
                    Console.WriteLine("Нажмите Enter чтобы продолжить под этим аккаунтом, введите 'logout' чтобы разлогиниться, или 'exit' чтобы остановить приложение.");
                    Console.Write("> ");

                    string? input = null;
                    try
                    {
                        input = await Task.Run(() => Console.ReadLine(), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Ожидание ввода было прервано токеном отмены.");
                        return false;
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Отмена через токен остановки.");
                        return false;
                    }

                    input = input?.Trim() ?? "";

                    if (input.Equals("logout", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Пользователь выбрал logout — выполняем TdApi.LogOut().");
                        try
                        {
                            await client.ExecuteAsync(new TdApi.LogOut());
                            _logger.LogInformation("Запрошен logout. Ожидаем смены состояния авторизации...");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка при вызове LogOut.");
                        }

                        for (int i = 0; i < 40; i++) 
                        {
                            var s = await client.ExecuteAsync(new TdApi.GetAuthorizationState());
                            if (!(s is TdApi.AuthorizationState.AuthorizationStateReady))
                                break;
                            await Task.Delay(500, stoppingToken);
                        }

                        return true;
                    }
                    else if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Пользователь выбрал exit. Прекращаем работу.");
                        return false;
                    }
                    else
                    {
                        _logger.LogInformation("Пользователь выбрал продолжить под текущим аккаунтом.");
                        return true;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке/выводе текущего состояния авторизации.");
                return true;
            }
        }

        public async Task AddChanelToUser(string channelName, long userID)
        {
            using var scope = _scopeFactory.CreateScope();
            _dbChannelService = scope.ServiceProvider.GetRequiredService<DBChannelService>();

            var channelID = await FindChannelsIds(channelName);

            if (channelID != 0)
            {
                try
                {
                    await _dbChannelService.AddOrGetChannelAsync(channelID, channelName);

                    bool subscribed = await _dbChannelService.SubscribeUserToChannelAsync(userID, channelID);
                    if (subscribed)
                    {
                        _logger.LogInformation("Пользователь {UserId} успешно подписан на канал {ChannelName} (ID: {ChannelId})", userID, channelName, channelID);
                    }
                    else
                    {
                        _logger.LogInformation("Пользователь {UserId} уже подписан на канал {ChannelName} (ID: {ChannelId})", userID, channelName, channelID);
                    }

                    await JoinChannelIfNeeded(channelID);

                    if (!channelIds.ContainsKey(channelID))
                    {
                        channelIds[channelID] = channelName;
                        _logger.LogInformation("Канал {ChannelName} добавлен в кэш (ID: {ChannelId})", channelName, channelID);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при попытке подписки на канал {ChannelName} (ID: {ChannelId})", channelName, channelID);
                }
            }
        }

        private async Task LoadChannelsFromDatabaseAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            _dbChannelService = scope.ServiceProvider.GetRequiredService<DBChannelService>();

            var allChannels = await _dbChannelService.GetAllChannelsAsync();

            foreach (var channel in allChannels)
            {
                if (!channelIds.ContainsKey(channel.ChannelId))
                {
                    channelIds[channel.ChannelId] = channel.ChannelName;
                    _logger.LogInformation("Канал из БД: {ChannelName} (ID: {ChannelId})", channel.ChannelName, channel.ChannelId);
                }
            }
        }

        private async Task FindChannelsIds()
        {
            foreach (var username in channelUsernames)
            {
                try
                {
                    var chat = await client.ExecuteAsync(new TdApi.SearchPublicChat { Username = username });
                    if (chat != null && chat.Id < 0) // ID каналов всегда отрицательные
                    {
                        channelIds[chat.Id] = username;
                        _logger.LogInformation("Найден канал {Username} с ID {ChatId}", username, chat.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка поиска канала {Username}", username);
                }
            }
        }
        private async Task<long> FindChannelsIds(string channelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(channelName))
                {
                    _logger.LogWarning("Пропущен пустой username в списке каналов.");
                    return 0;
                }

                _logger.LogInformation("Поиск канала по username: {Username}", channelName);
                var chat = await client.ExecuteAsync(new TdApi.SearchPublicChat { Username = channelName });
                if (chat != null && chat.Id < 0) // ID каналов всегда отрицательные
                {
                    _logger.LogInformation("Найден канал {Username} с ID {ChatId}", channelName, chat.Id);
                    return chat.Id;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка поиска канала {Username}", channelName);
                return 0;
            }
        }

        private async Task<bool> JoinChannelIfNeeded(long chatId)
        {
            try
            {
                await client.ExecuteAsync(new TdApi.JoinChat { ChatId = chatId });
                _logger.LogInformation("Попытка подписки на канал {ChatId} завершена", chatId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подписке на канал {ChatId}", chatId);
                return false;
            }
        }

        private async Task OnUpdateReceived(TdApi.Update update)
        {
            using var scope = _scopeFactory.CreateScope();
            _producer = scope.ServiceProvider.GetRequiredService<RabbitMQProducer>();

            if (update is TdApi.Update.UpdateNewMessage newMessage)
            {
                var message = newMessage.Message;

                if (channelIds.TryGetValue(message.ChatId, out string? channelName))
                {
                    TelegramMessage msgToSend = GetMessageText(message, channelName);

                    try
                    {
                        var link = await client.ExecuteAsync(new TdApi.GetMessageLink
                        {
                            ChatId = message.ChatId,
                            MessageId = message.Id,
                            ForAlbum = true // TODO : Рассмотреть true и пересылку и запись всего альбома 
                        });

                        if (link != null && !string.IsNullOrEmpty(link.Link))
                        {
                            msgToSend.PublicUrl = link.Link;
                            _logger.LogInformation("Получена ссылка для сообщения из {ChannelName}: {Link}", channelName, link.Link);
                        }
                        else
                        {
                            _logger.LogWarning("Не удалось получить ссылку для сообщения {MessageId} из канала {ChannelName}.", message.Id, channelName);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при получении ссылки для сообщения {MessageId} из {ChannelName}", message.Id, channelName);
                        return;
                    }

                    try
                    {
                        await _producer.SendMessageAsync(msgToSend);
                        _logger.LogInformation("Сообщение из канала {ChannelName} (ID: {MessageId}) успешно отправлено в очередь.", channelName, message.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при отправке сообщения из канала {ChannelName} в RabbitMQ", channelName);
                    }
                }
            }
        }

        private static TelegramMessage GetMessageText(TdApi.Message message, string channelName)
        {
            var tm = new TelegramMessage
            {
                Id = Guid.NewGuid(),
                TelegramMessageId = message.Id,
                ChannelId = message.ChatId,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Date).UtcDateTime,
                ChannelName = channelName ?? "Unknown"
            };

            switch (message.Content)
            {
                case TdApi.MessageContent.MessageText textContent:
                    tm.MessageText = textContent.Text?.Text ?? "";
                    break;

                case TdApi.MessageContent.MessagePhoto photoContent:
                    tm.MessageText = photoContent.Caption?.Text ?? "";
                    tm.HasPhoto = true;
                    break;

                case TdApi.MessageContent.MessageVideo videoContent:
                    tm.MessageText = videoContent.Caption?.Text ?? "";
                    tm.HasVideo = true;
                    break;

                default:
                    tm.MessageText = $"[{message.Content.GetType().Name}]";
                    break;
            }

            return tm;
        }

        private async Task Authorize()
        {
            while (true)
            {
                TdApi.AuthorizationState? state = await client.ExecuteAsync(new TdApi.GetAuthorizationState());

                switch (state)
                {
                    case TdApi.AuthorizationState.AuthorizationStateReady:
                        _logger.LogInformation("Авторизация успешна!");
                        return;

                    case TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber:
                        _logger.LogInformation("Ожидается ввод номера телефона.");
                        Console.Write("Введите номер телефона: ");
                        string phoneNumber = Console.ReadLine() ?? "";
                        await client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber { PhoneNumber = phoneNumber });
                        break;

                    case TdApi.AuthorizationState.AuthorizationStateWaitCode:
                        _logger.LogInformation("Ожидается ввод кода из Telegram.");
                        Console.Write("Введите код из Telegram: ");
                        string code = Console.ReadLine() ?? "";
                        await client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code });
                        break;

                    case TdApi.AuthorizationState.AuthorizationStateWaitPassword:
                        _logger.LogInformation("Ожидается ввод двухфакторного пароля.");
                        Console.Write("Введите пароль: ");
                        string pwd = Console.ReadLine() ?? "";
                        await client.ExecuteAsync(new TdApi.CheckAuthenticationPassword { Password = pwd });
                        break;

                    default:
                        _logger.LogWarning("⛔️ Неизвестное состояние авторизации: {State}", state);
                        return;
                }
            }
        }
    }
}
