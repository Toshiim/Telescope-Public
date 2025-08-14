using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class DBRecommendationService
    {
        private readonly TelegramDbContext _context;

        public DBRecommendationService(TelegramDbContext context)
        {
            _context = context;
        }

        public async Task<DbTelegramRecommendation> SaveRecommendationAsync(long telegramUserId, Guid messageId)
        {
            var recommendation = new DbTelegramRecommendation
            {
                TelegramUserId = telegramUserId,
                MessageId = messageId,
            };

            _context.Recommendations.Add(recommendation);
            await _context.SaveChangesAsync();

            return recommendation;
        }
    }
}
