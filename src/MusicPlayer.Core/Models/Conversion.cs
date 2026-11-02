namespace MusicPlayer.Core.Models;

public enum AudioFormat { Mp3, Flac, Wav, M4a }

/// <summary>El bitrate solo se aplica a los formatos con pérdida (MP3 y M4A).</summary>
public sealed record ConversionOptions(AudioFormat Format, int BitrateKbps = 192);

public sealed record ConversionBatchResult(
    IReadOnlyList<string> Created, int Skipped, IReadOnlyList<string> Errors, bool Cancelled);