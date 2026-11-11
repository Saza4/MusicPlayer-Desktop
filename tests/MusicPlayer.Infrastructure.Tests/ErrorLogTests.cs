using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>docs/specs/playback-resilience.md, criterio 3: el registro añade, recorta y nunca lanza.</summary>
public class ErrorLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("error-log-tests").FullName;
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 30, 0, TimeSpan.Zero);

    private string LogPath => Path.Combine(_dir, "datos", "errores.log");

    private ErrorLog NewLog() => new(LogPath, () => Now);

    [Fact]
    public void Write_CreatesTheFolder_AndAppendsDateSourceTypeAndMessage()
    {
        var log = NewLog();

        log.Write("interfaz", new InvalidOperationException("No se pudo abrir"));
        log.Write("tarea", new IOException("Disco lleno"));

        var text = File.ReadAllText(LogPath);
        Assert.Contains("[2026-11-11 10:30:00] interfaz: System.InvalidOperationException: No se pudo abrir", text);
        Assert.Contains("tarea: System.IO.IOException: Disco lleno", text);
        Assert.True(text.IndexOf("No se pudo abrir", StringComparison.Ordinal)
                    < text.IndexOf("Disco lleno", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_IncludesTheStackTrace()
    {
        Exception thrown;
        try { throw new InvalidOperationException("con traza"); }
        catch (Exception ex) { thrown = ex; }

        NewLog().Write("interfaz", thrown);

        Assert.Contains(nameof(Write_IncludesTheStackTrace), File.ReadAllText(LogPath));
    }

    [Fact]
    public void Write_SameErrorInARow_IsWrittenOnce_AndCountedWhenAnotherArrives()
    {
        var log = NewLog();

        for (var i = 0; i < 50; i++)
            log.Write("interfaz", new InvalidOperationException("en bucle"));
        var afterLoop = new FileInfo(LogPath).Length;
        log.Write("interfaz", new IOException("otro"));

        var text = File.ReadAllText(LogPath);
        Assert.Equal(1, text.Split("] interfaz: System.InvalidOperationException: en bucle").Length - 1);
        Assert.True(afterLoop < 2048);
        Assert.Contains("se repitió 49 veces más", text);
    }

    [Fact]
    public void Write_OverTheLimit_KeepsTheMostRecentHalf()
    {
        var log = NewLog();
        var filler = new string('x', 4000);

        for (var i = 0; i < 100; i++) // ~400 KB en total
            log.Write("interfaz", new InvalidOperationException($"error {i} {filler}"));

        var text = File.ReadAllText(LogPath);
        Assert.True(new FileInfo(LogPath).Length <= ErrorLog.MaxBytes);
        Assert.Contains("error 99 ", text);
        Assert.DoesNotContain("error 0 ", text);
    }

    [Fact]
    public void Write_WhenTheFileCannotBeWritten_DoesNotThrow()
    {
        // La «carpeta» del registro es en realidad un archivo: no se puede crear nada dentro.
        var blocker = Path.Combine(_dir, "ocupado");
        File.WriteAllText(blocker, "");
        var log = new ErrorLog(Path.Combine(blocker, "errores.log"));

        log.Write("interfaz", new InvalidOperationException("no cabe"));

        Assert.False(Directory.Exists(blocker));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
