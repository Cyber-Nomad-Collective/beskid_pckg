using Microsoft.AspNetCore.Components;

namespace Server.Components.Layout;

public partial class AppTopBar
{
    [Parameter] public RenderFragment? HeaderStart { get; set; }
}