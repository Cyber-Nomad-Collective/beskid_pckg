using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class ApiKeyGenerateDialog
{
    private readonly CreateKeyInput Model = new();
    private bool IsHidden = true;

    [Parameter] public bool IsWorking { get; set; }
    [Parameter] public string? LastCreatedKey { get; set; }
    [Parameter] public EventCallback<CreateKeyInput> OnGenerate { get; set; }
    [Parameter] public EventCallback OnCopyKey { get; set; }
    [Parameter] public EventCallback OnDismissKey { get; set; }

    public async Task OpenDialogAsync()
    {
        Model.Name = string.Empty;
        Model.PublishScope = true;
        Model.ReadScope = true;
        IsHidden = false;
        await OnDismissKey.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    public async Task CloseDialogAsync()
    {
        IsHidden = true;
        await OnDismissKey.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private Task CloseAsync() => CloseDialogAsync();

    private Task SubmitAsync() => OnGenerate.InvokeAsync(Model);

    private Task DismissKey() => OnDismissKey.InvokeAsync();

    public sealed class CreateKeyInput
    {
        public string Name { get; set; } = string.Empty;
        public bool PublishScope { get; set; } = true;
        public bool ReadScope { get; set; } = true;
    }
}
