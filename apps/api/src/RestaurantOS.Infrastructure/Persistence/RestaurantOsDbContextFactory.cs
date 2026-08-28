using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestaurantOS.Infrastructure;

public sealed class RestaurantOsDbContextFactory : IDesignTimeDbContextFactory<RestaurantOsDbContext>
{
    public RestaurantOsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RestaurantOs")
            ?? "Server=(localdb)\\mssqllocaldb;Database=RestaurantOS;Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RestaurantOsDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new RestaurantOsDbContext(options);
    }
}
