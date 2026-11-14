using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ServiceDesk.Api.Contracts;

namespace ServiceDesk.Tests.Integration;

/// <summary>Boots the real API against a throwaway SQLite database file.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"servicedesk-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
            ["Database:Provider"] = "Sqlite",
            ["Database:Seed"] = "true",
            ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef"
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { /* best effort cleanup */ }
    }
}

public static class TestApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string AdminEmail = "admin@servicedesk.dev";
    public const string AgentEmail = "agent@servicedesk.dev";
    public const string StaffPassword = "Agent@12345";

    public static async Task<HttpClient> LoginAsync(this ApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public static Task<HttpClient> LoginAsAgentAsync(this ApiFactory factory) =>
        factory.LoginAsync(AgentEmail, StaffPassword);

    /// <summary>Registers a brand-new customer so tests do not depend on each other.</summary>
    public static async Task<(HttpClient Client, UserDto User)> NewCustomerAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"customer-{Guid.NewGuid():N}@test.dev";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Test Customer", email, password = "Password@123" }, Json);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth.User);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
