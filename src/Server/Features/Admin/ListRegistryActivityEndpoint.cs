using FastEndpoints;
using Server.Services;

namespace Server.Features.Admin;

public sealed class ListRegistryActivityEndpoint(IPckgRegistryActivityLog activityLog)
    : EndpointWithoutRequest<List<RegistryActivityEntry>>
{
    public override void Configure()
    {
        Get("/admin/registry-activity");
        Roles("SuperAdmin");
        Summary(s => s.Summary = "Recent package registry activity (publish, upsert, yank, etc.).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var take = Query<int>("take", isRequired: false);
        if (take <= 0 || take > 500)
        {
            take = 200;
        }

        var items = activityLog.GetRecent(take);
        await Send.OkAsync(items.ToList(), ct);
    }
}
