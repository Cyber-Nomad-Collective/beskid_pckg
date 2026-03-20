using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FluentUI.AspNetCore.Components;
using Scalar.AspNetCore;
using Server.Components;
using Server.Components.Account;
using Server.Data;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

var internalApiBaseAddress = builder.Configuration["HttpClient:InternalBaseAddress"];
if (string.IsNullOrWhiteSpace(internalApiBaseAddress))
{
    var urlsSetting = builder.Configuration["ASPNETCORE_URLS"]
        ?? builder.Configuration["URLS"];
    internalApiBaseAddress = ResolveInternalBaseAddress(urlsSetting) ?? "http://127.0.0.1:8080";
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor.HttpContext;

    var client = new HttpClient
    {
        BaseAddress = new Uri(internalApiBaseAddress)
    };

    if (httpContext is not null)
    {
        if (httpContext.Request.Headers.TryGetValue("Cookie", out var cookie)
            && !string.IsNullOrWhiteSpace(cookie))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie.ToString());
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        if (httpContext.Request.Headers.TryGetValue("X-API-Key", out var apiKey)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey.ToString());
        }
    }

    return client;
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddMemoryCache();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "Beskid Package Registry API";
        s.Version = "v1";
    };
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var dataProtectionKeysPath = builder.Configuration["Security:DataProtectionKeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "data", ".data-protection-keys");
}
try
{
    Directory.CreateDirectory(dataProtectionKeysPath);
}
catch (UnauthorizedAccessException)
{
    dataProtectionKeysPath = Path.Combine(Path.GetTempPath(), "pckg-dpkeys");
    Directory.CreateDirectory(dataProtectionKeysPath);
}
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("pckg");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=pckg.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "SmartAuth";
        options.DefaultChallengeScheme = "SmartAuth";
    })
    .AddPolicyScheme("SmartAuth", "Smart authentication selector", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader)
                && !string.IsNullOrWhiteSpace(apiKeyHeader.FirstOrDefault()))
            {
                return ApiKeyAuthenticationDefaults.Scheme;
            }

            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var header = authHeader.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = header["Bearer ".Length..].Trim();
                    if (token.StartsWith("bpk_", StringComparison.OrdinalIgnoreCase))
                    {
                        return ApiKeyAuthenticationDefaults.Scheme;
                    }

                    return IdentityConstants.BearerScheme;
                }
            }

            return IdentityConstants.ApplicationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<ApiKeyEntity>, PasswordHasher<ApiKeyEntity>>();
builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddScoped<IApiKeyManagementService, ApiKeyManagementService>();
builder.Services.AddScoped<IApiPrincipalResolver, ApiPrincipalResolver>();
builder.Services.AddSingleton<IPackageArtifactStore, PackageArtifactStore>();
builder.Services.AddSingleton<IPackageArtifactValidator, PackageArtifactValidator>();
builder.Services.AddScoped<IStartupSeeder, StartupSeeder>();
builder.Services.AddScoped<Server.Services.IAuthorizationService, Server.Services.AuthorizationService>();
builder.Services.AddScoped<Server.Services.IUserRatingService, Server.Services.UserRatingService>();
builder.Services.AddSingleton<Server.Services.IMarkdownService, Server.Services.MarkdownService>();

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.CombineLogs = true;
});

var app = builder.Build();

app.UseHttpLogging();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IStartupSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
var useHttpsRedirection = builder.Configuration.GetValue<bool?>("Security:UseHttpsRedirection") ?? false;
if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}

var uploadsRoot = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

