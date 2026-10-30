using System.Net.Http.Headers;
using System.Net.Http.Json;
using flash_cards_multiple_api.Data;
using flash_cards_multiple_api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace flash_cards_multiple_api.Tests;

/// <summary>
/// Boots the real app against a private in-memory SQLite database built from the real migrations
/// (so the seeded sample series exist, as in production).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Per-IP limit on register/login. High by default so the suite is never throttled.</summary>
    public int AuthPermitLimit { get; init; } = 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting is visible to the eager config reads in Program.cs, and wins over user-secrets.
        builder.UseSetting("Jwt:Key", "test-signing-key-test-signing-key-0123456789");
        builder.UseSetting("RateLimit:AuthPermitLimit", AuthPermitLimit.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            _connection.Open(); // stays open for the factory's life, or the in-memory database vanishes
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    /// <summary>Registers a fresh user and returns a client already carrying their bearer token.</summary>
    public async Task<(HttpClient Client, CurrentUser User)> CreateUserClientAsync(string password = "passw0rd1")
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var anon = CreateClient();

        var register = await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Tester", password));
        register.EnsureSuccessStatusCode();
        var user = (await register.Content.ReadFromJsonAsync<CurrentUser>())!;

        var login = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }
}
