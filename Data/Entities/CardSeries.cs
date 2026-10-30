namespace flash_cards_multiple_api.Data.Entities;

public class CardSeries
{
    public Guid Id { get; set; }

    /// <summary>Owner. Null only for the seeded sample series, which no user can see.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Topic { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Opaque token the client sends back on PUT to detect lost updates.</summary>
    public string ConcurrencyStamp { get; set; } = "";

    public List<Card> Cards { get; set; } = [];
}
