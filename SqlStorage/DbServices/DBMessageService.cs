using ShareLib.Entities;

namespace SqlStorage.DbServices
{
    public class DBMessageService
    {
        private readonly TelegramDbContext _context;

        public DBMessageService(TelegramDbContext context)
        {
            _context = context;
        }

        public async Task<DbTelegramMessage> SaveMessageAsync(TelegramMessage source)
        {
            var message = source.ToEfEntity();
            var existingMessage = await _context.Messages.FindAsync(message.Id);

            if (existingMessage == null)
            {
                _context.Messages.Add(message);
            }
            else
            {
                _context.Entry(existingMessage).CurrentValues.SetValues(message);
            }

            await _context.SaveChangesAsync();
            return message;
        }
    }
}