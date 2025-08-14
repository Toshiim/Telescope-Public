using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SqlStorage
{
    public static class SqlStorageExtensions
    {
        public static IServiceCollection AddSqlStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<TelegramDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("Postgres")));

            return services;
        }
    }
}
