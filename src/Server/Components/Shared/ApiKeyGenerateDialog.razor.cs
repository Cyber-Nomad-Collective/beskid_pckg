using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class ApiKeyGenerateDialog : IDialogContentComponent<ApiKeyGenerateDialog.CreateKeyInput>
{
    [Parameter] public CreateKeyInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;

    private Task CancelAsync() => Dialog.CancelAsync();

    private Task SubmitAsync() => Dialog.CloseAsync(Content);

    public sealed class CreateKeyInput
    {
        public string Name { get; set; } = string.Empty;
        public bool PublishScope { get; set; } = true;
        public bool ReadScope { get; set; } = true;
    }
}
