using MusicPlayer.Core.Services;
using A = MusicPlayer.Core.Services.IslandHoverActions;
using S = MusicPlayer.Core.Services.IslandHoverState;

namespace MusicPlayer.Core.Tests;

public class IslandHoverTests
{
    private static IslandHover At(S state)
    {
        var hover = new IslandHover();
        if (state == S.Collapsed) return hover;
        hover.PointerEntered();
        if (state == S.Expanded) return hover;
        hover.PointerExited();
        if (state == S.PendingCollapse) return hover;
        hover.CollapseDelayElapsed(pointerOverPill: false);
        return hover; // Collapsing
    }

    [Fact]
    public void Enter_WhenCollapsed_GrowsTheHitAreaBeforeExpandingThePill()
    {
        var hover = new IslandHover();
        Assert.Equal(A.GrowHitArea | A.ExpandPill, hover.PointerEntered());
        Assert.Equal(S.Expanded, hover.State);
    }

    [Fact]
    public void Exit_WhenExpanded_WaitsBeforeCollapsing()
    {
        var hover = At(S.Expanded);
        Assert.Equal(A.StartCollapseDelay, hover.PointerExited());
        Assert.Equal(S.PendingCollapse, hover.State);
    }

    [Fact]
    public void Enter_DuringTheDelay_CancelsItWithoutResizing()
    {
        var hover = At(S.PendingCollapse);
        Assert.Equal(A.CancelCollapseDelay, hover.PointerEntered());
        Assert.Equal(S.Expanded, hover.State);
    }

    [Fact]
    public void DelayElapsed_CollapsesThePillButKeepsTheHitArea()
    {
        var hover = At(S.PendingCollapse);
        Assert.Equal(A.CollapsePill, hover.CollapseDelayElapsed(pointerOverPill: false));
        Assert.Equal(S.Collapsing, hover.State);
    }

    [Fact]
    public void DelayElapsed_WithThePointerStillOnThePill_IsAFalseExit()
    {
        // Un PointerExited espurio (cursor quieto mientras la pastilla cambia) no colapsa nada.
        var hover = At(S.PendingCollapse);
        Assert.Equal(A.None, hover.CollapseDelayElapsed(pointerOverPill: true));
        Assert.Equal(S.Expanded, hover.State);
    }

    [Fact]
    public void CollapseFinished_ShrinksTheHitArea()
    {
        var hover = At(S.Collapsing);
        Assert.Equal(A.ShrinkHitArea, hover.CollapseFinished(pointerOverPill: false));
        Assert.Equal(S.Collapsed, hover.State);
    }

    [Fact]
    public void Enter_WhileCollapsing_ReexpandsWithoutShrinkingTheHitArea()
    {
        // Caso límite: volver a entrar mientras se colapsa. La zona de clics sigue grande, no se toca.
        var hover = At(S.Collapsing);
        Assert.Equal(A.ExpandPill, hover.PointerEntered());
        Assert.Equal(S.Expanded, hover.State);
        Assert.Equal(A.None, hover.CollapseFinished(pointerOverPill: false)); // un fin de colapso tardío se ignora
    }

    [Fact]
    public void CollapseFinished_WithThePointerBack_ReexpandsInsteadOfShrinking()
    {
        // Caso límite: el cursor volvió justo antes de encoger la zona de clics y no llegó PointerEntered.
        var hover = At(S.Collapsing);
        Assert.Equal(A.ExpandPill, hover.CollapseFinished(pointerOverPill: true));
        Assert.Equal(S.Expanded, hover.State);
    }

    [Fact]
    public void Enter_AfterShrinking_GrowsAgain()
    {
        var hover = At(S.Collapsing);
        hover.CollapseFinished(pointerOverPill: false);
        Assert.Equal(A.GrowHitArea | A.ExpandPill, hover.PointerEntered());
    }

    [Fact]
    public void RepeatedEnterAndExit_NeverResizesTheHitArea()
    {
        var hover = At(S.Expanded);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(A.StartCollapseDelay, hover.PointerExited());
            Assert.Equal(A.CancelCollapseDelay, hover.PointerEntered());
        }
        Assert.Equal(S.Expanded, hover.State);
    }

    [Theory]
    [InlineData(S.Collapsed)]
    [InlineData(S.PendingCollapse)]
    [InlineData(S.Collapsing)]
    public void Exit_OutsideExpanded_DoesNothing(S state) =>
        Assert.Equal(A.None, At(state).PointerExited());

    [Theory]
    [InlineData(S.Collapsed)]
    [InlineData(S.Expanded)]
    [InlineData(S.Collapsing)]
    public void DelayElapsed_OutsidePendingCollapse_DoesNothing(S state) =>
        Assert.Equal(A.None, At(state).CollapseDelayElapsed(pointerOverPill: false));

    [Fact]
    public void Enter_WhenAlreadyExpanded_DoesNothing() =>
        Assert.Equal(A.None, At(S.Expanded).PointerEntered());

    [Theory]
    [InlineData(S.Expanded)]
    [InlineData(S.PendingCollapse)]
    [InlineData(S.Collapsing)]
    public void Reset_ReturnsToCollapsedAndShrinks(S state)
    {
        var hover = At(state);
        Assert.Equal(A.CancelCollapseDelay | A.CollapsePill | A.ShrinkHitArea, hover.Reset());
        Assert.Equal(S.Collapsed, hover.State);
    }

    [Fact]
    public void Reset_WhenCollapsed_DoesNothing() =>
        Assert.Equal(A.None, new IslandHover().Reset());
}
