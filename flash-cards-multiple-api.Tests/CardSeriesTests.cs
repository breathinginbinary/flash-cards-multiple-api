using System.Net;
using System.Net.Http.Json;
using flash_cards_multiple_api.Models;
using Microsoft.AspNetCore.Mvc;

namespace flash_cards_multiple_api.Tests;

public class CardSeriesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/card-series";

    private static CreateCardSeriesRequest ThreeCards(string topic = "Bird") => new(topic,
    [
        new CardInput(0, "German", "Vogel"),
        new CardInput(1, "Spanish", "pajaro"),
        new CardInput(2, "French", "oiseau"),
    ]);

    private static async Task<CardSeriesDto> CreateAsync(HttpClient client, CreateCardSeriesRequest request)
    {
        var response = await client.PostAsJsonAsync(Url, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CardSeriesDto>())!;
    }

    [Fact]
    public async Task Create_Returns201_WithLocation_AndTrimsInput()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(Url, new CreateCardSeriesRequest("  Bird  ",
            [new CardInput(0, "  German ", " Vogel  ")]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CardSeriesDto>())!;
        Assert.Equal($"{Url}/{created.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Bird", created.Topic);
        Assert.Equal(("German", "Vogel"), (created.Cards[0].Front, created.Cards[0].Back));
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.False(string.IsNullOrEmpty(created.ConcurrencyStamp));

        var fetched = await client.GetFromJsonAsync<CardSeriesDto>($"{Url}/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
    }

    public static TheoryData<string, CreateCardSeriesRequest> InvalidRequests => new()
    {
        { "topic", new("   ", [new CardInput(0, "a", "b")]) },
        { "topic", new(new string('x', 201), [new CardInput(0, "a", "b")]) },
        { "cards", new("T", []) },
        { "cards", new("T", [new(0, "a", "b"), new(1, "a", "b"), new(2, "a", "b"), new(3, "a", "b")]) },
        { "cards", new("T", [new(0, "a", "b"), new(2, "a", "b")]) },          // gap
        { "cards", new("T", [new(0, "a", "b"), new(0, "c", "d")]) },          // duplicate position
        { "cards[0].front", new("T", [new(0, " ", "b")]) },
        { "cards[0].front", new("T", [new(0, new string('x', 501), "b")]) },
        { "cards[0].back", new("T", [new(0, "a", "")]) },
        { "cards[0].back", new("T", [new(0, "a", new string('x', 1001))]) },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Create_InvalidInput_Returns400WithFieldError(string field, CreateCardSeriesRequest request)
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task List_ReturnsSummaries_OrderedByTopic_WithFirstCardFront()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        await CreateAsync(client, ThreeCards("Ant"));

        var list = (await client.GetFromJsonAsync<List<CardSeriesSummaryDto>>(Url))!;

        Assert.Equal(["Ant", "Cat", "Dog"], list.Select(s => s.Topic));
        Assert.All(list, s => Assert.Equal("German", s.FirstCardFront));
    }

    [Fact]
    public async Task List_IncludeCards_ReturnsFullSeriesWithOrderedCards()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        await CreateAsync(client, ThreeCards());

        var list = (await client.GetFromJsonAsync<List<CardSeriesDto>>($"{Url}?include=cards"))!;

        var bird = list.Single(s => s.Topic == "Bird");
        Assert.Equal([0, 1, 2], bird.Cards.Select(c => c.Position));
    }

    [Fact]
    public async Task Update_ChangesTopicAndCards_AndRotatesStamp()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, ThreeCards());

        var response = await client.PutAsJsonAsync($"{Url}/{created.Id}", new UpdateCardSeriesRequest("Birds",
            [new CardInput(0, "German", "Vogel!"), new CardInput(1, "Spanish", "ave")], created.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<CardSeriesDto>())!;
        Assert.Equal("Birds", updated.Topic);
        Assert.Equal(["Vogel!", "ave"], updated.Cards.Select(c => c.Back)); // third card removed
        Assert.Equal(created.Cards[0].Id, updated.Cards[0].Id);              // updated in place
        Assert.NotEqual(created.ConcurrencyStamp, updated.ConcurrencyStamp);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task Update_CanGrowCards()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, new("Bird", [new CardInput(0, "a", "b")]));

        var response = await client.PutAsJsonAsync($"{Url}/{created.Id}", new UpdateCardSeriesRequest("Bird",
            [new CardInput(0, "a", "b"), new CardInput(1, "c", "d"), new CardInput(2, "e", "f")], created.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await response.Content.ReadFromJsonAsync<CardSeriesDto>())!.Cards.Count);
    }

    [Fact]
    public async Task Update_WithStaleStamp_Returns409_AndLeavesSeriesUnchanged()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, ThreeCards());
        var cards = created.Cards.Select(c => new CardInput(c.Position, c.Front, c.Back)).ToList();
        (await client.PutAsJsonAsync($"{Url}/{created.Id}", new UpdateCardSeriesRequest("First", cards, created.ConcurrencyStamp)))
            .EnsureSuccessStatusCode();

        var stale = await client.PutAsJsonAsync($"{Url}/{created.Id}", new UpdateCardSeriesRequest("Second", cards, created.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("First", (await client.GetFromJsonAsync<CardSeriesDto>($"{Url}/{created.Id}"))!.Topic);
    }

    [Fact]
    public async Task Update_InvalidInput_Returns400()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, ThreeCards());

        var response = await client.PutAsJsonAsync($"{Url}/{created.Id}",
            new UpdateCardSeriesRequest("", [new CardInput(0, "a", "b")], created.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204_ThenSeriesIsGone()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, ThreeCards());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Url}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Url}/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task UnknownId_Returns404()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Url}/{id}",
            new UpdateCardSeriesRequest("T", [new CardInput(0, "a", "b")], "stamp"))).StatusCode);
    }

    [Fact]
    public async Task OtherUsersSeries_Return404_NeverForbidden_OnEveryVerb()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (intruder, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(owner, ThreeCards());
        var path = $"{Url}/{created.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync(path, new UpdateCardSeriesRequest(
            "Hijacked", [new CardInput(0, "a", "b")], created.ConcurrencyStamp))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(path)).StatusCode);

        var list = await intruder.GetFromJsonAsync<List<CardSeriesSummaryDto>>(Url);
        Assert.DoesNotContain(list!, s => s.Id == created.Id);
        Assert.Equal("Bird", (await owner.GetFromJsonAsync<CardSeriesDto>(path))!.Topic); // untouched
    }

    [Fact]
    public async Task UnownedSampleTemplates_AreNotReachableById()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.GetAsync($"{Url}/a1b2c3d4-0001-4000-8000-000000000001");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
