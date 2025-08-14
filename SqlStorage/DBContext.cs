using Microsoft.EntityFrameworkCore;

namespace SqlStorage
{
    public class TelegramDbContext : DbContext
    {
        public DbSet<DbTelegramUser> Users { get; set; }
        public DbSet<DbTelegramMessage> Messages { get; set; }
        public DbSet<DbTelegramFeedback> Feedbacks { get; set; }
        public DbSet<DbTelegramRecommendation> Recommendations { get; set; }

        public TelegramDbContext(DbContextOptions<TelegramDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbTelegramFeedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DbTelegramFeedback>()
                .HasOne(f => f.Message)
                .WithMany(m => m.Feedbacks)
                .HasForeignKey(f => f.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DbTelegramRecommendation>()
                .HasOne(r => r.User)
                .WithMany(u => u.Recommendations)
                .HasForeignKey(r => r.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DbTelegramRecommendation>()
                .HasOne(r => r.Message)
                .WithMany(m => m.Recommendations)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DbTelegramFeedback>()
                .HasIndex(f => new { f.TelegramUserId, f.MessageId })
                .IsUnique();

            modelBuilder.Entity<DbTelegramRecommendation>()
                .HasIndex(r => new { r.TelegramUserId, r.MessageId });

            modelBuilder.Entity<DbTelegramUserChannel>()
                .HasKey(uc => new { uc.TelegramUserId, uc.ChannelId });

            modelBuilder.Entity<DbTelegramUserChannel>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.SubscribedChannels)
                .HasForeignKey(uc => uc.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DbTelegramUserChannel>()
                .HasOne(uc => uc.Channel)
                .WithMany(c => c.Subscribers)
                .HasForeignKey(uc => uc.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}