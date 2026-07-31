using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Admin;

public sealed class ListUsersEndpoint : EndpointWithoutRequest
{
    public UserManager<ApplicationUser> UserManager { get; set; } = default!;
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/admin/users");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var hasPagination = HttpContext.Request.Query.ContainsKey("page")
            || HttpContext.Request.Query.ContainsKey("pageSize")
            || HttpContext.Request.Query.ContainsKey("search");
        var page = Query<int>("page", isRequired: false);
        var pageSize = Query<int>("pageSize", isRequired: false);
        var search = Query<string>("search", isRequired: false);

        if (page <= 0) page = 1;
        if (!hasPagination)
        {
            pageSize = int.MaxValue;
        }
        else if (pageSize <= 0 || pageSize > 100)
        {
            pageSize = 50;
        }

        var query = UserManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => 
                u.Email!.Contains(search) || 
                u.DisplayName.Contains(search) || 
                u.UserName!.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var skip = (page - 1) * pageSize;

        var users = await query
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await UserManager.GetRolesAsync(user);
            var rating = await Db.UserRatings
                .Where(r => r.UserId == user.Id)
                .Select(r => r.CalculatedScore)
                .FirstOrDefaultAsync(ct);

            userDtos.Add(new UserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.EmailConfirmed,
                user.IsPublisherVerified,
                roles.ToList(),
                rating
            ));
        }

        // The legacy Blazor screen uses explicit pagination while the React dashboard
        // requests the collection without paging. Keep one Identity query and project it
        // into the contract selected by the request shape.
        if (hasPagination)
        {
            await Send.OkAsync(new ListUsersResponse(userDtos, totalCount, page, pageSize), ct);
            return;
        }

        var reactUsers = userDtos.Select(user => new ReactAdminUser(
            user.Id,
            user.Email,
            user.Roles,
            user.IsPublisherVerified)).ToList();
        await Send.OkAsync(reactUsers, ct);
    }
}
