using Microsoft.EntityFrameworkCore;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class ModelSnapshotTests
{
    [Fact]
    public void RelationalModelMatchesCommittedMigrations()
    {
        using var db = new RestaurantOsDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
