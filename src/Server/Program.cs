using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FluentUI.AspNetCore.Components;
using Scalar.AspNetCore;
using Server.Components;
using Server.Components.Account;
using Server.Data;
using Server.Services;
using Server.Hubs;
using Server.Services.Notifications;
using Server.Services.Email;
using Wolverine;
using Wolverine.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using Server.Features.Auth;
using System.Security.Cryptography.X509Certificates;
using GoogleCaptchaComponent;
using GoogleCaptchaComponent.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("search", limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(30);
        limiter.PermitLimit = 120;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("download", limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(30);
        limiter.PermitLimit = 80;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("publish", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 20;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("docs", limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(30);
        limiter.PermitLimit = 120;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var aspireConnection = builder.Configuration.GetConnectionString("pckgdb");
var connectionString = defaultConnection
                       ?? aspireConnection
                       ?? "Host=localhost;Port=5432;Database=pckgdb;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Data Protection backs antiforgery and auth cookies. Default: PostgreSQL so Docker restarts and all replicas share one key ring.
// Set Security:PersistDataProtectionKeysToDatabase=false + Security:DataProtectionKeysPath when swapping EF to InMemory (integration tests).
var persistDpKeysToDatabase = builder.Configuration.GetValue("Security:PersistDataProtectionKeysToDatabase", true);
var dataProtectionBuilder = builder.Services.AddDataProtection().SetApplicationName("pckg");
if (persistDpKeysToDatabase)
{
    dataProtectionBuilder.PersistKeysToDbContext<ApplicationDbContext>();
}
else
{
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

    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var dataProtectionCertificatePath = builder.Configuration["Security:DataProtectionCertificatePath"];
var dataProtectionCertificatePassword = builder.Configuration["Security:DataProtectionCertificatePassword"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    var certificate = string.IsNullOrWhiteSpace(dataProtectionCertificatePassword)
        ? X509CertificateLoader.LoadPkcs12FromFile(dataProtectionCertificatePath, password: null)
        : X509CertificateLoader.LoadPkcs12FromFile(dataProtectionCertificatePath, dataProtectionCertificatePassword);
    dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
}

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
builder.Services.AddScoped<IPackageArtifactExplorerService, PackageArtifactExplorerService>();
builder.Services.AddScoped<IPackageDocsArchiveService, PackageDocsArchiveService>();
builder.Services.AddScoped<IPackageSourceFileTypeMapper, PackageSourceFileTypeMapper>();
builder.Services.AddScoped<IPackageSourceArchiveService, PackageSourceArchiveService>();
builder.Services.AddSingleton<IPckgRegistryActivityLog, PckgRegistryActivityLog>();
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
builder.Services.AddScoped<Server.Services.IAuthorizationService, Server.Services.AuthorizationService>();
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection(CaptchaOptions.SectionName));
var captchaBootstrap = builder.Configuration.GetSection(CaptchaOptions.SectionName).Get<CaptchaOptions>() ?? new();
builder.Services.AddGoogleCaptcha(c =>
{
    c.V3SiteKey = captchaBootstrap.RecaptchaV3SiteKey ?? string.Empty;
    c.DefaultVersion = CaptchaConfiguration.Version.V3;
    c.DefaultTheme = CaptchaConfiguration.Theme.Light;
});
builder.Services.AddHttpClient(CaptchaVerificationService.RecaptchaEnterpriseHttpClientName, client =>
{
    client.BaseAddress = new Uri("https://recaptchaenterprise.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<ICaptchaVerificationService, CaptchaVerificationService>();
builder.Services.AddScoped<ILinkContentGuard, LinkContentGuard>();
builder.Services.AddScoped<Server.Services.IUserRatingService, Server.Services.UserRatingService>();
builder.Services.AddSingleton<Server.Services.IMarkdownService, Server.Services.MarkdownService>();
builder.Services.AddSingleton<IHtmlSanitizationService, HtmlSanitizationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Prefer Wolverine transport for notifications broadcast
builder.Services.AddScoped<INotificationBroadcaster, WolverineNotificationBroadcaster>();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<INotificationActionHandler, DefaultNotificationActionHandler>();

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.CombineLogs = true;
});

// Wolverine messaging with SignalR transport
builder.UseWolverine(opts =>
{
    // Wire in SignalR transport for websocket/browser messaging
    opts.UseSignalR(o =>
    {
        // Reasonable default; can be tuned
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });

    // Publish our notification message type to SignalR
    opts.Publish(x =>
    {
        x.Message<NotificationPushed>().ToSignalR();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseHttpLogging();

using (var scope = app.Services.CreateScope())
{
    var migrations = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
    await migrations.ApplyAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Moderator"))
    {
        await roleManager.CreateAsync(new IdentityRole("Moderator"));
    }
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
app.UseRateLimiter();

// Redirect to onboarding if no users exist.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var pathValue = path.HasValue ? path.Value! : string.Empty;
    // HttpClient + base address combinations can produce paths like "//api/..." which must still bypass.
    while (pathValue.Length > 1 && pathValue[0] == '/' && pathValue[1] == '/')
    {
        pathValue = pathValue[1..];
    }

    if (pathValue.Length > 0 && pathValue[0] != '/')
    {
        pathValue = "/" + pathValue;
    }

    var isBypassedPath =
        pathValue.StartsWith("/onboarding", StringComparison.OrdinalIgnoreCase) ||
        pathValue.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        pathValue.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) ||
        pathValue.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        pathValue.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        pathValue.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
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

// System email flows (email confirmation + password reset)
app.MapPost("/api/auth/request-email-confirmation",
    async (HttpContext http,
           UserManager<ApplicationUser> userManager,
           IEmailSender emailSender,
           IEmailTemplateService templater,
           CancellationToken ct) =>
    {
        var user = await userManager.GetUserAsync(http.User);
        if (user is null) return Results.Unauthorized();

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var url = $"{http.Request.Scheme}://{http.Request.Host}/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var body = templater.Render("Confirm your email",
            $"<p>Please confirm your email by clicking the link below:</p><p><a href=\"{url}\">Confirm email</a></p>");
        await emailSender.SendAsync(user.Id, "Confirm your email", body, ct);

        return Results.Ok(new { ok = true });
    })
    .RequireAuthorization();

app.MapGet("/api/auth/confirm-email",
    async (HttpContext http,
           string userId,
           string token,
           UserManager<ApplicationUser> userManager) =>
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.BadRequest("Invalid user.");

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return Results.BadRequest("Invalid token.");

        return Results.Redirect("/auth?confirmed=1");
    })
    .AllowAnonymous();

app.MapPost("/api/auth/request-password-reset",
    async (HttpContext http,
           RequestPasswordResetRequest req,
           UserManager<ApplicationUser> userManager,
           IEmailSender emailSender,
           IEmailTemplateService templater,
           CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return Results.Ok(new { ok = true });
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null) return Results.Ok(new { ok = true });

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var url = $"{http.Request.Scheme}://{http.Request.Host}/auth?mode=reset&userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var body = templater.Render("Reset your password",
            $"<p>You requested a password reset. Click the link below to set a new password:</p><p><a href=\"{url}\">Reset password</a></p>");
        await emailSender.SendAsync(user.Id, "Reset your password", body, ct);

        return Results.Ok(new { ok = true });
    })
    .AllowAnonymous();

app.MapPost("/api/auth/reset-password",
    async (ResetPasswordRequest req,
           UserManager<ApplicationUser> userManager) =>
    {
        var user = await userManager.FindByIdAsync(req.UserId);
        if (user is null) return Results.BadRequest("Invalid user.");
        var result = await userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(";", result.Errors.Select(e => e.Description));
            return Results.BadRequest(errors);
        }
        return Results.Ok(new { ok = true });
    })
    .AllowAnonymous();

app.MapHub<NotificationsHub>("/hubs/notifications");
// Default Wolverine SignalR hub for WebSocket messages
app.MapWolverineSignalRHub("/api/messages");
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
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) =>
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

    if (!await roleManager.RoleExistsAsync("User"))
    {
        await roleManager.CreateAsync(new IdentityRole("User"));
    }

    await userManager.AddToRoleAsync(user, "User");

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
    var displayName = form["displayName"].FirstOrDefault()?.Trim() ?? string.Empty;
    var email = form["email"].FirstOrDefault()?.Trim() ?? string.Empty;
    var password = form["password"].FirstOrDefault() ?? string.Empty;
    var confirmPassword = form["confirmPassword"].FirstOrDefault() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(displayName))
    {
        return Results.Redirect("/onboarding?error=missing_name");
    }

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
        Email = email,
        EmailConfirmed = true,
        DisplayName = displayName
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
