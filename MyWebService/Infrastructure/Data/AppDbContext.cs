using Microsoft.EntityFrameworkCore;
using MyWebService.Domain.Entities;
using System.Reflection;

namespace MyWebService.Api.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Define your DbSets here based on your Domain entities
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    // Assuming you have an Order entity based on your file tree
    // public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discover and apply all IEntityTypeConfiguration<T> instances
        // found in the same assembly as the AppDbContext.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}