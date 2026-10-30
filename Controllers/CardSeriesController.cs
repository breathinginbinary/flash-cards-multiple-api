using flash_cards_multiple_api.Data;
using flash_cards_multiple_api.Data.Entities;
using flash_cards_multiple_api.Models;
using flash_cards_multiple_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace flash_cards_multiple_api.Controllers;

// The Angular dev proxy forwards /api/* unchanged, so the route carries the /api prefix.
[ApiController]
[Authorize]
[Route("api/card-series")]
public class CardSeriesController(AppDbContext db) : ControllerBase
{
    private const int MaxTopic = 200;
    private const int MaxFront = 500;
    private const int MaxBack = 1000;

    // Every query goes through here so another user's series is indistinguishable from a missing one (404, never 403).
    private IQueryable<CardSeries> Mine() => db.CardSeries.Where(s => s.UserId == User.GetUserId());

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? include, CancellationToken ct)
    {
        if (string.Equals(include, "cards", StringComparison.OrdinalIgnoreCase))
        {
            var full = await Mine().AsNoTracking().Include(s => s.Cards)
                .OrderBy(s => s.Topic).ToListAsync(ct);
            return Ok(full.Select(ToDto));
        }

        var summaries = await Mine().AsNoTracking()
            .OrderBy(s => s.Topic)
            .Select(s => new
            {
                s.Id,
                s.Topic,
                s.UpdatedAt,
                FirstCardFront = s.Cards.OrderBy(c => c.Position).Select(c => c.Front).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Ok(summaries.Select(s =>
            new CardSeriesSummaryDto(s.Id, s.Topic, s.FirstCardFront ?? "", AsUtc(s.UpdatedAt))));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardSeriesDto>> Get(Guid id, CancellationToken ct)
    {
        var series = await Mine().AsNoTracking().Include(s => s.Cards)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return series is null ? NotFound() : ToDto(series);
    }

    [HttpPost]
    public async Task<ActionResult<CardSeriesDto>> Create(CreateCardSeriesRequest request, CancellationToken ct)
    {
        if (!Validate(request, out var topic, out var cards)) return ValidationProblem(ModelState);

        var now = DateTime.UtcNow;
        var series = new CardSeries
        {
            Id = Guid.NewGuid(),
            UserId = User.GetUserId(),
            Topic = topic,
            CreatedAt = now,
            UpdatedAt = now,
            ConcurrencyStamp = NewStamp(),
            Cards = cards.Select(c => new Card
            {
                Id = Guid.NewGuid(), Position = c.Position, Front = c.Front, Back = c.Back,
            }).ToList(),
        };
        db.CardSeries.Add(series);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = series.Id }, ToDto(series));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CardSeriesDto>> Update(Guid id, UpdateCardSeriesRequest request, CancellationToken ct)
    {
        if (!Validate(request, out var topic, out var cards)) return ValidationProblem(ModelState);

        var series = await Mine().Include(s => s.Cards).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (series is null) return NotFound();
        if (series.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict(StaleProblem());

        series.Topic = topic;
        series.UpdatedAt = DateTime.UtcNow;
        series.ConcurrencyStamp = NewStamp();

        // Update in place by position rather than delete/insert, so the unique (series, position) index never trips.
        foreach (var existing in series.Cards.Where(c => cards.All(n => n.Position != c.Position)).ToList())
        {
            series.Cards.Remove(existing);
            db.Cards.Remove(existing);
        }
        foreach (var input in cards)
        {
            var card = series.Cards.FirstOrDefault(c => c.Position == input.Position);
            if (card is null)
            {
                // Add via the DbSet: a card with a preset key attached through the tracked collection is
                // treated as Modified, so SaveChanges would UPDATE a missing row and surface as a false 409.
                db.Cards.Add(new Card
                {
                    Id = Guid.NewGuid(), CardSeriesId = series.Id,
                    Position = input.Position, Front = input.Front, Back = input.Back,
                });
            }
            else
            {
                card.Front = input.Front;
                card.Back = input.Back;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed or deleted between our read and write.
            return Conflict(StaleProblem());
        }
        return ToDto(series);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var series = await Mine().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (series is null) return NotFound();

        db.CardSeries.Remove(series); // cards cascade
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Trims inputs and enforces the limits the Angular editor mirrors (topic 1-200, front 1-500, back 1-1000, 1-3 cards at positions 0..n-1).</summary>
    private bool Validate(CreateCardSeriesRequest request, out string topic, out List<CardInput> cards)
    {
        topic = (request.Topic ?? "").Trim();
        cards = (request.Cards ?? []).Select(c => new CardInput(c.Position, (c.Front ?? "").Trim(), (c.Back ?? "").Trim()))
            .OrderBy(c => c.Position).ToList();

        if (topic.Length is 0 or > MaxTopic)
            ModelState.AddModelError("topic", $"Topic must be 1-{MaxTopic} characters.");

        if (cards.Count is < 1 or > Card.CardsPerSeries)
            ModelState.AddModelError("cards", $"A series needs 1-{Card.CardsPerSeries} cards.");
        else if (!cards.Select(c => c.Position).SequenceEqual(Enumerable.Range(0, cards.Count)))
            ModelState.AddModelError("cards", "Card positions must be 0, 1, 2... with no gaps or duplicates.");

        foreach (var c in cards)
        {
            if (c.Front.Length is 0 or > MaxFront)
                ModelState.AddModelError($"cards[{c.Position}].front", $"Front must be 1-{MaxFront} characters.");
            if (c.Back.Length is 0 or > MaxBack)
                ModelState.AddModelError($"cards[{c.Position}].back", $"Back must be 1-{MaxBack} characters.");
        }

        return ModelState.IsValid;
    }

    private static ProblemDetails StaleProblem() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "This series was changed elsewhere.",
    };

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    // SQLite returns DateTimes with Kind=Unspecified; stamp them UTC so they serialize with a trailing Z.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static CardSeriesDto ToDto(CardSeries s) => new(
        s.Id,
        s.Topic,
        s.ImageUrl,
        s.Cards.OrderBy(c => c.Position).Select(c => new CardDto(c.Id, c.Position, c.Front, c.Back)).ToList(),
        AsUtc(s.CreatedAt),
        AsUtc(s.UpdatedAt),
        s.ConcurrencyStamp);
}
