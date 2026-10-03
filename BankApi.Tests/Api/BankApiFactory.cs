using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankApi.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace BankApi.Tests.Api;

/// <summary>Runs the real application against a throwaway SQLite database file.</summary>
public class BankApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "Admin123!";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"bankapi-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");
        builder.UseSetting("SeedAdmin:Username", AdminUsername);
        builder.UseSetting("SeedAdmin:Password", AdminPassword);
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateAuthorizedClientAsync(AdminUsername, AdminPassword);

    public async Task<HttpClient> CreateOperatorClientAsync()
    {
        var username = $"op_{Guid.NewGuid():N}"[..20];
        const string password = "Operator1!";

        var response = await CreateClient().PostAsJsonAsync("/api/auth/register", new { username, password });
        response.EnsureSuccessStatusCode();

        return await CreateAuthorizedClientAsync(username, password);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var token = await response.ReadAsync<TokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // Pooled connections keep the file locked.
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
