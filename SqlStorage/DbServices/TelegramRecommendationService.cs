using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class TelegramRecommendationService
    {
        private readonly TelegramDbContext _context;

        public TelegramRecommendationService(TelegramDbContext context)
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

        public async Task<List<Guid>> GetRecommendedMessagesForUserAsync(long telegramUserId)
        {
            return await _context.Recommendations
                .Where(r => r.TelegramUserId == telegramUserId)
                .Select(r => r.MessageId)
                .ToListAsync();
        }
    }
}
