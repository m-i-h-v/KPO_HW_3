using Microsoft.EntityFrameworkCore;
using PaymentsService.Models;

namespace PaymentsService.Database;

public sealed class PaymentContext : DbContext
{
	public DbSet<Account> Accounts { get; set; }
	public DbSet<InboxMessage> InboxMessages { get; set; }
	public DbSet<InboxMessageProcessed> InboxMessagesProcessed { get; set; }
	public DbSet<OutboxMessage> OutboxMessages { get; set; }

	public PaymentContext(DbContextOptions<PaymentContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Account>(entity =>
		{
			entity.HasKey(e => e.Id);

			entity.Property(e => e.Id)
				.HasColumnName("id")
				.IsRequired();

			entity.Property(e => e.UserId)
				.HasColumnName("user_id")
				.IsRequired();

			entity.HasIndex(e => e.UserId)
				.IsUnique();

            entity.Property(e => e.Balance)
				.HasColumnName("balance")
				.IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });

		modelBuilder.Entity<InboxMessage>(entity =>
		{
			entity.HasKey(e => e.Id);

			entity.Property(e => e.Id)
                .HasColumnName("id")
				.IsRequired();

			entity.HasIndex(e => e.Id)
				.IsUnique();

            entity.Property(e => e.Payload)
				.HasColumnName("payload")
				.IsRequired();

			entity.Property(e => e.IsProcessed)
				.HasColumnName("is_processed")
				.IsRequired();

			entity.Property(e => e.CreatedAt)
				.HasColumnName("created_at")
				.IsRequired();
		});

        modelBuilder.Entity<InboxMessageProcessed>(entity =>
        {
            entity.HasKey(e => e.Id);

			entity.Property(e => e.Id)
                .HasColumnName("id")
				.IsRequired();

			entity.HasIndex(e => e.Id)
				.IsUnique();

            entity.Property(e => e.ProcessedAt)
                .HasColumnName("processed_at")
                .IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

			entity.Property(e => e.Id)
				.HasColumnName("id")
				.IsRequired();

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .IsRequired();

            entity.Property(e => e.IsSent)
                .HasColumnName("is_sent")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });
    }
}