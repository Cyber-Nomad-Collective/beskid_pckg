using Microsoft.AspNetCore.Components;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminRegistryActivity : IAsyncDisposable
{
    private List<RegistryActivityRowDto> RegistryActivity = [];
    private bool IsLoadingRegistryActivity;
    private string? RegistryActivityError;
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoopTask;

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    [Inject]
    public ILogger<AdminRegistryActivity> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistryActivityAsync();
        _pollCts = new CancellationTokenSource();
        _pollLoopTask = RunPollLoopAsync(_pollCts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
            _pollCts.Dispose();
        }

        if (_pollLoopTask is not null)
        {
            try
            {
                await _pollLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunPollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(PollRegistryActivityAsync);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PollRegistryActivityAsync()
    {
        try
        {
            await LoadRegistryActivityAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Registry activity poll failed");
            RegistryActivityError = "Unable to refresh registry activity.";
        }
    }

    private Task RefreshRegistryActivityAsync() => LoadRegistryActivityAsync();

    private async Task LoadRegistryActivityAsync()
    {
        IsLoadingRegistryActivity = true;
        RegistryActivityError = null;

        try
        {
            var response = await ApiHttp.GetAsync("/api/admin/registry-activity?take=200");
            if (!response.IsSuccessStatusCode)
            {
                RegistryActivityError = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                        => "You do not have permission to view registry activity.",
                    _ => $"Registry activity API returned {(int)response.StatusCode}.",
                };
                return;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<RegistryActivityRowDto>>();
            RegistryActivity = rows ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load registry activity");
            RegistryActivityError = "Unable to load registry activity.";
        }
        finally
        {
            IsLoadingRegistryActivity = false;
        }
    }
}
