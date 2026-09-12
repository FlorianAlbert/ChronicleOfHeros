using ChronicleOfHeros.Identity.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChronicleOfHeros.Identity.AspNetCore.Data.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        _ = builder.ToTable("RefreshSessions");
        _ = builder.HasKey(session => session.Id);
        _ = builder.Property(session => session.UserId).IsRequired();
        _ = builder.Property(session => session.TokenHash).IsRequired();
        _ = builder.Property(session => session.FamilyId).IsRequired();
        _ = builder.Property(session => session.CreatedAtUtc).IsRequired();
        _ = builder.Property(session => session.ExpiresAtUtc).IsRequired();
        _ = builder.Property(session => session.FamilyExpiresAtUtc).IsRequired();
        _ = builder.HasIndex(session => session.UserId);
        _ = builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}