using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Server.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<PackageVersionEntity> PackageVersions => Set<PackageVersionEntity>();
    public DbSet<PackageReviewEntity> PackageReviews => Set<PackageReviewEntity>();
    public DbSet<PackageCommunityReviewEntity> PackageCommunityReviews => Set<PackageCommunityReviewEntity>();
    public DbSet<PackageIssueEntity> PackageIssues => Set<PackageIssueEntity>();
    public DbSet<PackageIssueVoteEntity> PackageIssueVotes => Set<PackageIssueVoteEntity>();
    public DbSet<UserEmailEntity> UserEmails => Set<UserEmailEntity>();
    public DbSet<UserRatingEntity> UserRatings => Set<UserRatingEntity>();
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<BoardPostEntity> BoardPosts => Set<BoardPostEntity>();
    public DbSet<BoardPostCommentEntity> BoardPostComments => Set<BoardPostCommentEntity>();
    public DbSet<BoardPostVoteEntity> BoardPostVotes => Set<BoardPostVoteEntity>();
    public DbSet<BoardCommentVoteEntity> BoardCommentVotes => Set<BoardCommentVoteEntity>();
    public DbSet<ResourcePermissionEntity> ResourcePermissions => Set<ResourcePermissionEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<UserNotificationPreferenceEntity> NotificationPreferences => Set<UserNotificationPreferenceEntity>();
    public DbSet<EmailSettingsEntity> EmailSettings => Set<EmailSettingsEntity>();
    public DbSet<FollowPackageEntity> PackageFollows => Set<FollowPackageEntity>();
    public DbSet<FollowPublisherEntity> PublisherFollows => Set<FollowPublisherEntity>();
    public DbSet<PackageTagEntity> PackageTags => Set<PackageTagEntity>();
    public DbSet<TopicEntity> Topics => Set<TopicEntity>();

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
            entity.Property(x => x.Category).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.Property(x => x.IconUrl).HasMaxLength(256);
            entity.Property(x => x.RepositoryUrl).HasMaxLength(256);
            entity.Property(x => x.WebsiteUrl).HasMaxLength(256);
            entity.Property(x => x.TotalDownloads).HasDefaultValue(0L);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.OwnerUserId);
        });

        builder.Entity<PackageVersionEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ManifestJson).IsRequired();
            entity.Property(x => x.ChecksumSha256).IsRequired().HasMaxLength(128);
            entity.Property(x => x.StorageKey).IsRequired().HasMaxLength(512);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(x => x.SizeBytes).IsRequired();
            entity.Property(x => x.IsYanked).HasDefaultValue(false);
            entity.HasIndex(x => new { x.PackageId, x.Version }).IsUnique();
            entity.HasIndex(x => x.PublishedAtUtc);
            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
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

        builder.Entity<PackageCommunityReviewEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Comment).IsRequired().HasMaxLength(1024);
            entity.HasIndex(x => x.PackageId);
            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PackageIssueEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(160);
            entity.Property(x => x.Body).IsRequired().HasMaxLength(3000);
            entity.HasIndex(x => x.PackageId);
            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PackageIssueVoteEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => new { x.IssueId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Issue)
                .WithMany()
                .HasForeignKey(x => x.IssueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserEmailEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(x => new { x.UserId, x.Email }).IsUnique();
            entity.HasIndex(x => x.Email);
        });

        builder.Entity<UserRatingEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.CalculatedScore);
        });

        builder.Entity<BoardEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Slug).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.EntityType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.EntityId).HasMaxLength(128);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        builder.Entity<BoardPostEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Content).IsRequired().HasMaxLength(10000);
            entity.Property(x => x.PostType).HasDefaultValue(BoardPostType.Issue);
            entity.HasIndex(x => x.BoardId);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.BoardId, x.IsPinned, x.CreatedAtUtc });
            entity.HasIndex(x => x.PostType);
            entity.HasOne(x => x.Board)
                .WithMany()
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BoardPostCommentEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Content).IsRequired().HasMaxLength(5000);
            entity.HasIndex(x => x.PostId);
            entity.HasIndex(x => x.ParentCommentId);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasOne(x => x.Post)
                .WithMany()
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ParentComment)
                .WithMany()
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BoardPostVoteEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Post)
                .WithMany()
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BoardCommentVoteEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => new { x.CommentId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Comment)
                .WithMany()
                .HasForeignKey(x => x.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResourcePermissionEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.ResourceType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ResourceId).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Permission).IsRequired().HasMaxLength(64);
            entity.Property(x => x.GrantedByUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => new { x.UserId, x.ResourceType, x.ResourceId });
            entity.HasIndex(x => new { x.ResourceType, x.ResourceId });
        });

        builder.Entity<NotificationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Type).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.DataJson);
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
        });

        builder.Entity<UserNotificationPreferenceEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.Type).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Type }).IsUnique();
        });

        builder.Entity<EmailSettingsEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromEmail).IsRequired().HasMaxLength(256);
            entity.Property(x => x.FromName).IsRequired().HasMaxLength(128);
        });

        builder.Entity<FollowPackageEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.PackageId).IsRequired().HasMaxLength(64);
            entity.HasIndex(x => new { x.UserId, x.PackageId }).IsUnique();
        });

        builder.Entity<FollowPublisherEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            entity.Property(x => x.PublisherUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => new { x.UserId, x.PublisherUserId }).IsUnique();
        });

        builder.Entity<PackageTagEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tag).IsRequired().HasMaxLength(48);
            entity.HasIndex(x => x.PackageId);
            entity.HasIndex(x => x.Tag);
        });

        builder.Entity<TopicEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Slug).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Description).HasMaxLength(768);
            entity.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.BoardId).IsUnique();
        });
    }
}
