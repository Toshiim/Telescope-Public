using Embedding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QdrantService;
using ShareLib.Settings;
using SqlStorage.DbServices;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telescope;

namespace TelegramBotService
{
    public class TelegramBotSender : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly TelegramBotClient _botClient;
        private readonly CancellationTokenSource _cts;
        private DBUserService _telegramUserService;
        private DBFeedbackService _telegramFeedbackService;
        private DBChannelService _telegramChannelService;
        private readonly ILogger<TelegramBotSender> _logger;
        private readonly OllamaEmbedding _ollamaEmbedding;
        private QdrantRepository _qdrantRepository;
        private readonly PublicationParser _publicationParser;
        private static readonly Dictionary<long, TaskCompletionSource<string>> _pendingConfirmations = new();

        public TelegramBotSender(
            IOptions<AppSettings> options,
            ILogger<TelegramBotSender> logger,
            OllamaEmbedding ollamaEmbedding,
            PublicationParser publicationParser,
            IServiceScopeFactory scopeFactory
            )
        {
            _botClient = new TelegramBotClient(options.Value.TelegramBot.BotToken);
            _cts = new CancellationTokenSource();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ollamaEmbedding = ollamaEmbedding ?? throw new ArgumentNullException(nameof(ollamaEmbedding));
            _publicationParser = publicationParser ?? throw new ArgumentNullException(nameof(publicationParser));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("Телеграм бот запущен и готов получать обновления");

            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var infoText = """
                 Этот бот рекомендует тебе посты из Telegram-каналов на основе твоих реакций.

                 Доступные команды:
                /start — Запуск бота.
                /info — Информация о боте и командах.
                /addchannel <ссылка или @ник> — Подписаться на канал. 
                /rag <текст запроса> — Семантический поиск текста по базе знаний. 
                /mychannels — Показать ваши добавленные каналы.
                /unsubscribe <название канала> — Отписаться от канала.

                 Ставь лайк/дизлайк на сообщения — система будет адоптироваться под твои предпочтения.
                """;

            using var scope = _scopeFactory.CreateScope();
            _telegramFeedbackService = scope.ServiceProvider.GetRequiredService<DBFeedbackService>();
            _telegramUserService = scope.ServiceProvider.GetRequiredService<DBUserService>();
            _telegramChannelService = scope.ServiceProvider.GetRequiredService<DBChannelService>();
            _qdrantRepository = scope.ServiceProvider.GetRequiredService<QdrantRepository>();

            _logger.LogInformation("Получено обновление: {UpdateType}", update.Type);
            if (update.Message != null)
            {
                if (_pendingConfirmations.TryGetValue(update.Message.From.Id, out var pendingTcs))
                {
                    pendingTcs.TrySetResult(update.Message.Text ?? "");
                    return;
                }

                //TODO: Вынести в Handler'ы код. 
                var message = update.Message;
                if (message.Text == "/start")
                {
                    await _telegramUserService.CreateUserAsync(
                        telegramUserId: message.From.Id,
                        username: message.From.Username,
                        firstName: message.From.FirstName,
                        lastName: message.From.LastName
                    );

                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Started" + infoText,
                        cancellationToken: cancellationToken
                    );
                }
                else if (message.Text.StartsWith("/rag "))
                {
                    var query = message.Text.Substring(5).Trim();

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Введите текст запроса после команды /rag");
                        return;
                    }

                    var links = await _qdrantRepository.SearchAsync(await _ollamaEmbedding.GenerateEmbedding(query));

                    if (links.Count == 0)
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Ничего не найдено 🕵️‍♂️");
                        return;
                    }

