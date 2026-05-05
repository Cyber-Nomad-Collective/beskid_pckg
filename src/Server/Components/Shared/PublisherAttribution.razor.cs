using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PublisherAttribution
{
    [Parameter, EditorRequired]
    public string OwnerUserId { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string DisplayName { get; set; } = string.Empty;

    [Parameter]
    public bool IsVerified { get; set; }
}
