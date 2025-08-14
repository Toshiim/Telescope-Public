using MassTransit;

namespace Telescope
{
    public sealed class RabbitMQProducer
    {
        private readonly ISendEndpointProvider _sendEndpointProvider;

        public RabbitMQProducer(ISendEndpointProvider sendEndpointProvider)
        {
            _sendEndpointProvider = sendEndpointProvider;
        }

        public async Task SendMessageAsync<T>(T message) where T : class
        {
            var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:telegram_posts"));
            await sendEndpoint.Send(message);
        }
    }
}
