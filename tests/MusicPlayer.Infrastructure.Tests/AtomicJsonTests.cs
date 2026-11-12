using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>docs/specs/library-data-safety-2.md, criterios 4 y 5: temporal limpio y respaldos que no se pisan.</summary>
public class AtomicJsonTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("atomic-json-tests").FullName;

    private string FilePath => Path.Combine(_dir, "datos.json");

    private sealed record Good(string Name);

    private sealed class Unserializable
    {
        public string Name => throw new InvalidOperationException("no se puede leer");
    }

    private string[] Backups() => Directory.GetFiles(_dir, "datos.json.corrupt-*").Order().ToArray();

    [Fact]
    public async Task Write_ThenRead_RoundTrips_AndLeavesNoTemporary()
    {
        await AtomicJson.WriteAsync(FilePath, new Good("uno"), default);

        Assert.Equal(new Good("uno"), await AtomicJson.ReadAsync<Good>(FilePath, default));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task Write_WhenSerializationFails_LeavesNoTemporary_AndKeepsThePreviousFile()
    {
        await AtomicJson.WriteAsync(FilePath, new Good("uno"), default);
        var before = await File.ReadAllTextAsync(FilePath);

        await Assert.ThrowsAnyAsync<Exception>(() => AtomicJson.WriteAsync(FilePath, new Unserializable(), default));

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.Equal(before, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotThrow_AndLeaveAValidFile()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            Task.Run(() => AtomicJson.WriteAsync(FilePath, new Good(new string((char)('a' + i), 50_000)), default))));

        Assert.NotNull(await AtomicJson.ReadAsync<Good>(FilePath, default));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task Read_CorruptTwice_KeepsBothBackups()
    {
        var now = new DateTimeOffset(2026, 11, 11, 10, 15, 0, TimeSpan.Zero);

        await File.WriteAllTextAsync(FilePath, "{ primero roto");
        Assert.Null(await AtomicJson.ReadAsync<Good>(FilePath, default, () => now));
        await File.WriteAllTextAsync(FilePath, "{ segundo roto");
        Assert.Null(await AtomicJson.ReadAsync<Good>(FilePath, default, () => now)); // mismo segundo

        var backups = Backups();
        Assert.Equal(2, backups.Length);
        Assert.EndsWith("datos.json.corrupt-20261111-101500", backups[0]);
        Assert.Equal(new[] { "{ primero roto", "{ segundo roto" }, backups.Select(File.ReadAllText));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Read_CorruptSevenTimes_KeepsTheFiveMostRecent_AndNeverTouchesAnOldDotCorrupt()
    {
        var legacy = FilePath + ".corrupt";
        await File.WriteAllTextAsync(legacy, "respaldo de una versión anterior");
        var start = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 7; i++)
        {
            await File.WriteAllTextAsync(FilePath, $"{{ roto {i}");
            var at = start.AddMinutes(i);
            await AtomicJson.ReadAsync<Good>(FilePath, default, () => at);
        }

        Assert.Equal(
            new[] { "{ roto 2", "{ roto 3", "{ roto 4", "{ roto 5", "{ roto 6" },
            Backups().Select(File.ReadAllText));
        Assert.True(File.Exists(legacy));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
