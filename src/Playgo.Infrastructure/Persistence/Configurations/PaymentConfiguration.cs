using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).HasMaxLength(8);
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(200);

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.ProviderTransactionId);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Subscription)
            .WithMany()
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Plan)
            .WithMany()
            .HasForeignKey(p => p.PlanId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
