using Microsoft.EntityFrameworkCore;

namespace ChronicleOfHeros.Api.Data;

public sealed class ChronicleOfHerosDbContext(DbContextOptions<ChronicleOfHerosDbContext> options) : DbContext(options);