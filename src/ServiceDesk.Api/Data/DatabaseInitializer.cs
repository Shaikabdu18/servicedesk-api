using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Sla;

namespace ServiceDesk.Api.Data;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    /// <summary>
    /// Applies migrations (SQL Server) or creates the schema (SQLite), then seeds demo data.
    /// Retries on SQL Server because the database container can start slower than the API.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (db.Database.IsSqlServer())
                {
                    await db.Database.MigrateAsync(ct);
                }
                else
                {
                    await db.Database.EnsureCreatedAsync(ct);
                }

                break;
            }
            catch (Exception ex) when (attempt < MaxAttempts && db.Database.IsSqlServer())
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{Max}). Retrying in 5 seconds.", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        if (options.Seed)
        {
            await DbSeeder.SeedAsync(
                db,
                sp.GetRequiredService<IPasswordHasher<User>>(),
                sp.GetRequiredService<ISlaCalculator>(),
                sp.GetRequiredService<TimeProvider>(),
                ct);
        }
    }
}
