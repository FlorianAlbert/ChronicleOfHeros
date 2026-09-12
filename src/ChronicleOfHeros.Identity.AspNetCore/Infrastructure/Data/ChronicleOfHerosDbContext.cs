using ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Data.Configurations;
using ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Data;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class ChronicleOfHerosDbContext(DbContextOptions<ChronicleOfHerosDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        _ = modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        _ = modelBuilder.ApplyConfiguration(new RefreshSessionConfiguration());
    }
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes