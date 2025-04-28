using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SqlStorage
{

    // TODO : Добавить список каналов на которые подписан пользователь.
    public class DbTelegramUser
    {
        [Key]
        public long TelegramUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual ICollection<DbTelegramFeedback> Feedbacks { get; set; }
        public virtual ICollection<DbTelegramRecommendation> Recommendations { get; set; }

        public DbTelegramUser()
        {
            Feedbacks = new HashSet<DbTelegramFeedback>();
            Recommendations = new HashSet<DbTelegramRecommendation>();
        }
    }

    public class DbTelegramMessage
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string MessageText { get; set; }

        public long TelegramMessageId { get; set; }

        public long ChannelId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ChannelName { get; set; }

        public bool HasPhoto { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool HasVideo { get; set; }

        public string? PublicUrl { get; set; }

        // Навигационные свойства
        public virtual ICollection<DbTelegramFeedback> Feedbacks { get; set; }
        public virtual ICollection<DbTelegramRecommendation> Recommendations { get; set; }

        public DbTelegramMessage()
        {
            Feedbacks = new HashSet<DbTelegramFeedback>();
            Recommendations = new HashSet<DbTelegramRecommendation>();
        }
    }

    public class DbTelegramFeedback
    {
        [Key]
        public int Id { get; set; }

        public long TelegramUserId { get; set; }

        public Guid MessageId { get; set; }

        [Required]
        public bool IsLiked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("TelegramUserId")]
        public virtual DbTelegramUser User { get; set; }

        [ForeignKey("MessageId")]
        public virtual DbTelegramMessage Message { get; set; }
    }

    public class DbTelegramRecommendation
    {
        [Key]
        public int Id { get; set; }

        public long TelegramUserId { get; set; }

        public Guid MessageId { get; set; }

        public DateTime RecommendedAt { get; set; } = DateTime.UtcNow;

        public bool WasViewed { get; set; }

        [ForeignKey("TelegramUserId")]
        public virtual DbTelegramUser User { get; set; }

        [ForeignKey("MessageId")]
        public virtual DbTelegramMessage Message { get; set; }
    }

}
