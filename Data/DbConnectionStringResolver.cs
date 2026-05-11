using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MyAspNetApp.Data;

internal static class DbConnectionStringResolver
{
    private const string DefaultConnectionName = "DefaultConnection";

    public static string ResolveRequiredConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString(DefaultConnectionName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var jsonConfiguration = new ConfigurationBuilder()
            .SetBasePath(environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
            .Build();

        connectionString = jsonConfiguration.GetConnectionString(DefaultConnectionName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            $"Missing or empty connection string: {DefaultConnectionName}. Check configuration sources and environment overrides.");
    }
}
