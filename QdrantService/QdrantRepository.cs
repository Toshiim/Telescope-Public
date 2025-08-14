using Qdrant.Client;
using Qdrant.Client.Grpc;
using ShareLib.Entities;
using Microsoft.Extensions.Options;
using ShareLib.Settings;
using Microsoft.Extensions.Logging;

namespace QdrantService
{
    public class QdrantRepository
    {
        private readonly QdrantClient _qdrantClient;
        private readonly string _collectionName;
        private readonly ulong _vectorSize;
        private readonly ILogger<QdrantRepository> _logger;

        public QdrantRepository(IOptions<AppSettings> options, ILogger<QdrantRepository> logger)
        {
            _logger = logger;
            _qdrantClient = new QdrantClient(new Uri(options.Value.Qdrant.ConnectionString));
            _vectorSize = options.Value.Qdrant.VectorSize;
            _collectionName = options.Value.Qdrant.CollectionName;
        }

        public async Task InitializeCollectionAsync()
        {
            try
            {
                var collections = await _qdrantClient.ListCollectionsAsync();
                if (!collections.Contains(_collectionName))
                {
                    await _qdrantClient.CreateCollectionAsync(_collectionName, new VectorParams
                    {
                        Size = _vectorSize,
                        Distance = Distance.Cosine
                    });
                    _logger.LogInformation("Коллекция '{CollectionName}' создана.", _collectionName);
                }
                else
                {
                    _logger.LogInformation("Коллекция '{CollectionName}' уже существует.", _collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации коллекции '{CollectionName}'.", _collectionName);
                throw;
            }
        }

        public async Task SaveEmbeddingAsync(TelegramMessage message, float[] vector)
        {
            _logger.LogInformation("Сохраняем эмбеддинг для сообщения: {MessageId}", message.Id);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = message.Id.ToString() },
                Vectors = new Vectors { Vector = vector },
                Payload =
                {
                    ["Message Text"] = new Value { StringValue = message.MessageText },
                    ["message_id"] = new Value { IntegerValue = message.TelegramMessageId },
                    ["channel_id"] = new Value { IntegerValue = message.ChannelId },
                    ["channel_name"] = new Value { StringValue = message.ChannelName },
                    ["has_photo"] = new Value { BoolValue = message.HasPhoto },
                    ["has_video"] = new Value { BoolValue = message.HasVideo },
                    ["timestamp"] = new Value { StringValue = message.Timestamp.ToString("o") },
                    ["Message link"] = new Value { StringValue = message.PublicUrl },
                    ["Is_liked"] = new Value { StringValue = "nule" }
                }
            };

            try
            {
                await _qdrantClient.UpsertAsync(_collectionName, new[] { point });
                _logger.LogInformation("Данные успешно сохранены в Qdrant для сообщения {MessageId}.", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении эмбеддинга для сообщения {MessageId}.", message.Id);
                throw;
            }
        }

        public async Task<List<string>> SearchAsync(float[] vector, ulong topK = 5)
        {
            var result = await _qdrantClient.QueryAsync(
                    collectionName: _collectionName,
                    query: vector,
                    limit: topK);

            var links = result
                .Where(hit => hit.Payload.ContainsKey("Message link"))
                .Select(hit => hit.Payload["Message link"].StringValue)
                .Where(link => !string.IsNullOrEmpty(link))
                .ToList();

            return links;
        }
    }
}
