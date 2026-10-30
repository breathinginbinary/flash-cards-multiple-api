namespace flash_cards_multiple_api.Data.Entities;

public class Card
{
    public const int CardsPerSeries = 3;

    public Guid Id { get; set; }
    public Guid CardSeriesId { get; set; }

    /// <summary>0..<see cref="CardsPerSeries"/>-1; unique within a series.</summary>
    public int Position { get; set; }
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";

    public CardSeries CardSeries { get; set; } = null!;
}
