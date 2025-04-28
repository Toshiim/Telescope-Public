using ShareLib.Entities;

namespace SqlStorage
{
    public static class TelegramMessageMapper
    {
        /// <summary>
        /// Преобразует ShareLib.TelegramMessage в DbTelegramMessage для Entity Framework
        /// </summary>
        public static DbTelegramMessage ToEfEntity(this TelegramMessage source)
        {
            return new DbTelegramMessage
            {
                Id = source.Id,
                MessageText = source.MessageText,
                TelegramMessageId = source.TelegramMessageId,
                ChannelId = source.ChannelId,
                ChannelName = source.ChannelName,
                HasPhoto = source.HasPhoto,
                Timestamp = source.Timestamp,
                HasVideo = source.HasVideo,
                PublicUrl = source.PublicUrl
            };
        }


        /// <summary>
        /// Преобразует EDbTelegramMessage в  SharedLib.TelegramMessage
        /// </summary>
        public static TelegramMessage ToEntity(this DbTelegramMessage source)
        {
            // Используем конструктор с параметром id, который есть в базовом классе
            var result = new TelegramMessage(source.Id)
            {
                MessageText = source.MessageText,
                TelegramMessageId = source.TelegramMessageId,
                ChannelId = source.ChannelId,
                ChannelName = source.ChannelName,
                HasPhoto = source.HasPhoto,
                HasVideo = source.HasVideo,
                PublicUrl = source.PublicUrl
            };

            return result;
        }
    }
}
