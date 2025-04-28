using Microsoft.Extensions.AI;
using OllamaSharp;
using ShareLib.Entities;
using ShareLib.Settings;
using Microsoft.Extensions.Options;
using QdrantService;
using RecommendationService;
using SqlStorage.DbServices;

namespace Embedding
{
    public class MessageProcessor
    {
        private readonly QdrantRepository _qdrantRepository;
        private readonly OllamaApiClient _ollamaClient;
        private readonly RecommendationSystem _recommendationSystem;
        private readonly TelegramMessageService _telegramMessageService;
        public MessageProcessor (QdrantRepository qdrantRepository, RecommendationSystem recommendationSystem,
            TelegramMessageService telegramMessageService, IOptions<AppSettings> options)
        {
            _qdrantRepository = qdrantRepository ?? throw new ArgumentNullException(nameof(qdrantRepository));
            _recommendationSystem = recommendationSystem ?? throw new ArgumentNullException(nameof(recommendationSystem));
            _telegramMessageService = telegramMessageService ?? throw new ArgumentNullException(nameof(telegramMessageService));
            _ollamaClient = new OllamaApiClient(new Uri(options.Value.Ollama.ConnectionString), options.Value.Ollama.Model);
        }

        public async Task InitializeCollectionAsync()
        {
            await _qdrantRepository.InitializeCollectionAsync();
        }

        public async Task ProcessMessageAsync(TelegramMessage message)
        {
            Console.WriteLine($" Обрабатываем текст: {message.MessageText}");

            if (message.MessageText != "")
            {
                var embedding = await _ollamaClient.GenerateEmbeddingAsync(message.MessageText);
                float[] vector = embedding.Vector.ToArray();

                Console.WriteLine($" Эмбеддинг: {string.Join(", ", vector.Take(5))}...");

                await _qdrantRepository.SaveEmbeddingAsync(message, vector);
                await _telegramMessageService.SaveMessageAsync(message);

                // В сервис рекомендаций
                await _recommendationSystem.ProcessRecommendationAsync(message.Id);
            }
        }
    }
}