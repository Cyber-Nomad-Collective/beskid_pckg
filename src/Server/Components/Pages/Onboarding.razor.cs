using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Components.Pages;

public partial class Onboarding
{
    [SupplyParameterFromForm(FormName = "onboarding")]
    private OnboardingFormModel? PostedModel { get; set; }

    private OnboardingFormModel Model => PostedModel ??= new();
    private string Message = string.Empty;
    private bool Success;

    protected override async Task OnInitializedAsync()
    {
        var hasUsers = await UserManager.Users.AnyAsync();
        if (hasUsers)
        {
            Navigation.NavigateTo("/auth?mode=login", forceLoad: true);
        }
    }

    private async Task CreateInitialAdminAsync()
    {
        Message = string.Empty;
        Success = false;

        if (string.IsNullOrWhiteSpace(Model.Email) || string.IsNullOrWhiteSpace(Model.Password))
        {
            Message = "Email and password are required.";
            return;
        }

        if (Model.Password != Model.ConfirmPassword)
        {
            Message = "Passwords do not match.";
            return;
        }

        var user = new ApplicationUser { UserName = Model.Email.Trim(), Email = Model.Email.Trim() };
        var createResult = await UserManager.CreateAsync(user, Model.Password);

        if (!createResult.Succeeded)
        {
            Message = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return;
        }

        if (!await RoleManager.RoleExistsAsync("SuperAdmin"))
        {
            await RoleManager.CreateAsync(new IdentityRole("SuperAdmin"));
        }

        await UserManager.AddToRoleAsync(user, "SuperAdmin");

        Success = true;
        Message = "SuperAdmin created. Please sign in.";
        Navigation.NavigateTo("/auth?mode=login", forceLoad: true);
    }

    private sealed class OnboardingFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}