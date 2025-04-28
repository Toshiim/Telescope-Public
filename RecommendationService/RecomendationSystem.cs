using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using TelegramBotService;
using Microsoft.Extensions.Options;
using ShareLib.Settings;
using SqlStorage.DbServices;

namespace RecommendationService
{
    public class RecommendationSystem
    {
        private readonly QdrantClient _qdrantClient;
        private readonly string _collectionName;
        private readonly TelegramBotSender _telegramBotService;
        private readonly TelegramUserService _telegramUserService;
        private readonly TelegramMessageService _telegramMessageService;
        private readonly TelegramFeedbackService _telegramFeedbackService;
        private readonly TelegramRecommendationService _telegramRecommendationService;


        public RecommendationSystem(TelegramBotSender telegramBotSender,TelegramRecommendationService telegramRecommendationService,
            TelegramUserService telegramUserService, TelegramMessageService telegramMessageService, TelegramFeedbackService telegramFeedbackService, IOptions<AppSettings> options)
        {
            _qdrantClient = new QdrantClient(new Uri(options.Value.Qdrant.ConnectionString));
            _telegramBotService = telegramBotSender ?? throw new ArgumentNullException(nameof(telegramBotSender));
            _telegramUserService = telegramUserService ?? throw new ArgumentNullException(nameof(telegramBotSender));
            _telegramMessageService = telegramMessageService ?? throw new ArgumentNullException(nameof(telegramMessageService));
            _telegramFeedbackService = telegramFeedbackService ?? throw new ArgumentNullException(nameof(telegramFeedbackService));
            _telegramRecommendationService = telegramRecommendationService ?? throw new ArgumentNullException(nameof(telegramRecommendationService));
            _collectionName = options.Value.Qdrant.CollectionName;
        }

        public async Task ProcessRecommendationAsync(Guid MessageID)
        {
            Console.WriteLine($" Поиск записи с ID: {MessageID} в коллекции '{_collectionName}'");
            var userIds = await _telegramUserService.GetAllUserIdsAsync();
            var messageLink = await _telegramMessageService.GetMessagePublicUrlAsync(MessageID);
            // TODO : индивидуальные каналы и рекомендации, для PoC и так пойдёт.

            foreach (var userId in userIds)
            {
                var LikedMessages = await _telegramFeedbackService.GetUserLikedMessagesAsync(userId);
                var DislikedMessages = await _telegramFeedbackService.GetUserDislikedMessagesAsync(userId);
                var feedbacks = LikedMessages.Count + DislikedMessages.Count;

                if (feedbacks < 5 && LikedMessages.Count == 0)
                {
                    Console.WriteLine($" Недостаточно отзывов для пользователя {userId}. Пропускаем сообщение {MessageID}.");
                    await _telegramBotService.SendInteractiveMessageAsync(userId, messageLink, MessageID, true);
                    await _telegramRecommendationService.SaveRecommendationAsync(userId, MessageID);
                }
                else
                {
                    Console.WriteLine($" Поиск рекомендаций для пользователя {userId} на основе отзывов.");
                    try
                    {
                        var searchResultBest = await _qdrantClient.QueryAsync(collectionName: _collectionName,
                                query: new RecommendInput
                                {
                                    Positive = { LikedMessages.Select(id => new VectorInput { Id = id }) },
                                    Negative = { DislikedMessages.Select(id => new VectorInput { Id = id }) },
                                    Strategy = RecommendStrategy.BestScore
                                },
                                filter: HasId(MessageID),
                                limit: 1
                            );

                        Console.WriteLine($" Рекомендации для пользователя {userId} на основе отзывов:");
                        var recommendedMessage = searchResultBest.FirstOrDefault();
                        Console.WriteLine($"  ID: {MessageID}, Score: {recommendedMessage.Score}");

                        // TODO: Проработать коэффициент.
                        if (recommendedMessage.Score > 0.2) // Порог рекомендаций 
                        {
                            Console.WriteLine($" Рекомендовано сообщение {MessageID} пользователю {userId}");
                            await _telegramBotService.SendInteractiveMessageAsync(userId, messageLink, MessageID, true); 
                            await _telegramRecommendationService.SaveRecommendationAsync(userId, MessageID);
                        }
                        else
                        {
                            Console.WriteLine($" Сообщение {recommendedMessage.Id} не рекомендовано.");
                            await _telegramBotService.SendInteractiveMessageAsync(userId, messageLink, MessageID, false);
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при запросе (BestScore): {ex.Message}");
                    }
                }
            }
        }
    }
}