using ChronicleOfHeros.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChronicleOfHeros.Api.Data;

public sealed class ChronicleOfHerosDbContext(DbContextOptions<ChronicleOfHerosDbContext> options)
	: IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
	public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
}