namespace Server.Components.Pages;

public partial class Publisher
{
    protected override void OnInitialized()
    {
        Navigation.NavigateTo("/dashboard/packages", forceLoad: true);
    }
}