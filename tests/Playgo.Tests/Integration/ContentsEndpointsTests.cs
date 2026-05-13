using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Playgo.Application.Common;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Enums;
using Playgo.Tests.Common;

namespace Playgo.Tests.Integration;

public class ContentsEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TestWebAppFactory _factory;

    public ContentsEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _factory.Seed = db =>
        {
            if (db.Contents.Any()) return;

            var action = db.Genres.FirstOrDefault(g => g.Name == "Action") ?? TestDataBuilder.CreateGenre("Action");
            var drama = db.Genres.FirstOrDefault(g => g.Name == "Drama") ?? TestDataBuilder.CreateGenre("Drama");
            if (action.Id == Guid.Empty || !db.Genres.Any(g => g.Id == action.Id)) db.Genres.Add(action);
            if (drama.Id == Guid.Empty || !db.Genres.Any(g => g.Id == drama.Id)) db.Genres.Add(drama);

            var c1 = TestDataBuilder.CreateContent("Featured Movie A", ContentType.Movie, ContentStatus.Published, isFeatured: true, genres: action);
            var c2 = TestDataBuilder.CreateContent("Trending Movie B", ContentType.Movie, ContentStatus.Published, genres: drama);
            c2.ViewCount = 999_999;
            var c3 = TestDataBuilder.CreateContent("Draft Movie C", ContentType.Movie, ContentStatus.Draft, genres: drama);
            db.Contents.AddRange(c1, c2, c3);
            db.SaveChanges();
        };
    }

    [Fact]
    public async Task Get_ReturnsPagedPublishedContents_NoDrafts()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/contents?page=1&pageSize=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await resp.Content.ReadFromJsonAsync<PagedResult<ContentListItemDto>>(JsonOpts);
        paged.Should().NotBeNull();
        paged!.Items.Should().NotContain(c => c.Title == "Draft Movie C");
    }

    [Fact]
    public async Task Featured_ReturnsFeaturedOnly()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/contents/featured?limit=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<ContentListItemDto>>(JsonOpts);
        items.Should().Contain(c => c.Title == "Featured Movie A");
    }

    [Fact]
    public async Task Trending_OrdersByViewCount()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/contents/trending?limit=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<ContentListItemDto>>(JsonOpts);
        items.Should().NotBeEmpty();
        items![0].Title.Should().Be("Trending Movie B");
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/contents/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
