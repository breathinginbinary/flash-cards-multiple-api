namespace flash_cards_multiple_api.Data.Entities;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Trimmed and lower-cased; unique.</summary>
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public List<CardSeries> CardSeries { get; set; } = [];
}
