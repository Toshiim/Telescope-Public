using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class TelegramUserService
    {
        private readonly TelegramDbContext _context;

        public TelegramUserService(TelegramDbContext context)
        {
            _context = context;
        }

        public async Task<DbTelegramUser> GetUserByIdAsync(long telegramUserId)
        {
            return await _context.Users.FindAsync(telegramUserId);
        }

        // TODO : Обработка существования
        public async Task<DbTelegramUser> CreateUserAsync(long telegramUserId, string username, string firstName = null, string lastName = null)
        {
            var user = new DbTelegramUser
            {
                TelegramUserId = telegramUserId,
                Username = username,
                FirstName = firstName,
                LastName = lastName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<long[]> GetAllUserIdsAsync()
        {
            var userIds = await _context.Users.Select(u => u.TelegramUserId).ToArrayAsync();
            return userIds;
        }

    }
}
