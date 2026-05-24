using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace Server.Components.Pages;

public partial class Onboarding
{
    [SupplyParameterFromQuery(Name = "error")]
    public string? ErrorCode { get; set; }

    private string Message = string.Empty;
    private bool Success;

    protected override async Task OnInitializedAsync()
    {
        Message = ErrorCode switch
        {
            "missing_credentials" => "Display name, email, and password are required.",
            "missing_name" => "Display name is required.",
            "password_mismatch" => "Passwords do not match.",
            "create_failed" => "Unable to create the administrator account.",
            _ => string.Empty
        };
        Success = false;

        var hasUsers = await UserManager.Users.AnyAsync();
        if (hasUsers)
        {
            Navigation.NavigateTo("/auth?mode=login", forceLoad: true);
        }
    }
}