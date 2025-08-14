using ShareLib.Entities;
using QdrantService;
using RecommendationService;
using SqlStorage.DbServices;
using Microsoft.Extensions.Logging;
using Embedding;

namespace MessageHandler
{
    public class MessageProcessor
    {
        private readonly QdrantRepository _qdrantRepository;
        private readonly RecommendationSystem _recommendationSystem;
        private readonly DBMessageService _telegramMessageService;
        private readonly ILogger<MessageProcessor> _logger;
        private readonly OllamaEmbedding _ollamaEmbedding;

        public MessageProcessor(
            QdrantRepository qdrantRepository,
            RecommendationSystem recommendationSystem,
            DBMessageService telegramMessageService,
            ILogger<MessageProcessor> logger,
            OllamaEmbedding ollamaEmbedding
            )
        {
            _qdrantRepository = qdrantRepository ?? throw new ArgumentNullException(nameof(qdrantRepository));
            _recommendationSystem = recommendationSystem ?? throw new ArgumentNullException(nameof(recommendationSystem));
            _telegramMessageService = telegramMessageService ?? throw new ArgumentNullException(nameof(telegramMessageService));
            _logger = logger;
            _ollamaEmbedding = ollamaEmbedding ?? throw new ArgumentNullException(nameof(ollamaEmbedding));
        }

        public async Task InitializeCollectionAsync()
        {
            await _qdrantRepository.InitializeCollectionAsync();
        }

        public async Task ProcessMessageAsync(TelegramMessage message)
        {

            if (message.MessageText != "")
            {
                float[] vector = await _ollamaEmbedding.GenerateEmbedding(message.MessageText);

                _logger.LogInformation("Эмбеддинг : {EmbeddingPreview}...", string.Join(", ", vector.Take(5)));

                await _qdrantRepository.SaveEmbeddingAsync(message, vector);
                await _telegramMessageService.SaveMessageAsync(message);

                // В сервис рекомендаций
                await _recommendationSystem.ProcessRecommendationAsync(message);
            }
        }
    }
}
