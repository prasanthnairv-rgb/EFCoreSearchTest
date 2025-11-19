using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EFCoreTest.Extensions;

public static class HostExtensions
{
    /// <summary>
    /// Runs an async seeding action using a scoped service provider. (Simulate ef core 9's host seeding)
    /// Returns the original host to allow fluent usage.
    /// </summary>
    public static async Task<IHost> UseAsyncSeeding(this IHost host, Func<IServiceProvider, Task> seeder)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(seeder);

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        var logger = services.GetService<ILogger<IHost>>() ?? host.Services.GetService<ILogger<IHost>>();

        try
        {
            await seeder(services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while running async seeding.");
            throw;
        }

        return host;
    }
}
