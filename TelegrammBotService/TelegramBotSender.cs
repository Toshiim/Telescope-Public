using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Polling;
using SqlStorage.DbServices;
using Microsoft.Extensions.Options;
using ShareLib.Settings;

namespace TelegramBotService
{
    public class TelegramBotSender
    {
        private readonly TelegramBotClient _botClient;
        private readonly CancellationTokenSource _cts;
        private readonly TelegramUserService _telegramUserService;
        private readonly TelegramFeedbackService _telegramFeedbackService;
        public TelegramBotSender(IOptions<AppSettings> options, TelegramUserService telegramUserService, TelegramFeedbackService telegramFeedbackService)
        {
            _botClient = new TelegramBotClient(options.Value.TelegramBot.BotToken);
            _cts = new CancellationTokenSource();
            _telegramUserService = telegramUserService ?? throw new ArgumentNullException(nameof(telegramUserService));
            _telegramFeedbackService = telegramFeedbackService ?? throw new ArgumentNullException(nameof(telegramFeedbackService));


            //  получение обновлений
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            // Запуск получения обновлений 
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );

            Console.WriteLine(" Телеграм бот запущен и готов получать обновления");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // TODO: Команды для: добавления пользователем в отслеживание сервисом канала

            if (update.Message != null)
            {
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
                        text: "Dude...",
                        cancellationToken: cancellationToken
                    );
                } 
            }

            if (update.CallbackQuery != null)
            {
                var callbackQuery = update.CallbackQuery;

                // Получаем данные из callback
                string callbackData = callbackQuery.Data;

                Console.WriteLine($" Получен callback: {callbackData} от пользователя {callbackQuery.From.Id}");

                // Парсим данные (формат: like_messageId или dislike_messageId)
                if (callbackData.StartsWith("like_") || callbackData.StartsWith("dislike_"))
                {
                    bool isLike = callbackData.StartsWith("like_");
                    string messageIdStr = callbackData.Substring(callbackData.IndexOf('_') + 1);

                    if (Guid.TryParse(messageIdStr, out Guid messageId))
                    {

                        await _telegramFeedbackService.SaveFeedbackAsync(callbackQuery.From.Id, messageId, isLike);
                        
                        // Отправляем подтверждение пользователю
                        await _botClient.AnswerCallbackQuery(
                            callbackQueryId: callbackQuery.Id,
                            text: isLike ? "👍 Вам понравилось это сообщение" : "👎 Вам не понравилось это сообщение",
                            cancellationToken: cancellationToken
                        );

                        // Обновляем текст сообщения, убирая кнопки 
                        //TODO: Мб не удалять кнопки. Написать обновление реакции. 
                        await _botClient.EditMessageReplyMarkup(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            replyMarkup: null,
                            cancellationToken: cancellationToken
                        );

                        // Добавляем текст с реакцией
                        await _botClient.EditMessageText(
                            chatId: callbackQuery.Message.Chat.Id,
                            messageId: callbackQuery.Message.MessageId,
                            text: $"{callbackQuery.Message.Text}\n\n{(isLike ? "👍 Вам понравилось это сообщение" : "👎 Вам не понравилось это сообщение")}",
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        Console.WriteLine($" Некорректный формат messageId: {messageIdStr}");
                    }
                }
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($" Ошибка обработки Telegram: {exception.Message}");
            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(long userId, string messageText)
        {
            try
            {
                await _botClient.SendMessage(
                    chatId: userId,
                    text: messageText
                );

                Console.WriteLine($" Сообщение отправлено пользователю {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ошибка при отправке сообщения: {ex.Message}");
            }
        }

        public async Task SendInteractiveMessageAsync(long userId, string messageText, Guid messageId, bool isRecommended)
        {
            var message = isRecommended? "Рекомендуемое сообщение: " + messageText
                : "Не рекомендованное сообщение: " + messageText;
            try
            {
                // Создаем инлайн-кнопки для обратной связи
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

                Console.WriteLine($" Сообщение с кнопками отправлено пользователю {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ошибка при отправке сообщения с кнопками: {ex.Message}");
            }
        }
    }
}