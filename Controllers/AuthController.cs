using System.Net.Mail;
using System.Text.RegularExpressions;
using flash_cards_multiple_api.Data;
using flash_cards_multiple_api.Data.Entities;
using flash_cards_multiple_api.Models;
using flash_cards_multiple_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace flash_cards_multiple_api.Controllers;

[ApiController]
[Route("api/auth")]
public partial class AuthController(
    AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    // Verified against when the email is unknown, so login timing doesn't reveal which emails exist.
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), "dummy-Password-1");

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CurrentUser>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var displayName = (request.DisplayName ?? "").Trim();
        var password = request.Password ?? "";

        if (email.Length is 0 or > 256 || !IsWellFormed(email))
            ModelState.AddModelError("email", "Enter a valid email address.");
        if (displayName.Length is 0 or > 50)
            ModelState.AddModelError("displayName", "Display name must be 1-50 characters.");
        if (password.Length is < 8 or > 100)
            ModelState.AddModelError("password", "Password must be 8-100 characters.");
        else if (!LetterAndDigit().IsMatch(password))
            ModelState.AddModelError("password", "Password needs at least one letter and one digit.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = displayName, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, password);

        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return EmailTaken();
        db.Users.Add(user);

        // Give every new user their own copy of the unowned sample series (Dog, Cat), saved atomically with the user.
        var samples = await db.CardSeries.AsNoTracking().Include(s => s.Cards)
            .Where(s => s.UserId == null).ToListAsync(ct);
        foreach (var sample in samples)
        {
            db.CardSeries.Add(new CardSeries
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Topic = sample.Topic,
                ImageUrl = sample.ImageUrl,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.CreatedAt,
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                Cards = sample.Cards.Select(c => new Card
                {
                    Id = Guid.NewGuid(), Position = c.Position, Front = c.Front, Back = c.Back,
                }).ToList(),
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against the unique email index.
            return EmailTaken();
        }

        return Created($"/api/auth/me", ToCurrentUser(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var password = request.Password ?? "";
        if (email.Length is 0 || password.Length is 0)
        {
            ModelState.AddModelError(email.Length is 0 ? "email" : "password", "This field is required.");
            return ValidationProblem(ModelState);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        var result = hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? DummyHash, password);
        if (user is null || result == PasswordVerificationResult.Failed) return Unauthorized();

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiresAt) = tokens.Create(user);
        return new LoginResponse(token, expiresAt, ToCurrentUser(user));
    }

    // Tokens are stateless, so there is nothing to revoke server-side; the client discards its copy.
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUser>> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        return user is null ? Unauthorized() : ToCurrentUser(user);
    }

    private ConflictObjectResult EmailTaken() => Conflict(new ValidationProblemDetails(
        new Dictionary<string, string[]> { ["email"] = ["That email is already registered."] })
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Email already registered.",
    });

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsWellFormed(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);

    private static CurrentUser ToCurrentUser(User u) => new(u.Id, u.Email, u.DisplayName);

    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d).*$")]
    private static partial Regex LetterAndDigit();
}
