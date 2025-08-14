using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class DBUserService
    {
        private readonly TelegramDbContext _context;

        public DBUserService(TelegramDbContext context)
        {
            _context = context;
        }


        public async Task<DbTelegramUser> CreateUserAsync(long telegramUserId, string username, string firstName = null, string lastName = null)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

            if (existingUser != null)
            {
                bool modified = false;

                if (existingUser.Username != username)
                {
                    existingUser.Username = username;
                    modified = true;
                }

                if (existingUser.FirstName != firstName)
                {
                    existingUser.FirstName = firstName;
                    modified = true;
                }

                if (existingUser.LastName != lastName)
                {
                    existingUser.LastName = lastName;
                    modified = true;
                }

                if (modified)
                {
                    _context.Users.Update(existingUser);
                    await _context.SaveChangesAsync();
                }

                return existingUser;
            }

            var newUser = new DbTelegramUser
            {
                TelegramUserId = telegramUserId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return newUser;
        }

        public async Task<bool> DeleteUserAsync(long telegramUserId)
        {
            var user = await _context.Users
                .Include(u => u.Feedbacks)
                .Include(u => u.Recommendations)
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}