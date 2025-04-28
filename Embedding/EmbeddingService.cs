using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Embedding
{
    public class EmbeddingService : BackgroundService
    {
        // TODO : Рефакторнуть полностью
        private readonly IServiceProvider _serviceProvider;
        private readonly MessageProcessor _messageProcessor;
        public EmbeddingService(IServiceProvider serviceProvider, MessageProcessor messageProcessor)
        {
            _serviceProvider = serviceProvider;
            _messageProcessor = messageProcessor;
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var busControl = scope.ServiceProvider.GetRequiredService<IBusControl>();


            await _messageProcessor.InitializeCollectionAsync();
            Console.WriteLine(" MessageProcessor инициализирован.");

            Console.WriteLine("EmbeddingService запущен...");
            await busControl.StartAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);

            await busControl.StopAsync(stoppingToken);
            Console.WriteLine("EmbeddingService завершает работу.");
        }
    }

}
