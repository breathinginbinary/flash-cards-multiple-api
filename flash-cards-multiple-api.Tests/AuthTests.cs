using System.Net;
using System.Net.Http.Json;
using flash_cards_multiple_api.Models;
using Microsoft.AspNetCore.Mvc;

namespace flash_cards_multiple_api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_ReturnsCreatedUser_WithNormalizedEmail()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"  {email.ToUpperInvariant()} ", "Alice", "passw0rd1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUser>();
        Assert.Equal(email, user!.Email);
        Assert.Equal("Alice", user.DisplayName);
    }

    [Fact]
    public async Task Register_GivesNewUserTheirOwnCopyOfTheSampleSeries()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var series = await client.GetFromJsonAsync<List<CardSeriesDto>>("/api/card-series?include=cards");

        Assert.Equal(["Cat", "Dog"], series!.Select(s => s.Topic));
        var dog = series!.Single(s => s.Topic == "Dog");
        Assert.Equal(["Hund", "perro"], dog.Cards.Select(c => c.Back));
        Assert.NotEqual(Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001"), dog.Id); // a copy, not the template
    }

    [Fact]
    public async Task Register_CopiesAreIndependentPerUser()
    {
        var (first, _) = await factory.CreateUserClientAsync();
        var (second, _) = await factory.CreateUserClientAsync();

        var firstSeries = (await first.GetFromJsonAsync<List<CardSeriesSummaryDto>>("/api/card-series"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync($"/api/card-series/{firstSeries[0].Id}")).StatusCode);

        var secondSeries = await second.GetFromJsonAsync<List<CardSeriesSummaryDto>>("/api/card-series");
        Assert.Equal(2, secondSeries!.Count);
    }

    [Theory]
    [InlineData("not-an-email", "Alice", "passw0rd1", "email")]
    [InlineData("", "Alice", "passw0rd1", "email")]
    [InlineData("a@example.com", "", "passw0rd1", "displayName")]
    [InlineData("a@example.com", "x", "short1", "password")]
    [InlineData("a@example.com", "x", "onlyletters", "password")]
    [InlineData("a@example.com", "x", "12345678", "password")]
    public async Task Register_InvalidInput_Returns400WithFieldError(string email, string name, string password, string field)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, name, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_CaseInsensitive()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "A", "passw0rd1"))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), "B", "passw0rd1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Login_ReturnsTokenThatAuthenticatesMe()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Alice", "passw0rd1"))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email.ToUpperInvariant(), "passw0rd1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.True(login.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(email, login.User.Email);

        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me");
        Assert.Equal(login.User.Id, me!.Id);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_AreIndistinguishable401s()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Alice", "passw0rd1"))).EnsureSuccessStatusCode();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrongpass1"));
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(NewEmail(), "passw0rd1"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync().ContinueWith(t => Strip(t.Result)),
                     await unknownEmail.Content.ReadAsStringAsync().ContinueWith(t => Strip(t.Result)));
    }

    // Bodies match except for the per-request traceId.
    private static string Strip(string body) => System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "");

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("GET", "/api/card-series")]
    [InlineData("POST", "/api/card-series")]
    [InlineData("GET", "/api/card-series/a1b2c3d4-0001-4000-8000-000000000001")]
    public async Task ProtectedEndpoints_Return401_WithoutToken(string method, string url)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_Returns401()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^3] + (token[^1] == 'A' ? "BBB" : "AAA"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/card-series")).StatusCode);
    }

    [Fact]
    public async Task Logout_Returns204_WithValidToken()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Login_IsRateLimited_AfterPermitLimitAttempts()
    {
        using var limited = new ApiFactory { AuthPermitLimit = 3 };
        var client = limited.CreateClient();
        var attempt = new LoginRequest(NewEmail(), "wrongpass1");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", attempt)).StatusCode);

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
             HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests],
            statuses);
    }
}
