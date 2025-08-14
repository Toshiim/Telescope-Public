using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using TelegramBotService;
using Microsoft.Extensions.Options;
using ShareLib.Settings;
using SqlStorage.DbServices;
using Microsoft.Extensions.Logging;
using ShareLib.Entities;

namespace RecommendationService
{
    public class RecommendationSystem
    {
        private readonly QdrantClient _qdrantClient;
        private readonly string _collectionName;
        private readonly TelegramBotSender _telegramBotService;
        private readonly DBChannelService _telegramChannelService;
        private readonly DBMessageService _telegramMessageService;
        private readonly DBFeedbackService _telegramFeedbackService;
        private readonly DBRecommendationService _telegramRecommendationService;
        private readonly ILogger<RecommendationSystem> _logger;

        public RecommendationSystem(
            TelegramBotSender telegramBotSender,
            DBRecommendationService telegramRecommendationService,
            DBChannelService telegramChannelService,
            DBMessageService telegramMessageService,
            DBFeedbackService telegramFeedbackService,
            IOptions<AppSettings> options,
            ILogger<RecommendationSystem> logger)
        {
            _qdrantClient = new QdrantClient(new Uri(options.Value.Qdrant.ConnectionString));
            _telegramBotService = telegramBotSender ?? throw new ArgumentNullException(nameof(telegramBotSender));
            _telegramChannelService = telegramChannelService ?? throw new ArgumentNullException(nameof(telegramChannelService));
            _telegramMessageService = telegramMessageService ?? throw new ArgumentNullException(nameof(telegramMessageService));
            _telegramFeedbackService = telegramFeedbackService ?? throw new ArgumentNullException(nameof(telegramFeedbackService));
            _telegramRecommendationService = telegramRecommendationService ?? throw new ArgumentNullException(nameof(telegramRecommendationService));
            _collectionName = options.Value.Qdrant.CollectionName;
            _logger = logger;
        }

        public async Task ProcessRecommendationAsync(TelegramMessage Message)
        {
            _logger.LogInformation("Поиск записи с ID: {MessageID} в коллекции '{CollectionName}'", Message, _collectionName);
            var userIds = await _telegramChannelService.GetUserIdsByChannelIdAsync(Message.ChannelId);

            foreach (var userId in userIds)
            {
                var likedMessages = await _telegramFeedbackService.GetUserLikedMessagesAsync(userId);
                var dislikedMessages = await _telegramFeedbackService.GetUserDislikedMessagesAsync(userId);
                var feedbacks = likedMessages.Count + dislikedMessages.Count;

                double threshold = ComputeAdaptiveThreshold(feedbacks);
                bool exploration = Random.Shared.NextDouble() < 0.1; // 10% шанс на исследование

                if (feedbacks < 5 && likedMessages.Count == 0 || exploration)
                {
                    _logger.LogInformation("Недостаточно отзывов для пользователя {UserId}. Пропускаем сообщение {MessageID}.", userId, Message);
                    await _telegramBotService.SendInteractiveMessageAsync(userId, Message.PublicUrl, Message.Id, true, 0, 0);
                    await _telegramRecommendationService.SaveRecommendationAsync(userId, Message.Id);
                }
                else
                {
                    _logger.LogInformation("Поиск рекомендаций для пользователя {UserId} на основе отзывов.", userId);
                    try
                    {
                        var searchResultBest = await _qdrantClient.QueryAsync(
                            collectionName: _collectionName,
                            query: new RecommendInput
                            {
                                Positive = { likedMessages.Select(id => new VectorInput { Id = id }) },
                                Negative = { dislikedMessages.Select(id => new VectorInput { Id = id }) },
                                Strategy = RecommendStrategy.BestScore
                            },
                            filter: HasId(Message.Id),
                            limit: 1
                        );

                        var recommendedMessage = searchResultBest.FirstOrDefault();
                        _logger.LogInformation("ID: {MessageID}, Score: {Score}", Message, recommendedMessage?.Score);

                        double? score = recommendedMessage?.Score;

                        bool isRecommended = exploration || (score.HasValue && score.Value > threshold);

                        if (isRecommended)
                        {
                            _logger.LogInformation("Рекомендовано сообщение {MessageID} пользователю {UserId}", Message, userId);
                            await _telegramBotService.SendInteractiveMessageAsync(userId, Message.PublicUrl, Message.Id, true, score, threshold);
                            await _telegramRecommendationService.SaveRecommendationAsync(userId, Message.Id);
                        }
                        else
                        {
                            _logger.LogInformation("Сообщение {MessageID} не рекомендовано.", recommendedMessage?.Id);
                            await _telegramBotService.SendInteractiveMessageAsync(userId, Message.PublicUrl, Message.Id, false, score, threshold);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при запросе (BestScore)");
                    }
                }
            }
        }

        private double ComputeAdaptiveThreshold(int feedbacks)
        {
            double baseThreshold = 0.3;
            double maxThreshold = 0.7;
            if (feedbacks <= 0) return baseThreshold;

            double normalized = Math.Log(feedbacks + 1) / Math.Log(100 + 1); // log(1) до log(101) => 0..1
            return baseThreshold + (maxThreshold - baseThreshold) * normalized;
        }

    }
}
