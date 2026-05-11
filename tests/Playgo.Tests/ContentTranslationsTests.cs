using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class ContentTranslationsTests
{
    private static (ContentService Service, ApplicationDbContext Db, Mock<ILocalizationContext> Locale) BuildSubject(string currentLanguage = "en")
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"i18n-{Guid.NewGuid()}")
            .Options;

        var db = new ApplicationDbContext(options);
        var locale = new Mock<ILocalizationContext>();
        locale.Setup(l => l.CurrentLanguage).Returns(currentLanguage);

        var service = new ContentService(db, locale.Object);
        return (service, db, locale);
    }

    private static CreateContentRequest BuildCreateRequest(List<UpsertContentTranslationRequest>? translations = null) =>
        new(
            Title: "Default Title",
            OriginalTitle: null,
            Description: "Default description",
            ShortDescription: null,
            Type: ContentType.Movie,
            ReleaseYear: 2024,
            ReleaseDate: null,
            DurationMinutes: 100,
            Country: null,
            Language: null,
            AgeRating: null,
            PosterUrl: null,
            BackdropUrl: null,
            TrailerUrl: null,
            VideoUrl: null,
            HlsManifestUrl: null,
            Director: "Default Director",
            Cast: null,
            IsFeatured: false,
            GenreIds: new List<Guid>(),
            Translations: translations);

    private static UpdateContentRequest BuildUpdateRequest(List<UpsertContentTranslationRequest>? translations) =>
        new(
            Title: "Default Title",
            OriginalTitle: null,
            Description: "Default description",
            ShortDescription: null,
            Type: ContentType.Movie,
            ReleaseYear: 2024,
            ReleaseDate: null,
            DurationMinutes: 100,
            Country: null,
            Language: null,
            AgeRating: null,
            PosterUrl: null,
            BackdropUrl: null,
            TrailerUrl: null,
            VideoUrl: null,
            HlsManifestUrl: null,
            Director: "Default Director",
            Cast: null,
            IsFeatured: false,
            GenreIds: new List<Guid>(),
            Status: ContentStatus.Published,
            IsTrending: false,
            Translations: translations);

    [Fact]
    public async Task Create_WithTranslations_PersistsAll()
    {
        var (service, db, _) = BuildSubject();

        var request = BuildCreateRequest(new List<UpsertContentTranslationRequest>
        {
            new("uz", "O'zbekcha", null, "Tavsif", null, null, null),
            new("ru", "Русский", null, "Описание", null, null, null),
        });

        var result = await service.CreateAsync(request);

        result.Success.Should().BeTrue();
        result.Data!.Translations.Should().HaveCount(2);

        var stored = await db.ContentTranslations.AsNoTracking().ToListAsync();
        stored.Should().HaveCount(2);
        stored.Select(t => t.LanguageCode).Should().BeEquivalentTo(new[] { "uz", "ru" });
        stored.Single(t => t.LanguageCode == "uz").Title.Should().Be("O'zbekcha");
        stored.Single(t => t.LanguageCode == "ru").Description.Should().Be("Описание");
    }

    [Fact]
    public async Task Update_RemovesOldTranslations_AndReplaces()
    {
        var (service, db, _) = BuildSubject();

        var created = await service.CreateAsync(BuildCreateRequest(new List<UpsertContentTranslationRequest>
        {
            new("uz", "Eski Uz", null, "Eski tavsif", null, null, null),
            new("en", "Old English", null, "Old desc", null, null, null),
        }));
        created.Success.Should().BeTrue();
        var contentId = created.Data!.Id;

        var update = await service.UpdateAsync(contentId, BuildUpdateRequest(new List<UpsertContentTranslationRequest>
        {
            new("ru", "Только Русский", null, "Только описание", null, null, null),
        }));

        update.Success.Should().BeTrue();

        var alive = await db.ContentTranslations.AsNoTracking()
            .Where(t => !t.IsDeleted && t.ContentId == contentId)
            .ToListAsync();
        alive.Should().HaveCount(1);
        alive[0].LanguageCode.Should().Be("ru");
        alive[0].Title.Should().Be("Только Русский");
    }

    [Fact]
    public async Task GetById_WithLangQuery_ReturnsLocalizedTitle()
    {
        var (service, _, locale) = BuildSubject(currentLanguage: "en");

        var created = await service.CreateAsync(BuildCreateRequest(new List<UpsertContentTranslationRequest>
        {
            new("uz", "O'zbekcha Sarlavha", null, "O'zbekcha tavsif", "Qisqacha", "Ali", "Vasya"),
        }));
        created.Success.Should().BeTrue();
        var contentId = created.Data!.Id;

        var asEnglish = await service.GetContentByIdAsync(contentId);
        asEnglish.Success.Should().BeTrue();
        asEnglish.Data!.Title.Should().Be("Default Title");

        locale.Setup(l => l.CurrentLanguage).Returns("uz");
        var asUz = await service.GetContentByIdAsync(contentId);
        asUz.Success.Should().BeTrue();
        asUz.Data!.Title.Should().Be("O'zbekcha Sarlavha");
        asUz.Data.Description.Should().Be("O'zbekcha tavsif");
        asUz.Data.Director.Should().Be("Ali");
        asUz.Data.Cast.Should().Be("Vasya");
        asUz.Data.Translations.Should().ContainSingle(t => t.LanguageCode == "uz");
    }

    [Fact]
    public async Task UniqueConstraint_PreventsDuplicateLangPerContent()
    {
        var (service, db, _) = BuildSubject();

        var created = await service.CreateAsync(BuildCreateRequest());
        created.Success.Should().BeTrue();
        var contentId = created.Data!.Id;

        var first = await service.UpsertTranslationAsync(contentId,
            new UpsertContentTranslationRequest("uz", "Birinchi", null, "Birinchi tavsif", null, null, null));
        first.Success.Should().BeTrue();

        var second = await service.UpsertTranslationAsync(contentId,
            new UpsertContentTranslationRequest("uz", "Ikkinchi", null, "Ikkinchi tavsif", null, null, null));
        second.Success.Should().BeTrue();

        var rows = await db.ContentTranslations.AsNoTracking()
            .Where(t => t.ContentId == contentId && t.LanguageCode == "uz")
            .ToListAsync();

        rows.Should().HaveCount(1, "the second upsert must mutate the existing row, never create a duplicate");
        rows[0].Title.Should().Be("Ikkinchi");
        rows[0].Description.Should().Be("Ikkinchi tavsif");
    }
}
