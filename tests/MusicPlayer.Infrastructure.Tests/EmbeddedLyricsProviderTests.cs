using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>docs/specs/untrusted-input-limits.md, criterio 3: la letra incrustada también tiene el tope de 1 MB.</summary>
public class EmbeddedLyricsProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("embedded-lyrics-tests").FullName;
    private readonly EmbeddedLyricsProvider _provider = new(new LrcLyricParser());

    private Track TrackWithLyrics(string lyrics)
    {
        var path = TestAudio.CreateMp3(_dir);
        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = lyrics;
            file.Save();
        }
        return new Track { FilePath = path, Title = "song" };
    }

    [Fact]
    public async Task ReadsEmbeddedLyrics()
    {
        var block = await _provider.GetLyricsAsync(TrackWithLyrics("[00:01.00]Letra inventada"));

        Assert.Equal(LyricsKind.Synced, block.Kind);
    }

    [Fact]
    public async Task EmbeddedLyricsOverTheLimit_AreTreatedAsMissing()
    {
        var huge = "[00:01.00]Letra inventada\n" + new string(' ', (int)LyricsEditor.MaxBytes);

        Assert.False((await _provider.GetLyricsAsync(TrackWithLyrics(huge))).IsAvailable);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
