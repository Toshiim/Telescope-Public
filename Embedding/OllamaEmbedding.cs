using Microsoft.Extensions.AI;
using OllamaSharp;
using ShareLib.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;

namespace Embedding
{
    public class OllamaEmbedding
    {
        private readonly OllamaApiClient _ollamaClient;
        private readonly ILogger<OllamaEmbedding> _logger;

        public OllamaEmbedding(IOptions<AppSettings> options, ILogger<OllamaEmbedding> logger)
        {
            _ollamaClient = new OllamaApiClient(new Uri(options.Value.Ollama.ConnectionString), options.Value.Ollama.Model);
            _logger = logger;
        }

        public async Task<float[]> GenerateEmbedding(string text)
        {
            _logger.LogInformation("Обрабатываем текст: {MessageText}", text);

            var embedding = await _ollamaClient.GenerateEmbeddingAsync(text);
            _logger.LogInformation("Эмбеддинг : {EmbeddingPreview}...", string.Join(", ", embedding.Vector.ToArray().Take(5)));
            return embedding.Vector.ToArray();
        }
    }
}

