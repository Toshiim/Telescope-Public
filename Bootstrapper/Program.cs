using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using Embedding;
using Telescope;
using RecommendationService;
using TelegramBotService;
using QdrantService;
using SqlStorage;
using SqlStorage.DbServices;
using ShareLib.Settings;

namespace Bootstrapper
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.Configure<AppSettings>(context.Configuration);

                    services.AddSqlStorage(context.Configuration);  
          
                    services.AddSingleton<QdrantRepository>();
                    services.AddSingleton<RecommendationSystem>();

                    services.AddScoped<TelegramMessageService>();
                    services.AddScoped<TelegramUserService>();
                    services.AddScoped<TelegramFeedbackService>();
                    services.AddScoped<TelegramRecommendationService>();
                    services.AddScoped<TelegramDbContext>();

                    services.AddSingleton<TelegramBotSender>();

                    services.AddMassTransit(x =>
                    {
                        x.AddConsumer<RabbitMQConsumer>();

                        x.UsingRabbitMq((context, cfg) =>
                        {
                            cfg.Host("rabbitmq://localhost", h =>
                            {
                                h.Username("guest");
                                h.Password("guest");
                            });

                            cfg.ReceiveEndpoint("telegram_posts", e =>
                            {
                                e.ConfigureConsumer<RabbitMQConsumer>(context);
                            });
                        });
                    });

                    services.AddScoped<RabbitMQProducer>();


                    // TODO : Реворк на медиатр. Необходимо разделение работы с БД и логики,
                    // Цель - перевести логику без состояний в Singltone, работу с БД в Scoped.
                    services.AddHostedService<EmbeddingService>();
                    services.AddHostedService<PublicationParser>();

                    services.AddSingleton<MessageProcessor>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}

