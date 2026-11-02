namespace MusicPlayer.Core.Models;

public sealed record CoverBatchResult(int Written, IReadOnlyList<string> Errors);