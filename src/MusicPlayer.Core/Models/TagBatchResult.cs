namespace MusicPlayer.Core.Models;

/// <summary>Un cambio de etiquetas de una pista, con lo anterior para poder deshacerlo.</summary>
public sealed record TagChange(Guid TrackId, TrackTags Before, TrackTags After);

public sealed record TagBatchResult(
    LibrarySnapshot Snapshot, IReadOnlyList<TagChange> Applied, IReadOnlyList<string> Errors);