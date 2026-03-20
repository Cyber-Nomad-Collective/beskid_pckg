using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
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
            "missing_credentials" => "Email and password are required.",
            "password_mismatch" => "Passwords do not match.",
            "create_failed" => "Unable to create SuperAdmin account.",
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