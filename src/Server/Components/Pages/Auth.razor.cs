using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Components.Pages;

public partial class Auth
{
    [SupplyParameterFromQuery(Name = "mode")]
    public string? RequestedMode { get; set; }

    [SupplyParameterFromForm(FormName = "login")]
    private LoginFormModel? PostedLoginModel { get; set; }

    private LoginFormModel LoginModel => PostedLoginModel ??= new();

    [SupplyParameterFromForm(FormName = "register")]
    private RegisterFormModel? PostedRegisterModel { get; set; }

    private RegisterFormModel RegisterModel => PostedRegisterModel ??= new();
    private string LoginMessage = string.Empty;
    private bool LoginSuccess;
    private string RegisterMessage = string.Empty;
    private bool RegisterSuccess;
    private bool IsLoginMode => !string.Equals(RequestedMode, "register", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        if (HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo("/dashboard/packages", forceLoad: true);
            return;
        }

        var hasUsers = await UserManager.Users.AnyAsync();
        if (!hasUsers)
        {
            Navigation.NavigateTo("/onboarding", forceLoad: true);
        }
    }

    private async Task LoginAsync()
    {
        LoginMessage = string.Empty;
        LoginSuccess = false;

        if (string.IsNullOrWhiteSpace(LoginModel.Email) || string.IsNullOrWhiteSpace(LoginModel.Password))
        {
            LoginMessage = "Email and password are required.";
            return;
        }

        var result = await SignInManager.PasswordSignInAsync(
            LoginModel.Email.Trim(), LoginModel.Password, LoginModel.RememberMe, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            LoginMessage = "Invalid credentials.";
            return;
        }

        Navigation.NavigateTo("/dashboard/packages/my", forceLoad: true);
    }

    private async Task RegisterAsync()
    {
        RegisterMessage = string.Empty;
        RegisterSuccess = false;

        if (string.IsNullOrWhiteSpace(RegisterModel.Email) || string.IsNullOrWhiteSpace(RegisterModel.Password))
        {
            RegisterMessage = "Email and password are required.";
            return;
        }

        if (RegisterModel.Password != RegisterModel.ConfirmPassword)
        {
            RegisterMessage = "Passwords do not match.";
            return;
        }

        var user = new ApplicationUser { UserName = RegisterModel.Email.Trim(), Email = RegisterModel.Email.Trim() };
        var createResult = await UserManager.CreateAsync(user, RegisterModel.Password);

        if (!createResult.Succeeded)
        {
            RegisterMessage = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return;
        }

        RegisterSuccess = true;
        RegisterMessage = "Account created. You can now sign in.";
        Navigation.NavigateTo("/auth?mode=login", forceLoad: true);
    }

    private sealed class LoginFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    private sealed class RegisterFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}