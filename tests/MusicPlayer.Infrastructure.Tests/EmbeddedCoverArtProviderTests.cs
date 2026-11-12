using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>Tope de tamaño al leer la carátula incrustada (docs/specs/audit-followup.md, criterio 6).</summary>
public class EmbeddedCoverArtProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("embedded-cover-tests").FullName;

    // Otra herramienta sí puede incrustar una imagen mayor que el tope de la app: se escribe con TagLib directamente.
    private string Mp3WithPicture(int bytes)
    {
        var path = TestAudio.CreateMp3(_dir);
        using var file = TagLib.File.Create(path);
        file.Tag.Pictures = [new TagLib.Picture(new TagLib.ByteVector(new byte[bytes])) { Type = TagLib.PictureType.FrontCover }];
        file.Save();
        return path;
    }

    private static Task<byte[]?> Read(string path) =>
        new EmbeddedCoverArtProvider().GetCoverAsync(new Track { FilePath = path, Title = "x" });

    [Fact]
    public async Task PictureWithinTheLimit_IsReturned()
    {
        Assert.Equal(2048, (await Read(Mp3WithPicture(2048)))!.Length);
    }

    [Fact]
    public async Task OversizedPicture_IsTreatedAsMissing()
    {
        Assert.Null(await Read(Mp3WithPicture(CoverImage.MaxBytes + 1)));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
