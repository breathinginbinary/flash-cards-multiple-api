using flash_cards_multiple_api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace flash_cards_multiple_api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CardSeries> CardSeries => Set<CardSeries>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(50);
            e.Property(u => u.PasswordHash).IsRequired();
            e.HasMany(u => u.CardSeries).WithOne(s => s.User)
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Length limits mirror the Angular card editor validators.
        modelBuilder.Entity<CardSeries>(e =>
        {
            e.Property(s => s.Topic).IsRequired().HasMaxLength(200);
            e.Property(s => s.ImageUrl).HasMaxLength(500);
            e.Property(s => s.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
            e.HasMany(s => s.Cards).WithOne(c => c.CardSeries)
                .HasForeignKey(c => c.CardSeriesId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Card>(e =>
        {
            e.Property(c => c.Front).IsRequired().HasMaxLength(500);
            e.Property(c => c.Back).IsRequired().HasMaxLength(1000);
            e.HasIndex(c => new { c.CardSeriesId, c.Position }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint(
                "CK_Cards_Position", $"\"Position\" >= 0 AND \"Position\" < {Card.CardsPerSeries}"));
        });

        SeedData(modelBuilder);
    }

    // The two placeholder series hard-coded in the Angular study page (Dog, Cat).
    private static void SeedData(ModelBuilder modelBuilder)
    {
        var seededAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var dogId = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");
        var catId = Guid.Parse("a1b2c3d4-0002-4000-8000-000000000002");

        modelBuilder.Entity<CardSeries>().HasData(
            new CardSeries
            {
                Id = dogId, Topic = "Dog", ImageUrl = "/images/Dog.png",
                CreatedAt = seededAt, UpdatedAt = seededAt,
                ConcurrencyStamp = "a1b2c3d4000140008000000000000001",
            },
            new CardSeries
            {
                Id = catId, Topic = "Cat", ImageUrl = "/images/Cat.png",
                CreatedAt = seededAt, UpdatedAt = seededAt,
                ConcurrencyStamp = "a1b2c3d4000240008000000000000002",
            });

        modelBuilder.Entity<Card>().HasData(
            new Card { Id = Guid.Parse("b1b2c3d4-0001-4000-8000-000000000001"), CardSeriesId = dogId, Position = 0, Front = "German", Back = "Hund" },
            new Card { Id = Guid.Parse("b1b2c3d4-0001-4000-8000-000000000002"), CardSeriesId = dogId, Position = 1, Front = "Spanish", Back = "perro" },
            new Card { Id = Guid.Parse("b1b2c3d4-0002-4000-8000-000000000001"), CardSeriesId = catId, Position = 0, Front = "German", Back = "Katze" },
            new Card { Id = Guid.Parse("b1b2c3d4-0002-4000-8000-000000000002"), CardSeriesId = catId, Position = 1, Front = "Spanish", Back = "gato" });
    }
}
