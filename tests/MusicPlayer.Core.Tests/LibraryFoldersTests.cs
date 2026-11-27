using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/auto-refresh-folders.md, criterios 2 y 3.</summary>
public class LibraryFoldersTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "library-folders"));
    private static string At(params string[] parts) => Path.Combine([Root, .. parts]);

    [Fact]
    public void Add_ToEmpty_AddsTheNormalizedFolder()
    {
        var folders = LibraryFolders.Add([], At("Música") + Path.DirectorySeparatorChar);

        Assert.Equal([At("Música")], folders);
    }

    [Fact]
    public void Add_SameFolderAgain_AddsNothing()
    {
        var folders = LibraryFolders.Add([At("a")], At("a"));

        Assert.Equal([At("a")], folders);
    }

    [Fact]
    public void Add_FolderInsideAnAddedOne_AddsNothing()
    {
        var folders = LibraryFolders.Add([At("a")], At("a", "rock"));

        Assert.Equal([At("a")], folders);
    }

    [Fact]
    public void Add_FolderThatContainsAddedOnes_ReplacesThem()
    {
        var folders = LibraryFolders.Add([At("a", "rock"), At("b"), At("a", "jazz")], At("a"));

        Assert.Equal([At("b"), At("a")], folders);
    }

    [Fact]
    public void Add_SiblingWithTheSamePrefix_IsNotInside()
    {
        var folders = LibraryFolders.Add([At("a")], At("ab"));

        Assert.Equal([At("a"), At("ab")], folders);
    }

    [Fact]
    public void Add_CaseInsensitiveComparer_TreatsCaseAsTheSameFolder()
    {
        var folders = LibraryFolders.Add([At("Musica")], At("MUSICA"), FilePaths.For(caseSensitive: false));

        Assert.Equal([At("Musica")], folders);
    }

    [Fact]
    public void Add_CaseSensitiveComparer_KeepsBoth()
    {
        var folders = LibraryFolders.Add([At("Musica")], At("MUSICA"), FilePaths.For(caseSensitive: true));

        Assert.Equal(2, folders.Count);
    }

    [Fact]
    public void Add_FileSystemRoot_ContainsEverything()
    {
        var root = Path.GetPathRoot(Root)!;

        var folders = LibraryFolders.Add([At("a")], root);

        Assert.Equal([root], folders);
        Assert.True(LibraryFolders.Contains(root, At("a")));
    }

    [Fact]
    public void Clean_DropsEmptyAndRelativeEntriesAndMergesOverlaps()
    {
        var folders = LibraryFolders.Clean([null, "", "   ", "relativa", At("a", "rock"), At("a"), At("a")]);

        Assert.Equal([At("a")], folders);
    }

    [Fact]
    public void Clean_InvalidPath_IsDroppedWithoutThrowing()
    {
        var folders = LibraryFolders.Clean([At("a") + "\0b", At("c")]);

        Assert.Equal([At("c")], folders);
    }
}
