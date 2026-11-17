using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class PlaylistManagerTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private sealed class InMemoryRepository : IPlaylistRepository
    {
        public IReadOnlyList<Playlist> Stored { get; set; } = Array.Empty<Playlist>();
        public int SaveCount { get; private set; }
        public bool FailOnSave { get; set; }

        public Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(Stored);

        public Task SaveAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct = default)
        {
            if (FailOnSave) throw new IOException("disco lleno");
            Stored = playlists;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Create_TrimsNamePersistsAndStartsEmpty()
    {
        var repo = new InMemoryRepository();

        var playlist = await new PlaylistManager(repo).CreateAsync("  Gym  ");

        Assert.Equal("Gym", playlist.Name);
        Assert.Empty(playlist.TrackIds);
        Assert.Single(repo.Stored);
        Assert.Equal(1, repo.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankName_Throws(string name)
    {
        var manager = new PlaylistManager(new InMemoryRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateAsync(name));
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_Throws()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        await manager.CreateAsync("Rock");

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateAsync("rock"));
    }

    [Fact]
    public async Task Rename_ChangesNameAndPersists()
    {
        var repo = new InMemoryRepository();
        var manager = new PlaylistManager(repo);
        var playlist = await manager.CreateAsync("Viejo");

        await manager.RenameAsync(playlist.Id, "Nuevo");

        Assert.Equal("Nuevo", manager.Find(playlist.Id)!.Name);
        Assert.Equal("Nuevo", repo.Stored[0].Name);
    }

    [Fact]
    public async Task Rename_ToAnotherPlaylistsName_Throws()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        await manager.CreateAsync("A");
        var second = await manager.CreateAsync("B");

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RenameAsync(second.Id, "a"));
        Assert.Equal("B", manager.Find(second.Id)!.Name);
    }

    [Fact]
    public async Task Rename_OnlyChangingCaseOfOwnName_IsAllowed()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        var playlist = await manager.CreateAsync("rock");

        var renamed = await manager.RenameAsync(playlist.Id, "Rock");

        Assert.Equal("Rock", renamed.Name);
    }

    [Fact]
    public async Task Delete_RemovesPlaylist()
    {
        var repo = new InMemoryRepository();
        var manager = new PlaylistManager(repo);
        var playlist = await manager.CreateAsync("X");

        await manager.DeleteAsync(playlist.Id);

        Assert.Empty(manager.Playlists);
        Assert.Empty(repo.Stored);
    }

    [Fact]
    public async Task AddTracks_AppendsInOrderAndSkipsDuplicates()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        var playlist = await manager.CreateAsync("P");

        var first = await manager.AddTracksAsync(playlist.Id, new[] { A, B, A });
        var second = await manager.AddTracksAsync(playlist.Id, new[] { B, C });

        Assert.Equal(2, first);  // A y B; la segunda A se omite
        Assert.Equal(1, second); // solo C
        Assert.Equal(new[] { A, B, C }, manager.Find(playlist.Id)!.TrackIds);
    }

    [Fact]
    public async Task RemoveTrack_RemovesOnlyThatTrack()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        var playlist = await manager.CreateAsync("P");
        await manager.AddTracksAsync(playlist.Id, new[] { A, B });

        await manager.RemoveTrackAsync(playlist.Id, A);

        Assert.Equal(new[] { B }, manager.Find(playlist.Id)!.TrackIds);
    }

    [Fact]
    public async Task UnknownPlaylistId_Throws()
    {
        var manager = new PlaylistManager(new InMemoryRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.RenameAsync(Guid.NewGuid(), "x"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Load_RestoresPlaylistsFromRepository()
    {
        var stored = new Playlist { Name = "Guardada", TrackIds = new[] { A } };
        var manager = new PlaylistManager(new InMemoryRepository { Stored = new[] { stored } });

        await manager.LoadAsync();

        Assert.Equal("Guardada", Assert.Single(manager.Playlists).Name);
    }

    [Fact]
    public async Task FailedSave_LeavesInMemoryStateUntouched()
    {
        var manager = new PlaylistManager(new InMemoryRepository { FailOnSave = true });

        await Assert.ThrowsAsync<IOException>(() => manager.CreateAsync("X"));

        Assert.Empty(manager.Playlists);
    }

    [Fact]
    public void Resolve_ReturnsItemsInPlaylistOrder_SkippingMissingOnes()
    {
        var library = new[] { (Id: A, Name: "a"), (Id: B, Name: "b"), (Id: C, Name: "c") };
        var playlist = new Playlist { Name = "P", TrackIds = new[] { C, Guid.NewGuid(), A } };

        var result = PlaylistManager.Resolve(playlist, library, i => i.Id);

        Assert.Equal(new[] { "c", "a" }, result.Select(i => i.Name));
    }

    [Fact]
    public async Task RemoveTracksEverywhere_CleansEveryPlaylist()
    {
        var manager = new PlaylistManager(new InMemoryRepository());
        var p1 = await manager.CreateAsync("P1");
        var p2 = await manager.CreateAsync("P2");
        await manager.AddTracksAsync(p1.Id, new[] { A, B });
        await manager.AddTracksAsync(p2.Id, new[] { B, C });

        await manager.RemoveTracksEverywhereAsync(new[] { B });

        Assert.Equal(new[] { A }, manager.Find(p1.Id)!.TrackIds);
        Assert.Equal(new[] { C }, manager.Find(p2.Id)!.TrackIds);
    }

    [Fact]
    public async Task RemoveTracksEverywhere_NothingToRemove_DoesNotSave()
    {
        var repo = new InMemoryRepository();
        var manager = new PlaylistManager(repo);
        var playlist = await manager.CreateAsync("P");
        await manager.AddTracksAsync(playlist.Id, new[] { A });
        var savesBefore = repo.SaveCount;

        await manager.RemoveTracksEverywhereAsync(new[] { C });

        Assert.Equal(savesBefore, repo.SaveCount);
    }

    // ---- Una operación a la vez (docs/specs/playlist-serialization.md) ----

    /// <summary>Retiene los guardados hasta <see cref="Release"/>: así dos operaciones coinciden a medias.</summary>
    private sealed class HeldRepository(params Playlist[] stored) : IPlaylistRepository
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<Playlist> Stored { get; private set; } = stored;
        public bool FailNextSave { get; set; }

        public void Release() => _release.TrySetResult();

        public Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(Stored);

        public async Task SaveAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct = default)
        {
            await _release.Task;
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("disco lleno");
            }
            Stored = playlists;
        }
    }

    /// <summary>Un gestor con la playlist «Gym» ya cargada y los guardados retenidos.</summary>
    private static async Task<(HeldRepository Repo, PlaylistManager Manager, Playlist Gym)> SeededAsync()
    {
        var gym = new Playlist { Name = "Gym" };
        var repo = new HeldRepository(gym);
        var manager = new PlaylistManager(repo);
        await manager.LoadAsync();
        return (repo, manager, gym);
    }

    [Fact]
    public async Task TwoAddsAtOnce_KeepBothTracks()
    {
        var (held, manager, gym) = await SeededAsync();

        var addA = manager.AddTracksAsync(gym.Id, new[] { A });
        var addB = manager.AddTracksAsync(gym.Id, new[] { B });
        held.Release();
        await Task.WhenAll(addA, addB);

        Assert.Equal(new[] { A, B }, manager.Find(gym.Id)!.TrackIds);
        Assert.Equal(new[] { A, B }, held.Stored.Single().TrackIds);
    }

    [Fact]
    public async Task CreateAndAddToAnotherAtOnce_KeepBothChanges()
    {
        var (held, manager, gym) = await SeededAsync();

        var create = manager.CreateAsync("Nueva");
        var add = manager.AddTracksAsync(gym.Id, new[] { A });
        held.Release();
        await Task.WhenAll(create, add);

        Assert.Equal(new[] { "Gym", "Nueva" }, held.Stored.Select(p => p.Name));
        Assert.Equal(new[] { A }, held.Stored[0].TrackIds);
        Assert.Equal(held.Stored, manager.Playlists);
    }

    [Fact]
    public async Task TwoCreatesWithTheSameNameAtOnce_OnlyTheFirstSucceeds()
    {
        var (held, manager, _) = await SeededAsync();

        var first = manager.CreateAsync("Nueva");
        var second = manager.CreateAsync("nueva");
        held.Release();

        await first;
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Equal(2, manager.Playlists.Count);
    }

    [Fact]
    public async Task AfterAFailedSave_TheNextOperationStillRuns()
    {
        var (held, manager, gym) = await SeededAsync();
        held.FailNextSave = true;

        var failing = manager.AddTracksAsync(gym.Id, new[] { A });
        var next = manager.AddTracksAsync(gym.Id, new[] { B });
        held.Release();

        await Assert.ThrowsAsync<IOException>(() => failing);
        await next;
        Assert.Equal(new[] { B }, manager.Find(gym.Id)!.TrackIds);
    }

    [Fact]
    public async Task CancelledWhileWaitingItsTurn_DoesNothing_AndDoesNotBlockTheRest()
    {
        var (held, manager, gym) = await SeededAsync();
        using var cts = new CancellationTokenSource();

        var first = manager.AddTracksAsync(gym.Id, new[] { A });
        var waiting = manager.AddTracksAsync(gym.Id, new[] { B }, cts.Token);
        cts.Cancel();
        // Antes de soltar el primer guardado: si no, el turno podría llegarle justo mientras se cancela.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        held.Release();

        await first;
        Assert.Equal(1, await manager.AddTracksAsync(gym.Id, new[] { C }));
        Assert.Equal(new[] { A, C }, manager.Find(gym.Id)!.TrackIds);
    }
}
