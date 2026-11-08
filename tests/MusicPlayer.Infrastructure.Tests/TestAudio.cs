namespace MusicPlayer.Infrastructure.Tests;

/// <summary>Archivos de audio mínimos para las pruebas de etiquetas: sin derechos y sin sonido.</summary>
internal static class TestAudio
{
    public const int FlacFrameLength = 64;

    /// <summary>MP3 de ocho marcos vacíos (MPEG1 capa 3, 128 kbps, 44,1 kHz), sin etiquetas.</summary>
    public static string CreateMp3(string dir, string name = "song.mp3")
    {
        var path = Path.Combine(dir, name);
        var frame = new byte[417];
        frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x00;

        using var stream = File.Create(path);
        for (int i = 0; i < 8; i++) stream.Write(frame);
        return path;
    }

    /// <summary>
    /// FLAC armado a mano: la firma, un STREAMINFO (8 kHz, mono, 16 bits, 0 muestras) y 64 bytes que empiezan como un
    /// marco de audio (sincronía 0xFFF8). No se decodifica: sirve para comprobar que la escritura no toca el audio.
    /// </summary>
    public static string CreateFlac(string dir, string name = "song.flac")
    {
        var path = Path.Combine(dir, name);
        var info = new byte[34];
        info[0] = 0x10; info[2] = 0x10;                        // tamaño de bloque mínimo y máximo: 4096
        const int rate = 8000;                                 // 20 bits de frecuencia, 3 de canales - 1, 5 de bits - 1
        info[10] = rate >> 12;
        info[11] = (rate >> 4) & 0xFF;
        info[12] = ((rate & 0xF) << 4) | (0 << 1) | (15 >> 4);
        info[13] = (15 & 0xF) << 4;

        using var stream = File.Create(path);
        stream.Write("fLaC"u8);
        stream.Write([0x80, 0x00, 0x00, 34]);                  // último bloque, tipo STREAMINFO, 34 bytes
        stream.Write(info);
        var frame = new byte[FlacFrameLength];
        frame[0] = 0xFF; frame[1] = 0xF8;
        for (int i = 2; i < frame.Length; i++) frame[i] = (byte)(i * 13);
        stream.Write(frame);
        return path;
    }

    /// <summary>Copia de Fixtures/silencio.m4a (AAC, 0,1 s de silencio, sin metadatos). Un M4A no se arma a mano.</summary>
    public static string CopyM4a(string dir, string name = "song.m4a")
    {
        var path = Path.Combine(dir, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "silencio.m4a"), path);
        return path;
    }
}
