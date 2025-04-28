using TdLib;
using Microsoft.Extensions.Hosting;
using ShareLib.Entities;
using ShareLib.Settings;
using Microsoft.Extensions.Options;

namespace Telescope
{
    public class PublicationParser : BackgroundService
    {
        private static TdClient client = null!;
        private static readonly List<string> channelUsernames = new()
        {
            "dtfbest",  "TelescopeTest", "qwerty_live", "bankrollo" 
        };

        private static readonly Dictionary<long, string> channelIds = new(); // ID каналов
        private RabbitMQProducer producer = null!;
        private readonly int ApiID;
        private readonly string ApiHash;

        public PublicationParser(IOptions<AppSettings> options, RabbitMQProducer rabbitMQProducer)
        {
            ApiID = options.Value.TelegramUserApi.ApiId;
            ApiHash = options.Value.TelegramUserApi.ApiHash;
            producer = rabbitMQProducer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            client = new TdClient();

            client.UpdateReceived += async (sender, update) => await OnUpdateReceived(update, producer);

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

            await Authorize();
            await FindChannelsIds();

            if (channelIds.Count == 0)
            {
                Console.WriteLine(" Ни один канал не найден.");
                return;
            }

            Console.WriteLine(" Отслеживаем каналы:");
            foreach (var (id, name) in channelIds)
            {
                Console.WriteLine($"  - {name} (ID: {id})");
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private static async Task FindChannelsIds()
        {
            foreach (var username in channelUsernames)
            {
                try
                {
                    var chat = await client.ExecuteAsync(new TdApi.SearchPublicChat { Username = username });
                    if (chat != null && chat.Id < 0) // ID каналов всегда отрицательные
                    {
                        channelIds[chat.Id] = username;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка поиска {username}: {ex.Message}");
                }
            }
        }

        private static async Task OnUpdateReceived(TdApi.Update update, RabbitMQProducer producer)
        {
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
                            ForAlbum = false // TODO : Рассмотреть true и пересылку и запись всего альбома 
                        });

                        if (link != null && !string.IsNullOrEmpty(link.Link)) 
                        {
                            msgToSend.PublicUrl = link.Link; 
                            Console.WriteLine($" Получена ссылка для сообщения из {channelName}: {link.Link}");
                        }
                        else
                        {
                            Console.WriteLine($" Не удалось получить ссылку для сообщения {message.Id} из канала {channelName}.");
                            return; // Т.к. ссылка критична, выходим из метода
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" Ошибка при получении ссылки для сообщения {message.Id} из {channelName}: {ex.Message}");
                    }

                    try
                    {
                        await producer.SendMessageAsync(msgToSend);
                        Console.WriteLine($" Сообщение из канала {channelName} (ID: {message.Id}) успешно отправлено в очередь.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" Ошибка при отправке сообщения из канала {channelName} в RabbitMQ: {ex.Message}");
                    }
                }
            }
        }


        private static TelegramMessage GetMessageText(TdApi.Message message, string channelName)
        {
            var tm = new TelegramMessage
            {
                Id = Guid.NewGuid(), // Внутренний ID
                TelegramMessageId = message.Id, // TDLib ID
                ChannelId = message.ChatId,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Date).UtcDateTime,
                ChannelName = channelName ?? "Unknown", //  переданное имя
                // PublicUrl будет установлен позже в OnUpdateReceived
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


                // TODO: Добавить обработку других типов сообщений
  
                default:
                    tm.MessageText = $"[{message.Content.GetType().Name}]"; // Указываем тип контента, если текст не извлечь
                    break;
            }

            return tm;
        }

        private static async Task Authorize()
        {
            while (true)
            {
                TdApi.AuthorizationState? state = await client.ExecuteAsync(new TdApi.GetAuthorizationState());

                switch (state)
                {
                    case TdApi.AuthorizationState.AuthorizationStateReady:
                        Console.WriteLine(" Авторизация успешна!");
                        return;

                    case TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber:
                        Console.Write("Введите номер телефона: ");
                        string phoneNumber = "";
                        await client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber { PhoneNumber = phoneNumber });
                        break;

                    case TdApi.AuthorizationState.AuthorizationStateWaitCode:
                        Console.Write("Введите код из Telegram: ");
                        string code = ""; 
                        await client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code });
                        break;

                    default:
                        Console.WriteLine($"⛔️ Неизвестное состояние: {state}");
                        return;
                }
            }
        }
    }
}

