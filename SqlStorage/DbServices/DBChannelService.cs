using Microsoft.EntityFrameworkCore;

namespace SqlStorage.DbServices
{
    public class DBChannelService
    {
        private readonly TelegramDbContext _context;

        public DBChannelService(TelegramDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Добавляет новый канал в базу данных или возвращает существующий, если он уже есть.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public async Task<DbTelegramChannel> AddOrGetChannelAsync(long channelId, string channelName)
        {
            var channel = await _context.Set<DbTelegramChannel>()
                .FirstOrDefaultAsync(c => c.ChannelId == channelId);

            if (channel == null)
            {
                channel = new DbTelegramChannel
                {
                    ChannelId = channelId,
                    ChannelName = channelName
                };
                _context.Add(channel);
                await _context.SaveChangesAsync();
            }

            return channel;
        }

        /// <summary>
        /// Связывает пользователя с каналом (если не связан).True, если подписка была успешно добавлена, иначе false.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public async Task<bool> SubscribeUserToChannelAsync(long userId, long channelId)
        {
            var exists = await _context.Set<DbTelegramUserChannel>()
                .AnyAsync(uc => uc.TelegramUserId == userId && uc.ChannelId == channelId);

            if (!exists)
            {
                _context.Add(new DbTelegramUserChannel
                {
                    TelegramUserId = userId,
                    ChannelId = channelId,
                    SubscribedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return true; 
            }

            return false; 
        }

        /// <summary>
        /// Получить список каналов, на которые подписан конкретный пользователь
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<List<DbTelegramChannel>> GetChannelsByUserAsync(long userId)
        {
            return await _context.Set<DbTelegramUserChannel>()
                .Include(uc => uc.Channel)
                .Where(uc => uc.TelegramUserId == userId)
                .Select(uc => uc.Channel)
                .ToListAsync();
        }

        /// <summary>
        /// Получить список ID всех пользователей, подписанных на заданный канал
        /// </summary>
        /// <param name="channelId">ID канала</param>
        /// <returns>Список TelegramUserId</returns>
        public async Task<List<long>> GetUserIdsByChannelIdAsync(long channelId)
        {
            return await _context.Set<DbTelegramUserChannel>()
                .Where(uc => uc.ChannelId == channelId)
                .Select(uc => uc.TelegramUserId)
                .ToListAsync();
        }

        /// <summary>
        /// Отписывает пользователя от канала, и если у канала не осталось подписчиков удаляет его.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public async Task<bool> UnsubscribeUserFromChannelAsync(long userId, string channelName)
        {
            var channel = await _context.Set<DbTelegramChannel>()
                .Include(c => c.Subscribers)
                .FirstOrDefaultAsync(c => c.ChannelName == channelName);

            if (channel == null)
                return false; 

            var link = await _context.Set<DbTelegramUserChannel>()
                .FirstOrDefaultAsync(x => x.TelegramUserId == userId && x.ChannelId == channel.ChannelId);

            if (link == null)
                return false; 

            _context.Remove(link);
            await _context.SaveChangesAsync();

            var stillSubscribed = await _context.Set<DbTelegramUserChannel>()
                .AnyAsync(x => x.ChannelId == channel.ChannelId);

            if (!stillSubscribed)
            {
                _context.Remove(channel);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<List<DbTelegramChannel>> GetAllChannelsAsync()
        {
            return await _context.Set<DbTelegramChannel>().ToListAsync();
        }
    }
}