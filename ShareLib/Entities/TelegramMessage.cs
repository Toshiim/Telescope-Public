namespace ShareLib.Entities
{
    public class TelegramMessage : BaseEntity<TelegramMessage>
    {
        public string MessageText { get; set; }
        public long TelegramMessageId { get; set; }  
        public long ChannelId { get; set; }  
        public string ChannelName { get; set; }   
        public bool HasPhoto { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;  
        public bool HasVideo { get; set; }
        public string? PublicUrl { get; set; }

        // Конструктор, который вызывает конструктор базового класса
        public TelegramMessage(Guid id) : base(id)
        {
        }

        // Стандартный конструктор
        public TelegramMessage() : base()
        {
        }

    }
}
