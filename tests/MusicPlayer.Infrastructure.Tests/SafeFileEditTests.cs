namespace MusicPlayer.Infrastructure.Tests;

// docs/specs/audit-3.md, H7.
public class SafeFileEditTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mp-safe-" + Guid.NewGuid().ToString("N"));

    public SafeFileEditTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string OldFile()
    {
        var path = Path.Combine(_root, "cancion.mp3");
        File.WriteAllText(path, "original");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-30));
        return path;
    }

    [Fact]
    public void Run_TheWorkingCopyIsDatedNow_NotLikeTheOriginal()
    {
        var path = OldFile();
        DateTime? copyDate = null;

        SafeFileEdit.Run(path, temp => copyDate = File.GetLastWriteTimeUtc(temp));

        // Con la fecha del original (30 días) el escáner lo tomaría por un temporal abandonado y lo borraría.
        Assert.NotNull(copyDate);
        Assert.True(DateTime.UtcNow - copyDate.Value < TimeSpan.FromHours(1));
    }

    [Fact]
    public void Run_WhenTheEditFailsAndTheCopyCannotBeDeleted_ThrowsTheEditError()
    {
        var path = OldFile();
        FileStream? held = null;
        try
        {
            // En Windows, el temporal abierto en exclusiva no se puede borrar: ese fallo no debe tapar el de la edición.
            var error = Assert.Throws<InvalidOperationException>(() => SafeFileEdit.Run(path, temp =>
            {
                held = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.None);
                throw new InvalidOperationException("fallo de la edición");
            }));

            Assert.Equal("fallo de la edición", error.Message);
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally
        {
            held?.Dispose();
        }
    }
}
