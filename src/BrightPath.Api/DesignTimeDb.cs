using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BrightPath.Api;

// Lets dotnet-ef build the context without running Program.cs.
public class DesignTimeDb : IDesignTimeDbContextFactory<BrightPathDb>
{
    public BrightPathDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BrightPathDb>()
            .UseNpgsql("Host=localhost;Port=5433;Database=brightpath;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention().Options);
}
