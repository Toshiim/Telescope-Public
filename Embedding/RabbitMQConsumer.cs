using MassTransit;
using Newtonsoft.Json;
using ShareLib.Entities;

namespace Embedding
{
    public class RabbitMQConsumer : IConsumer<TelegramMessage>
    {
        private readonly MessageProcessor _messageProcessor;

        public RabbitMQConsumer(MessageProcessor textProcessor)
        {
            _messageProcessor = textProcessor;
            
        }

        public async Task Consume(ConsumeContext<TelegramMessage> context)
        {
            var jsonMessage = JsonConvert.SerializeObject(context.Message);
            Console.WriteLine($" Получено сообщение: {jsonMessage}");

            await _messageProcessor.ProcessMessageAsync(context.Message);
        }

    }
}
