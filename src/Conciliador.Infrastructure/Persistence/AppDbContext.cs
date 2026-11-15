using Conciliador.Domain.Reconciliations;
using Conciliador.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Conciliador.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Name).HasMaxLength(120).IsRequired();
            user.Property(u => u.Email).HasMaxLength(200).IsRequired();
            user.Property(u => u.PasswordHash).HasMaxLength(300).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Reconciliation>(reconciliation =>
        {
            reconciliation.ToTable("reconciliations");
            reconciliation.HasKey(r => r.Id);
            reconciliation.Property(r => r.Id).ValueGeneratedNever();
            reconciliation.Property(r => r.CreatedBy).HasMaxLength(200).IsRequired();
            reconciliation.Property(r => r.Operation).HasConversion<string>().HasMaxLength(20);
            reconciliation.Property(r => r.TotalSales).HasPrecision(18, 2);
            reconciliation.Property(r => r.TotalStatement).HasPrecision(18, 2);
            reconciliation.Property(r => r.TotalDifference).HasPrecision(18, 2);
            reconciliation.HasIndex(r => r.CreatedAtUtc);
            reconciliation.Ignore(r => r.IsFullyMatched);

            reconciliation.OwnsMany(r => r.Lines, line =>
            {
                line.ToTable("reconciliation_lines");
                line.WithOwner().HasForeignKey("ReconciliationId");
                line.Property<int>("Id");
                line.HasKey("Id");
                line.Property(l => l.Brand).HasMaxLength(40).IsRequired();
                line.Property(l => l.SalesAmount).HasPrecision(18, 2);
                line.Property(l => l.StatementAmount).HasPrecision(18, 2);
                line.Property(l => l.Difference).HasPrecision(18, 2);
                line.Property(l => l.Status).HasConversion<string>().HasMaxLength(30);
            });

            reconciliation.OwnsMany(r => r.Issues, issue =>
            {
                issue.ToTable("reconciliation_issues");
                issue.WithOwner().HasForeignKey("ReconciliationId");
                issue.Property<int>("Id");
                issue.HasKey("Id");
                issue.Property(i => i.Severity).HasConversion<string>().HasMaxLength(20);
                issue.Property(i => i.Code).HasMaxLength(60).IsRequired();
                issue.Property(i => i.Message).HasMaxLength(600).IsRequired();
            });

            reconciliation.OwnsMany(r => r.Sources, source =>
            {
                source.ToTable("reconciliation_sources");
                source.WithOwner().HasForeignKey("ReconciliationId");
                source.Property<int>("Id");
                source.HasKey("Id");
                source.Property(s => s.FileName).HasMaxLength(260).IsRequired();
                source.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            });
        });
    }
}
