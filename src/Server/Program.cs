using Microsoft.FluentUI.AspNetCore.Components;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using pckg.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Server.Components;
using Server.Components.Account;
using Server.Components.Layout;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var baseUri = new Uri($"{httpContextAccessor.HttpContext?.Request.Scheme}://{httpContextAccessor.HttpContext?.Request.Host}");
    return new HttpClient { BaseAddress = baseUri };
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
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys");
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

            return context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                ? IdentityConstants.BearerScheme
                : IdentityConstants.ApplicationScheme;
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

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseAntiforgery());
app.UseAuthentication();
app.UseAuthorization();
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
app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
