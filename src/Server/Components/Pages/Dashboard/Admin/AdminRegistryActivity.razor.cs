using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminRegistryActivity : IDisposable
{
    private List<RegistryActivityRowDto> RegistryActivity = [];
    private bool IsLoadingRegistryActivity;
    private System.Threading.Timer? _registryActivityTimer;

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistryActivityAsync();
        _registryActivityTimer = new System.Threading.Timer(
            _ => _ = InvokeAsync(PollRegistryActivityAsync),
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3));
    }

    public void Dispose() => _registryActivityTimer?.Dispose();

    private async Task PollRegistryActivityAsync()
    {
        await LoadRegistryActivityAsync();
        StateHasChanged();
    }

    private Task RefreshRegistryActivityAsync() => LoadRegistryActivityAsync();

    private async Task LoadRegistryActivityAsync()
    {
        IsLoadingRegistryActivity = true;
        try
        {
            var response = await ApiHttp.GetAsync("/api/admin/registry-activity?take=200");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<RegistryActivityRowDto>>();
            RegistryActivity = rows ?? [];
        }
        finally
        {
            IsLoadingRegistryActivity = false;
        }
    }
}
