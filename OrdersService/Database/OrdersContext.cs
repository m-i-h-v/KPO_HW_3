using Microsoft.EntityFrameworkCore;
using OrdersService.Models;

namespace OrdersService.Database;

public class OrdersContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public OrdersContext(DbContextOptions<OrdersContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.HasIndex(e => e.Id)
                .IsUnique();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            entity.Property(e => e.Price)
                .HasColumnName("price")
                .IsRequired();

            entity.Property(e => e.OrderStatus)
                .HasColumnName("order_status")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
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

            entity.Property(e => e.IsSent)
                .HasColumnName("is_sent")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });
    }
}