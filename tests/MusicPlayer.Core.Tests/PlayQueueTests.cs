using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class PlayQueueTests
{
    private static readonly string[] Songs = { "a", "b", "c", "d", "e" };

    private static PlayQueue<string> Started(
        int start = 0, bool shuffle = false, int seed = 1, RepeatMode repeat = RepeatMode.Off)
    {
        var queue = new PlayQueue<string>(new Random(seed)) { Shuffle = shuffle, Repeat = repeat };
        queue.Start(Songs, start);
        return queue;
    }

    // Lo que suena desde la pista actual hasta el final (solo con Repeat = Off).
    private static List<string> PlayThrough(PlayQueue<string> queue)
    {
        var played = new List<string> { queue.Current! };
        while (queue.MoveNext() is { } next) played.Add(next);
        return played;
    }

    [Fact]
    public void Sequential_PlaysInOrderAndStopsAtTheEnd()
    {
        var queue = Started();

        Assert.Equal(Songs, PlayThrough(queue));
        Assert.Null(queue.MoveNext());
        Assert.Equal("e", queue.Current);
    }

    [Fact]
    public void Start_InTheMiddle_BeginsThere()
    {
        var queue = Started(2);

        Assert.Equal(new[] { "c", "d", "e" }, PlayThrough(queue));
    }

    [Fact]
    public void RepeatAll_WrapsToTheBeginning()
    {
        var queue = Started(4, repeat: RepeatMode.All);

        Assert.Equal("a", queue.MoveNext());
    }

    [Fact]
    public void RepeatOne_OnlyAppliesWhenTheTrackEnds()
    {
        var queue = Started(1, repeat: RepeatMode.One);

        Assert.Equal("c", queue.MoveNext()); // el botón Siguiente avanza igualmente

        Assert.Equal("c", queue.MoveNextAfterEnd()); // al terminar sola, se repite
        Assert.Equal("c", queue.Current);
    }

    [Fact]
    public void AfterEnd_WithoutRepeatOne_MovesToTheNextTrack()
    {
        var queue = Started(1);

        Assert.Equal("c", queue.MoveNextAfterEnd());
    }

    [Fact]
    public void MovePrevious_GoesBackAndStopsAtTheStart()
    {
        var queue = Started(1);

        Assert.Equal("a", queue.MovePrevious());
        Assert.Null(queue.MovePrevious());
        Assert.Equal("a", queue.Current);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    public void Shuffle_VisitsEveryTrackOnceStartingWithTheChosenOne(int seed)
    {
        var played = PlayThrough(Started(2, shuffle: true, seed: seed));

        Assert.Equal("c", played[0]);
        Assert.Equal(Songs.OrderBy(s => s), played.OrderBy(s => s));
    }

    [Fact]
    public void Shuffle_DoesNotModifyTheSourceList()
    {
        var source = new[] { "a", "b", "c", "d", "e" };
        var queue = new PlayQueue<string>(new Random(3)) { Shuffle = true };
        queue.Start(source, 0);

        PlayThrough(queue);

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, source);
    }

    [Fact]
    public void TurningShuffleOff_ContinuesFromTheCurrentTrackInOriginalOrder()
    {
        var queue = Started(0, shuffle: true, seed: 7);
        var current = queue.MoveNext()!;

        queue.Shuffle = false;

        Assert.Equal(current, queue.Current);
        var index = Array.IndexOf(Songs, current);
        Assert.Equal(index + 1 < Songs.Length ? Songs[index + 1] : null, queue.MoveNext());
    }

    [Fact]
    public void TurningShuffleOn_KeepsTheCurrentTrackAndPlaysEveryTrackOnce()
    {
        var queue = Started(2);

        queue.Shuffle = true;
        var played = PlayThrough(queue);

        Assert.Equal("c", played[0]);
        Assert.Equal(Songs.OrderBy(s => s), played.OrderBy(s => s));
    }

    [Fact]
    public void RepeatAllWithShuffle_ReshufflesAndNeverRepeatsTheLastTrackFirst()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var queue = Started(0, shuffle: true, seed: seed, repeat: RepeatMode.All);
            var last = queue.Current!;
            for (int i = 1; i < Songs.Length; i++)
                last = queue.MoveNext()!; // termina la primera vuelta

            var secondRound = new List<string> { queue.MoveNext()! };
            Assert.NotEqual(last, secondRound[0]);

            for (int i = 1; i < Songs.Length; i++)
                secondRound.Add(queue.MoveNext()!);
            Assert.Equal(Songs.OrderBy(s => s), secondRound.OrderBy(s => s));
        }
    }

    [Fact]
    public void RemoveWhere_OtherTrack_IsSkipped()
    {
        var queue = Started(0);

        queue.RemoveWhere(s => s == "b");

        Assert.Equal("c", queue.MoveNext());
    }

    [Fact]
    public void RemoveWhere_CurrentTrack_NextContinuesWithWhatFollowed()
    {
        var queue = Started(2);

        queue.RemoveWhere(s => s == "c");

        Assert.Equal("d", queue.MoveNext());
    }

    [Fact]
    public void RemoveWhere_Everything_LeavesAnEmptyQueue()
    {
        var queue = Started(2);

        queue.RemoveWhere(_ => true);

        Assert.Equal(0, queue.Count);
        Assert.Null(queue.Current);
        Assert.Null(queue.MoveNext());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Start_WithInvalidIndex_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayQueue<string>().Start(Songs, index));
    }

    [Fact]
    public void EmptyQueue_HasNothingToPlay()
    {
        var queue = new PlayQueue<string>();

        Assert.Null(queue.Current);
        Assert.Null(queue.MoveNext());
        Assert.Null(queue.MovePrevious());
    }

    [Fact]
    public void PlayNext_PlaysRightAfterTheCurrentTrack()
    {
        var queue = Started(1);
        queue.PlayNext("x");
        Assert.Equal("x", queue.MoveNext());
        Assert.Equal("c", queue.MoveNext());
    }

    [Fact]
    public void PlayNext_Several_PlayInTheOrderTheyWereAdded()
    {
        var queue = Started(1);
        queue.PlayNext("x");
        queue.PlayNext("y");
        Assert.Equal("x", queue.MoveNext());
        Assert.Equal("y", queue.MoveNext());
        Assert.Equal("c", queue.MoveNext());
    }

    [Fact]
    public void PlayNext_AfterTheQueuedTrackStarted_NewOneGoesRightAfterIt()
    {
        var queue = Started(1);
        queue.PlayNext("x");
        Assert.Equal("x", queue.MoveNext());
        queue.PlayNext("y");
        Assert.Equal("y", queue.MoveNext());
        Assert.Equal("c", queue.MoveNext());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PlayNext_WithShuffle_StillPlaysNext(int seed)
    {
        var queue = Started(0, shuffle: true, seed: seed);
        queue.PlayNext("x");
        Assert.Equal("x", queue.MoveNext());
    }

    [Fact]
    public void PlayNext_OnEmptyQueue_PlaysIt()
    {
        var queue = new PlayQueue<string>();
        queue.PlayNext("x");
        Assert.Equal(1, queue.Count);
        Assert.Equal("x", queue.MoveNext());
    }

    [Fact]
    public void Enqueue_PlaysAfterEverythingElse()
    {
        var queue = Started(2);
        queue.Enqueue("x");
        Assert.Equal(new[] { "c", "d", "e", "x" }, PlayThrough(queue));
    }

    [Fact]
    public void Enqueue_WithShuffle_StaysLast()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var queue = Started(0, shuffle: true, seed: seed);
            queue.Enqueue("x");
            var played = PlayThrough(queue);
            Assert.Equal(Songs.Length + 1, played.Count);
            Assert.Equal("x", played[^1]);
        }
    }

    [Fact]
    public void Enqueue_WithRepeatAll_JoinsTheNextRound()
    {
        var queue = Started(4, repeat: RepeatMode.All);
        queue.Enqueue("x");
        Assert.Equal("x", queue.MoveNext());
        Assert.Equal("a", queue.MoveNext()); // nueva vuelta
    }

    [Fact]
    public void RemoveWhere_QueuedTrack_IsSkipped()
    {
        var queue = Started(0);
        queue.PlayNext("x");
        queue.RemoveWhere(s => s == "x");
        Assert.Equal("b", queue.MoveNext());
    }

    [Fact]
    public void Peek_ReturnsTheFollowingTrackWithoutAdvancing()
    {
        var queue = Started(1);

        Assert.Equal("c", queue.PeekNextAfterEnd());
        Assert.Equal("b", queue.Current);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Peek_MatchesWhatTheTrackEndingWouldPlay_EvenWithShuffle(int seed)
    {
        var queue = Started(0, shuffle: true, seed: seed);

        var peeked = queue.PeekNextAfterEnd();

        Assert.Equal(peeked, queue.MoveNextAfterEnd());
    }

    [Fact]
    public void Peek_RepeatOne_ReturnsTheCurrentTrack()
    {
        var queue = Started(1, repeat: RepeatMode.One);

        Assert.Equal("b", queue.PeekNextAfterEnd());
    }

    [Fact]
    public void Peek_LastTrackWithRepeatOff_ReturnsNull()
    {
        Assert.Null(Started(4).PeekNextAfterEnd());
    }

    [Fact]
    public void Peek_LastTrackWithRepeatAll_ReturnsTheFirstTrack()
    {
        var queue = Started(4, repeat: RepeatMode.All);

        Assert.Equal("a", queue.PeekNextAfterEnd());
        Assert.Equal("a", queue.MoveNextAfterEnd());
    }

    [Fact]
    public void Peek_LastTrackWithRepeatAllAndShuffle_IsUnknownUntilTheRoundStarts()
    {
        var queue = Started(0, shuffle: true, seed: 1, repeat: RepeatMode.All);
        for (int i = 1; i < Songs.Length; i++) queue.MoveNext();

        Assert.Null(queue.PeekNextAfterEnd());
    }

    [Fact]
    public void Peek_SeesTracksAddedWithPlayNext()
    {
        var queue = Started(1);

        queue.PlayNext("x");

        Assert.Equal("x", queue.PeekNextAfterEnd());
    }

    [Fact]
    public void Peek_EmptyQueue_ReturnsNull()
    {
        Assert.Null(new PlayQueue<string>().PeekNextAfterEnd());
    }
}