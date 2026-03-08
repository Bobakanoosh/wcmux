using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Unit tests for TabStore: tab collection state management including
/// create, switch, close, rename, ordering, and event firing.
/// </summary>
public class TabStoreTests
{
    private static TabStore CreateStoreWithOneTab(
        out string tabId, out string paneId,
        string label = "~/projects")
    {
        var store = new TabStore();
        tabId = "tab1";
        paneId = "p1";
        store.CreateTab(tabId, paneId, "s1", label);
        return store;
    }

    // ── CreateTab ────────────────────────────────────────────────────────

    [Fact]
    public void CreateTab_AddsTabToCollection()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");

        Assert.Equal(1, store.TabCount);
        Assert.NotNull(store.GetTab("t1"));
    }

    [Fact]
    public void CreateTab_SetsAsActiveTab()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");

        Assert.Equal("t1", store.ActiveTabId);
    }

    [Fact]
    public void CreateTab_CreatesIndependentLayoutStore()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");

        var tab = store.GetTab("t1");
        Assert.NotNull(tab);
        Assert.NotNull(tab.Layout);
        Assert.Equal("p1", tab.Layout.ActivePaneId);
    }

    [Fact]
    public void CreateTab_DefaultLabelNotCustom()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");

        var tab = store.GetTab("t1")!;
        Assert.Equal("~/home", tab.Label);
        Assert.False(tab.IsCustomLabel);
    }

    [Fact]
    public void CreateTab_FiresTabsChangedAndActiveTabChanged()
    {
        var store = new TabStore();
        var tabsChangedCount = 0;
        string? activeTabChangedId = null;

        store.TabsChanged += () => tabsChangedCount++;
        store.ActiveTabChanged += id => activeTabChangedId = id;

        store.CreateTab("t1", "p1", "s1", "~/home");

        Assert.Equal(1, tabsChangedCount);
        Assert.Equal("t1", activeTabChangedId);
    }

    [Fact]
    public void CreateTab_SecondTabBecomesActive()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        Assert.Equal("t2", store.ActiveTabId);
        Assert.Equal(2, store.TabCount);
    }

    // ── SwitchTab ────────────────────────────────────────────────────────

    [Fact]
    public void SwitchTab_ChangesActiveTabId()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        store.SwitchTab("t1");

        Assert.Equal("t1", store.ActiveTabId);
    }

    [Fact]
    public void SwitchTab_FiresActiveTabChanged()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        string? firedId = null;
        store.ActiveTabChanged += id => firedId = id;

        store.SwitchTab("t1");

        Assert.Equal("t1", firedId);
    }

    [Fact]
    public void SwitchTab_DoesNotModifyInactiveTabLayout()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        // Split a pane in t2
        var tab2 = store.GetTab("t2")!;
        tab2.Layout.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");

        // Switch to t1
        store.SwitchTab("t1");

        // t1 should still be single-pane
        var tab1 = store.GetTab("t1")!;
        Assert.True(tab1.Layout.IsSinglePane);

        // t2 should still have the split
        tab2 = store.GetTab("t2")!;
        Assert.False(tab2.Layout.IsSinglePane);
    }

    [Fact]
    public void SwitchTab_SameTabIsNoOp()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        var firedCount = 0;
        store.ActiveTabChanged += _ => firedCount++;

        store.SwitchTab(tabId);

        Assert.Equal(0, firedCount);
    }

    [Fact]
    public void SwitchTab_UnknownTabIdIsNoOp()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        var firedCount = 0;
        store.ActiveTabChanged += _ => firedCount++;

        store.SwitchTab("nonexistent");

        Assert.Equal(0, firedCount);
        Assert.Equal(tabId, store.ActiveTabId);
    }

    // ── CloseTab ─────────────────────────────────────────────────────────

    [Fact]
    public void CloseTab_RemovesFromCollection()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        store.CloseTab("t1");

        Assert.Equal(1, store.TabCount);
        Assert.Null(store.GetTab("t1"));
    }

    [Fact]
    public void CloseTab_FiresTabsChanged()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        var fired = 0;
        store.TabsChanged += () => fired++;

        store.CloseTab("t1");

        Assert.Equal(1, fired);
    }

    [Fact]
    public void CloseTab_ReturnsClosedTabState()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        var closed = store.CloseTab("t1");

        Assert.NotNull(closed);
        Assert.Equal("t1", closed.TabId);
    }

    [Fact]
    public void CloseTab_ActiveTab_SwitchesToRightNeighbor()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/a");
        store.CreateTab("t2", "p2", "s2", "~/b");
        store.CreateTab("t3", "p3", "s3", "~/c");
        store.SwitchTab("t2"); // make t2 active

        store.CloseTab("t2");

        Assert.Equal("t3", store.ActiveTabId);
    }

    [Fact]
    public void CloseTab_ActiveTab_NoRightNeighbor_SwitchesToLeft()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/a");
        store.CreateTab("t2", "p2", "s2", "~/b");
        store.CreateTab("t3", "p3", "s3", "~/c");
        // t3 is active (last created)

        store.CloseTab("t3");

        Assert.Equal("t2", store.ActiveTabId);
    }

    [Fact]
    public void CloseTab_InactiveTab_DoesNotChangeActiveTab()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/a");
        store.CreateTab("t2", "p2", "s2", "~/b");
        // t2 is active

        store.CloseTab("t1");

        Assert.Equal("t2", store.ActiveTabId);
    }

    [Fact]
    public void CloseTab_LastTab_FiresLastTabClosed()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        var lastTabFired = false;
        store.LastTabClosed += () => lastTabFired = true;

        store.CloseTab(tabId);

        Assert.True(lastTabFired);
    }

    [Fact]
    public void CloseTab_LastTab_ReturnsTabState()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        var closed = store.CloseTab(tabId);

        Assert.NotNull(closed);
        Assert.Equal(tabId, closed.TabId);
    }

    // ── RenameTab ────────────────────────────────────────────────────────

    [Fact]
    public void RenameTab_UpdatesLabelAndSetsCustomFlag()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        store.RenameTab(tabId, "My Tab");

        var tab = store.GetTab(tabId)!;
        Assert.Equal("My Tab", tab.Label);
        Assert.True(tab.IsCustomLabel);
    }

    [Fact]
    public void RenameTab_FiresTabsChanged()
    {
        var store = CreateStoreWithOneTab(out var tabId, out _);

        var fired = 0;
        store.TabsChanged += () => fired++;

        store.RenameTab(tabId, "Renamed");

        Assert.Equal(1, fired);
    }

    // ── TabOrder ─────────────────────────────────────────────────────────

    [Fact]
    public void TabOrder_MaintainsInsertionOrder()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/a");
        store.CreateTab("t2", "p2", "s2", "~/b");
        store.CreateTab("t3", "p3", "s3", "~/c");

        Assert.Equal(new[] { "t1", "t2", "t3" }, store.TabOrder);
    }

    [Fact]
    public void TabOrder_CloseMiddleTab_PreservesOrder()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/a");
        store.CreateTab("t2", "p2", "s2", "~/b");
        store.CreateTab("t3", "p3", "s3", "~/c");

        store.CloseTab("t2");

        Assert.Equal(new[] { "t1", "t3" }, store.TabOrder);
    }

    // ── GetTab ───────────────────────────────────────────────────────────

    [Fact]
    public void GetTab_UnknownId_ReturnsNull()
    {
        var store = CreateStoreWithOneTab(out _, out _);

        Assert.Null(store.GetTab("nonexistent"));
    }

    // ── ActiveLayout ─────────────────────────────────────────────────────

    [Fact]
    public void ActiveLayout_ReturnsActiveTabLayoutStore()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        var activeLayout = store.ActiveLayout;

        Assert.NotNull(activeLayout);
        Assert.Equal("p2", activeLayout.ActivePaneId);
    }

    [Fact]
    public void ActiveLayout_AfterSwitch_ReturnsNewActiveTabLayout()
    {
        var store = new TabStore();
        store.CreateTab("t1", "p1", "s1", "~/home");
        store.CreateTab("t2", "p2", "s2", "~/work");

        store.SwitchTab("t1");

        Assert.Equal("p1", store.ActiveLayout!.ActivePaneId);
    }

    // ── TabCount ─────────────────────────────────────────────────────────

    [Fact]
    public void TabCount_EmptyStore_ReturnsZero()
    {
        var store = new TabStore();
        Assert.Equal(0, store.TabCount);
    }
}
