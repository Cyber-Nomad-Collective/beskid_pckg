using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Users;

/// <summary>React projections of the existing user and package ownership records.</summary>
public sealed class ListReactPublishersEndpoint(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    : EndpointWithoutRequest<List<ReactCommunityProfile>>
{
    public override void Configure()
    {
        Get("/publishers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ownerIds = await db.Packages.AsNoTracking()
            .Where(package => package.IsPublic)
            .Select(package => package.OwnerUserId)
            .Distinct()
            .ToListAsync(ct);
        var users = await userManager.Users.AsNoTracking()
            .Where(user => ownerIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.UserName)
            .ToListAsync(ct);
        await Send.OkAsync(users.Select(ReactCommunityProfile.FromUser).ToList(), ct);
    }
}

public sealed class ListReactPublisherPackagesEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<List<ReactPublisherPackage>>
{
    public override void Configure()
    {
        Get("/publishers/{subject}/packages");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var subject = Route<string>("subject");
        if (string.IsNullOrWhiteSpace(subject))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var packages = await db.Packages.AsNoTracking()
            .Where(package => package.IsPublic && package.OwnerUserId == subject)
            .OrderByDescending(package => package.UpdatedAtUtc)
            .Select(package => new ReactPublisherPackage(
                package.Id.ToString(), package.Name, package.Description, package.Category,
                package.TotalDownloads, package.UpdatedAtUtc))
            .ToListAsync(ct);
        await Send.OkAsync(packages, ct);
    }
}

public sealed class GetReactCommunityProfileEndpoint(UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest<ReactCommunityProfile>
{
    public override void Configure()
    {
        Get("/community/profiles/{subject}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var subject = Route<string>("subject");
        var user = string.IsNullOrWhiteSpace(subject) ? null : await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(ReactCommunityProfile.FromUser(user), ct);
    }
}

public sealed record ReactCommunityProfile(string Subject, string DisplayName, string Bio, IReadOnlyList<string> SocialLinks)
{
    public static ReactCommunityProfile FromUser(ApplicationUser user) => new(
        user.Id,
        string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? user.Id : user.DisplayName,
        user.Bio,
        ProfileSocialLinks.FromUser(user).Select(link => link.Url).ToList());
}

public sealed record ReactPublisherPackage(string Id, string Name, string Description, string Category, long TotalDownloads, DateTimeOffset UpdatedAtUtc);
