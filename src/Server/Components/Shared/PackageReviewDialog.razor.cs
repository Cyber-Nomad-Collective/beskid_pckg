using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageReviewDialog : IDialogContentComponent<PackageReviewDialog.ReviewInput>
{
    [Parameter] public ReviewInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;

    private Task CancelAsync() => Dialog.CancelAsync();

    private Task SubmitAsync()
    {
        Content.Comment = Content.Comment.Trim();
        Content.Rating = Math.Clamp(Content.Rating, 1, 5);
        return Dialog.CloseAsync(Content);
    }

    private Task OnRatingChanged(int value)
    {
        Content.Rating = Math.Clamp(value, 1, 5);
        return Task.CompletedTask;
    }

    public sealed record ReviewInput
    {
        public string PackageName { get; init; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
    }
}
