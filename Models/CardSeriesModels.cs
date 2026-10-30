namespace flash_cards_multiple_api.Models;

// JSON shapes mirror flash-card-multiple/src/app/core/cards/card.models.ts.

public record CardDto(Guid Id, int Position, string Front, string Back);

public record CardSeriesDto(
    Guid Id,
    string Topic,
    string? ImageUrl,
    List<CardDto> Cards,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ConcurrencyStamp);

public record CardSeriesSummaryDto(Guid Id, string Topic, string FirstCardFront, DateTime UpdatedAt);

public record CardInput(int Position, string Front, string Back);

public record CreateCardSeriesRequest(string Topic, List<CardInput> Cards);

public record UpdateCardSeriesRequest(string Topic, List<CardInput> Cards, string ConcurrencyStamp)
    : CreateCardSeriesRequest(Topic, Cards);
