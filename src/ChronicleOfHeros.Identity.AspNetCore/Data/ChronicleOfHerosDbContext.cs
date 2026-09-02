using ChronicleOfHeros.Identity.AspNetCore.Data.Configurations;
using ChronicleOfHeros.Identity.AspNetCore.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChronicleOfHeros.Identity.AspNetCore.Data;

internal sealed class ChronicleOfHerosDbContext(DbContextOptions<ChronicleOfHerosDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshSessionConfiguration());
    }
}