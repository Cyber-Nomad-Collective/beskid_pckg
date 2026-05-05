using Microsoft.Extensions.Configuration;

namespace Server.Configuration;

/// <summary>
/// Resolves the PostgreSQL connection string from configuration.
/// Precedence: ConnectionStrings:DefaultConnection (compose/Coolify usually set this), ConnectionStrings:pckgdb (Aspire),
/// then discrete Pckg:Database:* when host is set, then local dev default.
/// </summary>
public static class ConnectionStringResolver
{
    public static string ResolveDefaultConnection(IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            return defaultConnection;
        }

        var aspireConnection = configuration.GetConnectionString("pckgdb");
        if (!string.IsNullOrWhiteSpace(aspireConnection))
        {
            return aspireConnection;
        }

        var db = configuration.GetSection("Pckg:Database");
        var host = db["Host"];
        if (!string.IsNullOrWhiteSpace(host))
        {
            var port = string.IsNullOrWhiteSpace(db["Port"]) ? "5432" : db["Port"]!;
            var name = string.IsNullOrWhiteSpace(db["Name"]) ? "pckgdb" : db["Name"]!;
            var username = string.IsNullOrWhiteSpace(db["Username"]) ? "postgres" : db["Username"]!;
            var password = db["Password"] ?? string.Empty;
            return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
        }

        return "Host=localhost;Port=5432;Database=pckgdb;Username=postgres;Password=postgres";
    }
}