// Redirect to onboarding if no users exist.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isBypassedPath =
        path.StartsWithSegments("/onboarding", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) ||
        Path.HasExtension(path);

    if (isBypassedPath)
    {
        await next();
        return;
    }

    var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
    var hasUsers = await dbContext.Users.AsNoTracking().AnyAsync(context.RequestAborted);
    if (!hasUsers)
    {
        context.Response.Redirect("/onboarding");
        return;
    }

    await next();
});
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api");

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
    app.MapScalarApiReference("/scalar", o =>
    {
        o.OpenApiRoutePattern = "/swagger/v1/swagger.json";
    });
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>().DisableAntiforgery();
app.MapPost("/auth/login", async (
    HttpContext context,
    SignInManager<ApplicationUser> signInManager) =>
{
    if (!context.Request.HasFormContentType)
    {
        return Results.Redirect("/auth?mode=login&error=invalid_request");
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var email = form["email"].FirstOrDefault()?.Trim() ?? string.Empty;
    var password = form["password"].FirstOrDefault() ?? string.Empty;
    var rememberMe = string.Equals(form["rememberMe"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(form["rememberMe"].FirstOrDefault(), "on", StringComparison.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/auth?mode=login&error=missing_credentials");
    }

    var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
    if (!result.Succeeded)
    {
        return Results.Redirect("/auth?mode=login&error=invalid_credentials");
    }

    return Results.Redirect("/dashboard/packages/my");
}).DisableAntiforgery();
app.MapPost("/auth/register", async (
    HttpContext context,
    UserManager<ApplicationUser> userManager) =>
{
    if (!context.Request.HasFormContentType)
    {
        return Results.Redirect("/auth?mode=register&error=invalid_request");
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var email = form["email"].FirstOrDefault()?.Trim() ?? string.Empty;
    var password = form["password"].FirstOrDefault() ?? string.Empty;
    var confirmPassword = form["confirmPassword"].FirstOrDefault() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/auth?mode=register&error=register_missing_credentials");
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
        return Results.Redirect("/auth?mode=register&error=register_password_mismatch");
    }

    var user = new ApplicationUser
    {
        UserName = email,
        Email = email
    };

    var createResult = await userManager.CreateAsync(user, password);
    if (!createResult.Succeeded)
    {
        return Results.Redirect("/auth?mode=register&error=register_create_failed");
    }

    return Results.Redirect("/auth?mode=login");
}).DisableAntiforgery();
app.MapPost("/onboarding/create", async (
    HttpContext context,
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) =>
{
    var hasUsers = await dbContext.Users.AsNoTracking().AnyAsync(context.RequestAborted);
    if (hasUsers)
    {
        return Results.Redirect("/auth?mode=login");
    }

    if (!context.Request.HasFormContentType)
    {
        return Results.Redirect("/onboarding?error=missing_credentials");
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var email = form["email"].FirstOrDefault()?.Trim() ?? string.Empty;
    var password = form["password"].FirstOrDefault() ?? string.Empty;
    var confirmPassword = form["confirmPassword"].FirstOrDefault() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/onboarding?error=missing_credentials");
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
        return Results.Redirect("/onboarding?error=password_mismatch");
    }

    var user = new ApplicationUser
    {
        UserName = email,
        Email = email
    };

    var createResult = await userManager.CreateAsync(user, password);
    if (!createResult.Succeeded)
    {
        return Results.Redirect("/onboarding?error=create_failed");
    }

    if (!await roleManager.RoleExistsAsync("SuperAdmin"))
    {
        await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));
    }

    await userManager.AddToRoleAsync(user, "SuperAdmin");
    return Results.Redirect("/auth?mode=login");
}).DisableAntiforgery();
app.MapGet("/users/logout", (HttpContext context) =>
{
    var redirectTarget = context.Request.QueryString.HasValue
        ? $"/api/users/logout{context.Request.QueryString.Value}"
        : "/api/users/logout";
    return Results.Redirect(redirectTarget);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .DisableAntiforgery();

app.Run();

static string? ResolveInternalBaseAddress(string? urlsSetting)
{
    if (string.IsNullOrWhiteSpace(urlsSetting))
    {
        return null;
    }

    var firstUrl = urlsSetting
        .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(firstUrl))
    {
        return null;
    }

    var normalizedUrl = firstUrl
        .Replace("://+:", "://127.0.0.1:", StringComparison.Ordinal)
        .Replace("://*:", "://127.0.0.1:", StringComparison.Ordinal);

    if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsed))
    {
        return null;
    }

    var host = parsed.Host is "0.0.0.0" or "[::]" or "::" or "+"
        ? "127.0.0.1"
        : parsed.Host;

    var port = parsed.IsDefaultPort
        ? (string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
        : parsed.Port;

    return $"{parsed.Scheme}://{host}:{port}";
}

namespace Server
{
    public partial class Program;
}
