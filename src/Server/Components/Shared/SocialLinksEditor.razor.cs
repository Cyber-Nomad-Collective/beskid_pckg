using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class SocialLinksEditor
{
    [Parameter]
    public List<SocialLinkItem> Links { get; set; } = [];

    [Parameter]
    public EventCallback<List<SocialLinkItem>> LinksChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public int MaxLinks { get; set; } = 5;

    private async Task AddLink()
    {
        if (Links.Count >= MaxLinks) return;

        Links.Add(new SocialLinkItem());
        await NotifyChanged();
    }

    private async Task RemoveLink(int index)
    {
        if (index < 0 || index >= Links.Count) return;

        Links.RemoveAt(index);
        await NotifyChanged();
    }

    private async Task MoveUp(int index)
    {
        if (index <= 0 || index >= Links.Count) return;

        (Links[index], Links[index - 1]) = (Links[index - 1], Links[index]);
        await NotifyChanged();
    }

    private async Task MoveDown(int index)
    {
        if (index < 0 || index >= Links.Count - 1) return;

        (Links[index], Links[index + 1]) = (Links[index + 1], Links[index]);
        await NotifyChanged();
    }

    private async Task OnPlatformChanged(int index, SocialPlatform platform)
    {
        if (index < 0 || index >= Links.Count) return;

        Links[index].Platform = platform;
        await NotifyChanged();
    }

    private async Task OnUrlChanged(int index, string? url)
    {
        if (index < 0 || index >= Links.Count) return;

        Links[index].Url = url?.Trim() ?? string.Empty;
        await NotifyChanged();
    }

    private Task NotifyChanged()
    {
        return LinksChanged.InvokeAsync(Links);
    }
}

public class SocialLinkItem
{
    public SocialPlatform Platform { get; set; } = SocialPlatform.Website;
    public string Url { get; set; } = string.Empty;
}

