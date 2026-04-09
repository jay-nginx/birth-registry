using BirthRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace BirthRegistry.Data;

public class BirthRegistryDbContext(DbContextOptions<BirthRegistryDbContext> options) : DbContext(options)
{
    public DbSet<BirthRecord> BirthRecords => Set<BirthRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BirthRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.RegistrationNumber).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.RegistrationNumber).IsUnique();
            entity.HasIndex(e => new { e.ChildLastName, e.DateOfBirth });
        });
    }
}
