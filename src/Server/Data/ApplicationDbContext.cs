using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace pckg.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<PackageReviewEntity> PackageReviews => Set<PackageReviewEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApiKeyEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Prefix).IsRequired().HasMaxLength(16);
            entity.Property(x => x.KeyHash).IsRequired();
            entity.Property(x => x.ScopesCsv).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Name });
        });

        builder.Entity<PackageEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.OwnerUserId);
        });

        builder.Entity<PackageReviewEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(512);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ReviewerUserId).HasMaxLength(450);
            entity.Property(x => x.ReviewNotes).HasMaxLength(512);
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
