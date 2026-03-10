using Microsoft.EntityFrameworkCore;
using TgBotVPN.Models;

namespace TgBotVPN.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TelegramUser> TelegramUsers { get; set; }
    public DbSet<OutlineKey> OutlineKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TelegramUser
        modelBuilder.Entity<TelegramUser>(entity =>
        {
            entity.HasKey(e => e.TelegramId);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IsWhitelisted).HasDefaultValue(false);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.IsWhitelisted);
        });

        // Configure OutlineKey
        modelBuilder.Entity<OutlineKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.KeyName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AccessUrl).IsRequired().HasMaxLength(2040);
            entity.Property(e => e.DataLimitGb).HasDefaultValue(100);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // One-to-One relationship
            entity.HasOne(e => e.TelegramUser)
                .WithOne(u => u.OutlineKey)
                .HasForeignKey<OutlineKey>(e => e.TelegramId)
                .OnDelete(DeleteBehavior.Cascade);

            // Create unique constraint on TelegramId to enforce 1-to-1 relationship
            entity.HasIndex(e => e.TelegramId).IsUnique();
        });
    }
}
