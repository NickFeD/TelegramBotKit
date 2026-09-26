using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Sample.PhotoSelector.Access;
using TelegramBotKit.Sample.PhotoSelector.Browsing;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;
using TelegramBotKit.Sample.PhotoSelector.Media;
using Xunit;

namespace TelegramBotKit.Sample.PhotoSelector.Tests;

public sealed class PhotoSelectorBehaviorTests
{
    [Fact]
    public async Task FirstPrivateHumanUserBecomesAdmin()
    {
        await using var harness = await TestHarness.CreateAsync();

        var created = await harness.Access.TryBootstrapAdminAsync(101, true, true, true);
        var user = await harness.Access.FindEnabledAsync(101);

        Assert.True(created);
        Assert.Equal(UserRole.Admin, user?.Role);
    }

    [Fact]
    public async Task GroupOrBotMessageCannotCreateAdmin()
    {
        await using var harness = await TestHarness.CreateAsync();

        Assert.False(await harness.Access.TryBootstrapAdminAsync(101, false, true, true));
        Assert.False(await harness.Access.TryBootstrapAdminAsync(102, true, false, true));
        Assert.False(await harness.Access.TryBootstrapAdminAsync(103, true, true, false));

        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentBootstrapCreatesExactlyOneAdmin()
    {
        await using var harness = await TestHarness.CreateAsync();

        var results = await Task.WhenAll(Enumerable.Range(1, 12)
            .Select(id => harness.Access.TryBootstrapAdminAsync(id, true, true, true)));

        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.Single(await db.Users.Where(x => x.Role == UserRole.Admin && x.IsEnabled).ToListAsync());
        Assert.Single(results, x => x);
    }

    [Fact]
    public async Task AdminCanGrantAndRevokeWhileUnauthorizedUserIsDenied()
    {
        await using var harness = await TestHarness.CreateAsync();
        await harness.Access.TryBootstrapAdminAsync(1, true, true, true);

        Assert.False(await harness.Access.IsAuthorizedAsync(2));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Access.GrantAsync(999, 2));

        await harness.Access.GrantAsync(1, 2);
        Assert.True(await harness.Access.IsAuthorizedAsync(2));

        await harness.Access.RevokeAsync(1, 2);
        Assert.False(await harness.Access.IsAuthorizedAsync(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Access.RevokeAsync(1, 1));
    }

    [Fact]
    public async Task OnlyEnabledSourceIsIndexedAndDuplicateMessageIsIgnored()
    {
        await using var harness = await TestHarness.CreateAsync();
        await harness.Access.TryBootstrapAdminAsync(1, true, true, true);
        var envelope = Envelope(messageId: 10);

        Assert.False(await harness.Media.IndexAsync(envelope));

        await harness.Sources.SetEnabledAsync(1, envelope.SourceChatId, envelope.SourceChatTitle, true);
        Assert.True(await harness.Media.IndexAsync(envelope));
        Assert.False(await harness.Media.IndexAsync(envelope));

        await harness.Sources.SetEnabledAsync(1, envelope.SourceChatId, envelope.SourceChatTitle, false);
        Assert.False(await harness.Media.IndexAsync(Envelope(messageId: 11)));

        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.Single(await db.MediaItems.ToListAsync());
        var topic = await db.SourceTopics.SingleAsync();
        Assert.Equal("Vacation", topic.Title);
    }

    [Fact]
    public async Task LikeSkipTransitionsPersistAndAreUserSpecific()
    {
        await using var harness = await SeedMediaAsync();
        var mediaId = await GetOnlyMediaIdAsync(harness);

        await harness.Selections.SetAsync(1, mediaId, SelectionState.Liked);
        await harness.Selections.SetAsync(1, mediaId, SelectionState.Skipped);
        await harness.Selections.SetAsync(2, mediaId, SelectionState.Skipped);
        await harness.Selections.SetAsync(2, mediaId, SelectionState.Liked);

        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(SelectionState.Skipped, (await db.Selections.FindAsync(1L, mediaId))?.State);
        Assert.Equal(SelectionState.Liked, (await db.Selections.FindAsync(2L, mediaId))?.State);
        Assert.Equal(2, await db.Selections.CountAsync());
    }

    [Fact]
    public async Task ExportUsesOriginalFileIdInsteadOfPreview()
    {
        await using var harness = await SeedMediaAsync();
        var mediaId = await GetOnlyMediaIdAsync(harness);
        await harness.Selections.SetAsync(1, mediaId, SelectionState.Liked);

        var item = Assert.Single(await harness.Exports.GetLikedAsync(1));

        Assert.Equal("original-file-id", item.FileId);
        Assert.NotEqual("preview-file-id", item.FileId);
        Assert.Equal(MediaKind.ImageDocument, item.MediaKind);

        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.False((await db.MediaItems.SingleAsync()).PreviewIsReusable);
    }

    [Theory]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.VideoDocument)]
    public async Task VideoKindsArePersistedAndExportTheOriginal(MediaKind mediaKind)
    {
        await using var harness = await TestHarness.CreateAsync();
        await harness.Access.TryBootstrapAdminAsync(1, true, true, true);
        await harness.Sources.SetEnabledAsync(1, -100123, "Видео", true);

        var envelope = Envelope(20) with
        {
            MediaKind = mediaKind,
            OriginalFileId = "original-video-id",
            OriginalFileUniqueId = "original-video-unique-id",
            PreviewFileId = "preview-video-id",
            PreviewFileUniqueId = "preview-video-unique-id",
            PreviewIsReusable = mediaKind == MediaKind.Video,
            FileName = "video.mp4",
            MimeType = "video/mp4"
        };
        Assert.True(await harness.Media.IndexAsync(envelope));

        var mediaId = await GetOnlyMediaIdAsync(harness);
        await harness.Selections.SetAsync(1, mediaId, SelectionState.Liked);
        var exported = Assert.Single(await harness.Exports.GetLikedAsync(1));

        Assert.Equal(mediaKind, exported.MediaKind);
        Assert.Equal("original-video-id", exported.FileId);
    }

    [Fact]
    public async Task BackNavigationCanChangeAnEarlierDecision()
    {
        await using var harness = await SeedMediaAsync();
        Assert.True(await harness.Media.IndexAsync(Envelope(11)));
        var first = await harness.Browsing.StartAsync(1, BrowseFilter.Unreviewed);
        Assert.NotNull(first);

        await harness.Selections.SetAsync(1, first.Media.Id, SelectionState.Liked);
        var second = await harness.Browsing.MoveAsync(1, first.Media.Id, forward: true);
        Assert.NotNull(second);
        var previous = await harness.Browsing.MoveAsync(1, second.Media.Id, forward: false);
        Assert.Equal(first.Media.Id, previous?.Media.Id);

        await harness.Selections.SetAsync(1, first.Media.Id, SelectionState.Skipped);
        await using var db = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(SelectionState.Skipped, (await db.Selections.FindAsync(1L, first.Media.Id))?.State);
    }

    private static async Task<TestHarness> SeedMediaAsync()
    {
        var harness = await TestHarness.CreateAsync();
        await harness.Access.TryBootstrapAdminAsync(1, true, true, true);
        await harness.Access.GrantAsync(1, 2);
        await harness.Sources.SetEnabledAsync(1, -100123, "Photos", true);
        await harness.Media.IndexAsync(Envelope(10));
        return harness;
    }

    private static async Task<long> GetOnlyMediaIdAsync(TestHarness harness)
    {
        await using var db = await harness.Factory.CreateDbContextAsync();
        return await db.MediaItems.Select(x => x.Id).SingleAsync();
    }

    private static MediaEnvelope Envelope(int messageId) => new(
        SourceChatId: -100123,
        SourceChatTitle: "Photos",
        SourceMessageId: messageId,
        SourceThreadId: 42,
        SourceSenderUserId: 7,
        MediaKind: MediaKind.ImageDocument,
        OriginalFileId: "original-file-id",
        OriginalFileUniqueId: "original-unique-id",
        PreviewFileId: "preview-file-id",
        PreviewFileUniqueId: "preview-unique-id",
        PreviewIsReusable: false,
        FileName: "photo.png",
        MimeType: "image/png",
        FileSize: 1234,
        Width: 320,
        Height: 240,
        TelegramMessageDateUtc: DateTime.UtcNow,
        TopicTitle: "Vacation");

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _databasePath;

        private TestHarness(ServiceProvider provider, string databasePath)
        {
            _provider = provider;
            _databasePath = databasePath;
        }

        public IDbContextFactory<PhotoSelectorDbContext> Factory =>
            _provider.GetRequiredService<IDbContextFactory<PhotoSelectorDbContext>>();
        public AccessService Access => _provider.GetRequiredService<AccessService>();
        public SourceChatService Sources => _provider.GetRequiredService<SourceChatService>();
        public MediaIndexService Media => _provider.GetRequiredService<MediaIndexService>();
        public SelectionService Selections => _provider.GetRequiredService<SelectionService>();
        public BrowseService Browsing => _provider.GetRequiredService<BrowseService>();
        public ExportService Exports => _provider.GetRequiredService<ExportService>();

        public static async Task<TestHarness> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"photo-selector-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddDbContextFactory<PhotoSelectorDbContext>(options =>
                options.UseSqlite($"Data Source={path};Default Timeout=15"));
            services.AddSingleton<AccessService>();
            services.AddSingleton<SourceChatService>();
            services.AddSingleton<MediaIndexService>();
            services.AddSingleton<SelectionService>();
            services.AddSingleton<BrowseService>();
            services.AddSingleton<ExportService>();
            var provider = services.BuildServiceProvider();
            var harness = new TestHarness(provider, path);

            await using var db = await harness.Factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
    }
}
