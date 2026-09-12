using ChronicleOfHeros.Identity.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChronicleOfHeros.Identity.AspNetCore.Data.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        _ = builder.Property(user => user.IsActive).IsRequired();
        _ = builder.Property(user => user.MustChangePassword).IsRequired();
    }
}