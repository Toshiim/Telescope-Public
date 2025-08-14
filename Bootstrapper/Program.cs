using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using MessageHandler;
using Embedding;
using Telescope;
using RecommendationService;
using TelegramBotService;
using QdrantService;
using SqlStorage;
using SqlStorage.DbServices;
using ShareLib.Settings;
using Serilog;

namespace Bootstrapper
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("Запуск Bootstrapper...");

                var host = Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        services.Configure<AppSettings>(context.Configuration);

                        services.AddSqlStorage(context.Configuration);
                        services.AddScoped<QdrantRepository>();
                        services.AddScoped<RecommendationSystem>();
                        services.AddScoped<OllamaEmbedding>();

                        services.AddScoped<DBMessageService>();
                        services.AddScoped<DBUserService>();
                        services.AddScoped<DBFeedbackService>();
                        services.AddScoped<DBRecommendationService>();
                        services.AddScoped<DBChannelService>();

                        services.AddSingleton<TelegramBotSender>();
                        services.AddHostedService(sp => sp.GetRequiredService<TelegramBotSender>());

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
                        services.AddSingleton<PublicationParser>(); 
                        services.AddSingleton<IPublicationParser>(sp => sp.GetRequiredService<PublicationParser>()); 
                        services.AddHostedService(sp => sp.GetRequiredService<PublicationParser>()); 

                        services.AddScoped<MessageProcessor>();
                    })
                    .Build();

                using (var scope = host.Services.CreateScope())
                {
                    var qdrantRepository = scope.ServiceProvider.GetRequiredService<QdrantRepository>();
                    await qdrantRepository.InitializeCollectionAsync(); // Инициализация коллекции Qdrant при запуске
                }

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Приложение упало при запуске");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}