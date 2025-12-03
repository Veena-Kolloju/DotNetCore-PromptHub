using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace CleanArchitectureDemo.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}