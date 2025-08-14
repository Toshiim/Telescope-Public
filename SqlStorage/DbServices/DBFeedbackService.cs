using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class DBFeedbackService
    {
        private readonly TelegramDbContext _context;

        public DBFeedbackService(TelegramDbContext context)
        {
            _context = context;
        }

        public async Task<DbTelegramFeedback> SaveFeedbackAsync(long telegramUserId, Guid messageId, bool isLiked)
        {
            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.TelegramUserId == telegramUserId && f.MessageId == messageId);

            if (existingFeedback != null)
            {
                existingFeedback.IsLiked = isLiked;
            }
            else
            {
                existingFeedback = new DbTelegramFeedback
                {
                    TelegramUserId = telegramUserId,
                    MessageId = messageId,
                    IsLiked = isLiked
                };

                _context.Feedbacks.Add(existingFeedback);
            }

            await _context.SaveChangesAsync();
            return existingFeedback;
        }

        public async Task<List<Guid>> GetUserLikedMessagesAsync(long telegramUserId)
        {
            return await _context.Feedbacks
                .Where(f => f.TelegramUserId == telegramUserId && f.IsLiked)
                .Select(f => f.MessageId)
                .ToListAsync();
        }

        public async Task<List<Guid>> GetUserDislikedMessagesAsync(long telegramUserId)
        {
            return await _context.Feedbacks
                .Where(f => f.TelegramUserId == telegramUserId && !f.IsLiked)
                .Select(f => f.MessageId)
                .ToListAsync();
        }
    }
}
