using MassTransit;
using Newtonsoft.Json;
using ShareLib.Entities;
using Microsoft.Extensions.Logging;

namespace MessageHandler
{
    public class RabbitMQConsumer : IConsumer<TelegramMessage>
    {
        private readonly MessageProcessor _messageProcessor;
        private readonly ILogger<RabbitMQConsumer> _logger;

        public RabbitMQConsumer(MessageProcessor textProcessor, ILogger<RabbitMQConsumer> logger)
        {
            _messageProcessor = textProcessor;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<TelegramMessage> context)
        {
            var jsonMessage = JsonConvert.SerializeObject(context.Message);
            _logger.LogInformation("Получено сообщение: {JsonMessage}", jsonMessage);

            await _messageProcessor.ProcessMessageAsync(context.Message);
        }
    }
}
