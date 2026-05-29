using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Features.Auth;

namespace Server.Components.Pages;

public partial class Auth
{
    [SupplyParameterFromQuery(Name = "mode")]
    public string? RequestedMode { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? ErrorCode { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    private string LoginMessage = string.Empty;
    private bool LoginSuccess;
    private string RegisterMessage = string.Empty;
    private bool RegisterSuccess;
    private bool IsLoginMode => !string.Equals(RequestedMode, "register", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        if (HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var destination = AuthRedirectHelper.SanitizeReturnUrl(ReturnUrl) ?? "/dashboard/packages";
            Navigation.NavigateTo(destination, forceLoad: true);
            return;
        }

        var hasUsers = await UserManager.Users.AnyAsync();
        if (!hasUsers)
        {
            Navigation.NavigateTo("/onboarding", forceLoad: true);
        }

        LoginMessage = ErrorCode switch
        {
            "missing_credentials" => "Email and password are required.",
            "invalid_credentials" => "Invalid credentials.",
            "invalid_request" => "Invalid login request.",
            _ => string.Empty
        };
        LoginSuccess = false;

        RegisterMessage = ErrorCode switch
        {
            "register_missing_credentials" => "Email and password are required.",
            "register_password_mismatch" => "Passwords do not match.",
            "register_create_failed" => "Unable to create account.",
            _ => string.Empty
        };
        RegisterSuccess = false;
    }
}