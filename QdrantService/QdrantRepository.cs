using Qdrant.Client;
using Qdrant.Client.Grpc;
using ShareLib.Entities;
using Microsoft.Extensions.Options;
using ShareLib.Settings;

namespace QdrantService
{
    public class QdrantRepository
    {
        private readonly QdrantClient _qdrantClient;
        private readonly string _collectionName;
        private readonly ulong _vectorSize;

        public QdrantRepository(IOptions<AppSettings> options)
        {
            _qdrantClient = new QdrantClient(new Uri(options.Value.Qdrant.ConnectionString));
            _vectorSize = options.Value.Qdrant.VectorSize;
            _collectionName = options.Value.Qdrant.CollectionName;
        }

        public async Task InitializeCollectionAsync()
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            if (!collections.Contains(_collectionName))
            {
                await _qdrantClient.CreateCollectionAsync(_collectionName, new VectorParams
                {
                    Size = _vectorSize, 
                    Distance = Distance.Cosine
                });
                Console.WriteLine($" Коллекция '{_collectionName}' создана.");
            }
            else
            {
                Console.WriteLine($" Коллекция '{_collectionName}' уже существует.");
            }
        }

        public async Task SaveEmbeddingAsync(TelegramMessage message, float[] vector)
        {
            Console.WriteLine($" Сохраняем эмбеддинг для сообщения: {message.Id}");
            // TODO :  Избыточно, почистить. 
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

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point });
            Console.WriteLine($" Данные сохранены в Qdrant");
        }
    }
}