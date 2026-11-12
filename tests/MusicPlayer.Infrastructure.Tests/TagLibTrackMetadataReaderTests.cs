using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>
/// docs/specs/untrusted-input-limits.md, criterios 7 y 9: un archivo dañado nunca hace lanzar al lector. El caso
/// salió de dañar bytes de los audios de prueba: TagLib lanza ArgumentOutOfRangeException (ni CorruptFileException
/// ni IOException) cuando un tamaño del bloque de comentarios de un FLAC es absurdo.
/// </summary>
public class TagLibTrackMetadataReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("metadata-reader-tests").FullName;
    private readonly TagLibTrackMetadataReader _reader = new();

    private string TaggedFlac()
    {
        var path = TestAudio.CreateFlac(_dir);
        using var file = TagLib.File.Create(path);
        file.Tag.Title = "Título de prueba";
        file.Tag.Performers = new[] { "Artista" };
        file.Save();
        return path;
    }

    [Fact]
    public void Read_HealthyFile_ReturnsItsTags()
    {
        var track = _reader.Read(TaggedFlac());

        Assert.Equal("Título de prueba", track!.Title);
        Assert.Equal("Artista", track.Artist);
    }

    [Fact]
    public void Read_FlacWithAnAbsurdCommentLength_ReturnsNullInsteadOfThrowing()
    {
        var path = TaggedFlac();
        var bytes = File.ReadAllBytes(path);

        // Firma (4) + cabecera y STREAMINFO (4 + 34): ahí empieza el bloque de comentarios Vorbis (tipo 4).
        const int block = 4 + 4 + 34;
        Assert.Equal(4, bytes[block] & 0x7F);
        // Tras su cabecera de 4 bytes va la longitud del «vendor»: se pone a 0xFFFFFFFF.
        for (var i = 0; i < 4; i++) bytes[block + 4 + i] = 0xFF;
        File.WriteAllBytes(path, bytes);

        Assert.Null(_reader.Read(path));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