                    foreach (var link in links)
                    {
                        await _botClient.SendMessage(message.Chat.Id, link);
                    }
                }
                else if (message.Text.StartsWith("/addchannel "))
                {
                    var query = message.Text.Substring(12).Trim();

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Введите название канала или ссылку на него после /addchannel");
                        return;
                    }

                    var username = NormalizeChannelUsername(query);

                    if (string.IsNullOrEmpty(username))
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Не удалось распознать никнейм канала. Проверьте формат.");
                        return;
                    }

                    await _publicationParser.AddChanelToUser(username, message.From.Id);
                }
                else if (message.Text == "/info")
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: infoText,
                        cancellationToken: cancellationToken
                    );
                }
                else if (message.Text == "/mychannels")
                {
                    var channels = await _telegramChannelService.GetChannelsByUserAsync(message.From.Id);

                    if (channels.Count == 0)
                    {
                        await _botClient.SendMessage(
                            message.Chat.Id,
                            "Вы ещё не добавили ни одного канала.\nИспользуйте /addchannel <ссылка или @ник>",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    var response = "📺 Ваши каналы:\n";
                    foreach (var channel in channels)
                    {
                        response += $"• {channel.ChannelName} (ID: {channel.ChannelId})\n";
                    }

                    await _botClient.SendMessage(
                        message.Chat.Id,
                        response,
                        cancellationToken: cancellationToken
                    );
                }
                else if (message.Text.StartsWith("/unsubscribe "))
                {
                    var input = message.Text.Substring(13).Trim();

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Укажите канал для отписки: /unsubscribe <название>");
                        return;
                    }

                    var normalized = NormalizeChannelUsername(input);

                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Не удалось распознать название канала.");
                        return;
                    }

                    var success = await _telegramChannelService.UnsubscribeUserFromChannelAsync(message.From.Id, normalized);

                    if (success)
                        await _botClient.SendMessage(message.Chat.Id, $"Вы успешно отписались от канала: {normalized}");
                    else
                        await _botClient.SendMessage(message.Chat.Id, $"Вы не были подписаны на канал: {normalized}");
                }
                else if (message.Text == "/delete")
                {
                    var tcs = new TaskCompletionSource<string>();
                    _pendingConfirmations[message.From.Id] = tcs;

                    await _botClient.SendMessage(message.Chat.Id,
                        "Вы уверены, что хотите удалить все свои данные? Напишите 'ДА' в течение 30 секунд.");

                    var delayTask = Task.Delay(TimeSpan.FromSeconds(30));
                    var completedTask = await Task.WhenAny(tcs.Task, delayTask);

                    _pendingConfirmations.Remove(message.From.Id);

                    if (completedTask == tcs.Task && tcs.Task.Result.Trim().ToUpper() == "ДА")
                    {
                        var result = await _telegramUserService.DeleteUserAsync(message.From.Id);
                        await _botClient.SendMessage(message.Chat.Id, result
                            ? "Ваши данные были удалены."
                            : "Вы не были зарегистрированы.");
                    }
                    else
                    {
                        await _botClient.SendMessage(message.Chat.Id, "Удаление отменено.");
                    }
                }


            }
            if (update.CallbackQuery != null)
            {
                var callbackQuery = update.CallbackQuery;
                string callbackData = callbackQuery.Data;

                _logger.LogInformation("Получен callback: {CallbackData} от пользователя {UserId}", callbackData, callbackQuery.From.Id);

                if (callbackData.StartsWith("like_") || callbackData.StartsWith("dislike_"))
                {
                    bool isLike = callbackData.StartsWith("like_");
                    string messageIdStr = callbackData.Substring(callbackData.IndexOf('_') + 1);

                    if (Guid.TryParse(messageIdStr, out Guid messageId))
                    {
                        await _telegramFeedbackService.SaveFeedbackAsync(callbackQuery.From.Id, messageId, isLike);

                        await _botClient.AnswerCallbackQuery(
                            callbackQueryId: callbackQuery.Id,
                            text: isLike ? "👍 Вам понравилось это сообщение" : "👎 Вам не понравилось это сообщение",
                            cancellationToken: cancellationToken
                        );

                        //TODO: Мб не удалять кнопки. Написать обновление реакции. 
                        await _botClient.EditMessageReplyMarkup(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            replyMarkup: null,
                            cancellationToken: cancellationToken
                        );

                        await _botClient.EditMessageText(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            text: $"{callbackQuery.Message.Text}\n\n{(isLike ? "👍 Вам понравилось это сообщение" : "👎 Вам не понравилось это сообщение")}",
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        _logger.LogWarning("Некорректный формат messageId: {MessageIdStr}", messageIdStr);
                    }
                }
            }
        }
        private string NormalizeChannelUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            input = input.Trim();
            if (input.StartsWith("@"))
                input = input.Substring(1);

            if (input.StartsWith("https://t.me/"))
                input = input.Substring("https://t.me/".Length);
            else if (input.StartsWith("t.me/"))
                input = input.Substring("t.me/".Length);

            input = input.TrimEnd('/');

            return input;
        }

        /// <summary>
        /// Обработчик ошибок при получении обновлений от Telegram
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="exception"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Ошибка обработки Telegram");
            return Task.CompletedTask;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="messageURL"></param>
        /// <param name="messageId"></param>
        /// <param name="isRecommended"></param>
        /// <returns></returns>
        public async Task SendInteractiveMessageAsync(long userId, string messageURL, Guid messageId, bool isRecommended, double? score, double threshold)
        {
            var message = isRecommended ? "Рекомендуемое сообщение: " + messageURL : "Не рекомендованное сообщение: " + messageURL;
            message += score == 0 && threshold == 0 ? $" Автоматически рекомендованное сообщение" : $"\n\nОценка: {score:F2}, Порог: {threshold:F2}";
            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("👍 Нравится", $"like_{messageId}"),
                        InlineKeyboardButton.WithCallbackData("👎 Не нравится", $"dislike_{messageId}")
                    }
                });

                await _botClient.SendMessage(
                    chatId: userId,
                    text: message,
                    replyMarkup: inlineKeyboard
                );

                _logger.LogInformation("Сообщение отправлено пользователю {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения с кнопками пользователю {UserId}", userId);
            }
        }
    }
}