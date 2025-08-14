using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SqlStorage
{
    public class DbTelegramUser
    {
        [Key]
        public long TelegramUserId { get; set; }

        [MaxLength(100)]
        public string? Username { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<DbTelegramFeedback> Feedbacks { get; set; }
        public virtual ICollection<DbTelegramRecommendation> Recommendations { get; set; }
        public virtual ICollection<DbTelegramUserChannel> SubscribedChannels { get; set; } = new HashSet<DbTelegramUserChannel>();

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

    public class DbTelegramUserChannel
    {
        public long TelegramUserId { get; set; }
        public long ChannelId { get; set; }

        public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("TelegramUserId")]
        public virtual DbTelegramUser User { get; set; }

        [ForeignKey("ChannelId")]
        public virtual DbTelegramChannel Channel { get; set; }
    }


    public class DbTelegramChannel
    {
        [Key]
        public long ChannelId { get; set; }
        [Required]
        [MaxLength(100)]
        public string ChannelName { get; set; }

        public virtual ICollection<DbTelegramUserChannel> Subscribers { get; set; } = new HashSet<DbTelegramUserChannel>();
    }
}