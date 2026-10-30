namespace flash_cards_multiple_api.Models;

// JSON shapes mirror SPEC.md §6.1 (core/auth) in the Angular app.

public record RegisterRequest(string Email, string DisplayName, string Password);

public record LoginRequest(string Email, string Password);

public record CurrentUser(Guid Id, string Email, string DisplayName);

public record LoginResponse(string AccessToken, DateTime ExpiresAt, CurrentUser User);
