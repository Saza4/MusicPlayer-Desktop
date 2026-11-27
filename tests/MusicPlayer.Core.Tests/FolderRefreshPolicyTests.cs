using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/auto-refresh-folders.md, criterio 11.</summary>
public class FolderRefreshPolicyTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstTime_Starts()
    {
        Assert.True(new FolderRefreshPolicy().TryStart(T0));
    }

    [Fact]
    public void WhileRunning_DoesNotStartAnother()
    {
        var policy = new FolderRefreshPolicy();
        policy.TryStart(T0);

        Assert.False(policy.TryStart(T0.AddMinutes(5)));
    }

    [Fact]
    public void BeforeTheMinimumInterval_DoesNotStart()
    {
        var policy = new FolderRefreshPolicy();
        policy.TryStart(T0);
        policy.Finish(T0.AddSeconds(10));

        Assert.False(policy.TryStart(T0.AddSeconds(10) + FolderRefreshPolicy.MinInterval - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void TheIntervalCountsFromTheEndOfTheLastRefresh()
    {
        var policy = new FolderRefreshPolicy();
        policy.TryStart(T0);
        policy.Finish(T0.AddSeconds(10));

        Assert.True(policy.TryStart(T0.AddSeconds(10) + FolderRefreshPolicy.MinInterval));
    }

    [Fact]
    public void ClockMovedBack_Starts()
    {
        var policy = new FolderRefreshPolicy();
        policy.TryStart(T0);
        policy.Finish(T0);

        Assert.True(policy.TryStart(T0.AddHours(-1)));
    }

    [Fact]
    public void MinInterval_IsOneMinute()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), FolderRefreshPolicy.MinInterval);
    }
}
