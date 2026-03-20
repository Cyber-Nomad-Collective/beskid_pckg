using Microsoft.AspNetCore.Components;

namespace Server.Components.Layout;

public partial class AppTopBar
{
    [Parameter] public RenderFragment? HeaderStart { get; set; }
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private bool IsNotificationCenterOpen;
    private int NotificationUnreadCount;

    private void ToggleNotificationCenter()
    {
        IsNotificationCenterOpen = !IsNotificationCenterOpen;
    }

    private void CloseNotificationCenter()
    {
        IsNotificationCenterOpen = false;
    }

    private void OnNotificationUnreadCountChanged(int unreadCount)
    {
        NotificationUnreadCount = unreadCount;
    }
}