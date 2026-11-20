using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using AngleSharp.Dom;

using Bunit;
using Bunit.TestDoubles;

using Censio.Contracts;
using Censio.Server.Components.Pages;
using Censio.Server.Data;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the region overview <see cref="Regions" />.
/// </summary>
[TestClass]
public sealed class RegionsTests
{
    #region Constants

    /// <summary>
    /// The text of a figure without a value.
    /// </summary>
    private const string Dash = "–";

    /// <summary>
    /// The first session GUID of the tests.
    /// </summary>
    private const int FirstSession = 3245;

    /// <summary>
    /// The second session GUID of the tests.
    /// </summary>
    private const int SecondSession = 6627;

    /// <summary>
    /// The text of a failed read.
    /// </summary>
    private const string ReloadText = "The data cannot be read right now. Reload the page to try again.";

    /// <summary>
    /// The name of the first profile in the deletion tests.
    /// </summary>
    private const string FirstProfileName = "Profile one";

    /// <summary>
    /// The name of the second profile in the deletion tests.
    /// </summary>
    private const string SecondProfileName = "Profile two";

    /// <summary>
    /// The English dialog text of a deletion of <see cref="FirstProfileName" />.
    /// </summary>
    private const string FirstProfileDialogText = "All data of the profile Profile one is removed from the server: its regions, islands, goods and history. This cannot be undone.";

    /// <summary>
    /// The English text of a failed deletion (spec E9).
    /// </summary>
    private const string DeleteFailedText = "The history could not be deleted.";

    /// <summary>
    /// The statement that makes every deletion of a profile fail before its rows are gone.
    /// </summary>
    private const string CreateFailingDeleteTriggerSql = "CREATE TRIGGER \"FailProfileDelete\" BEFORE DELETE ON \"Profiles\" BEGIN SELECT RAISE(ABORT, 'test delete failure'); END;";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The first profile of the tests.
    /// </summary>
    private static readonly Guid _firstProfile = new("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// The second profile of the tests.
    /// </summary>
    private static readonly Guid _secondProfile = new("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RegionsTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public RegionsTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the temporary directories of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// The overview of a profile's address offers one enabled <c>Delete history</c> button in the language shown, without
    /// the text of a refused host.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="label">The label of the spec</param>
    /// <param name="refusal">The text of a refused host in that language, which the page should not show</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(UiLanguage.English, "Delete history", "Deleting needs this page opened by IP address or as localhost, or by a host name the setting AllowedHosts lists.")]
    [DataRow(UiLanguage.German, "Verlauf löschen", "Löschen setzt voraus, dass diese Seite über die IP-Adresse oder localhost geöffnet wurde, oder über einen Hostnamen, den die Einstellung AllowedHosts nennt.")]
    public async Task RegionsProfileAddressOffersDeleteHistory(UiLanguage language, string label, string refusal)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        // Act
        var page = host.Render<Regions>(language, parameters => parameters.Add(regions => regions.Profile, _firstProfile.ToString()));

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        var buttons = Buttons(page, label);

        Assert.HasCount(1, buttons, "The overview of a profile's address should offer one Delete history button");
        Assert.IsFalse(buttons[0].HasAttribute("disabled"), "The button should be enabled on a page of a trusted host");
        Assert.DoesNotContain(refusal, PageTestHost.Text(page), "A page of a trusted host should not show the refusal text");
    }

    /// <summary>
    /// The button opens the dialog with the spec's title, the text naming the profile as the profile switch names it
    /// (the unnamed text without a name), and the buttons <c>Cancel</c> and <c>Delete</c> in that order; opening it
    /// deletes nothing.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="name">The profile name, or <see langword="null" /> for an unnamed profile</param>
    /// <param name="title">The title of the spec</param>
    /// <param name="text">The text of the spec with the shown name</param>
    /// <param name="cancel">The label of the cancelling button</param>
    /// <param name="confirm">The label of the confirming button</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(UiLanguage.English, FirstProfileName, "Delete history?", FirstProfileDialogText, "Cancel", "Delete")]
    [DataRow(UiLanguage.German, FirstProfileName, "Verlauf löschen?", "Alle Daten des Profils Profile one werden vom Server entfernt: seine Regionen, Inseln, Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.", "Abbrechen", "Löschen")]
    [DataRow(UiLanguage.English, null, "Delete history?", "All data of the profile Unnamed profile is removed from the server: its regions, islands, goods and history. This cannot be undone.", "Cancel", "Delete")]
    [DataRow(UiLanguage.German, null, "Verlauf löschen?", "Alle Daten des Profils Unbenanntes Profil werden vom Server entfernt: seine Regionen, Inseln, Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.", "Abbrechen", "Löschen")]
    public async Task RegionsDeleteDialogShowsSpecTexts(UiLanguage language, string? name, string title, string text, string cancel, string confirm)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreNamedProfileAsync(host, _firstProfile, name, 0);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, language);

        // Act
        ClickDeleteHistory(page, language);

        // Assert
        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual(title, dialogs.Find(".mud-dialog-title").TextContent.Trim(), "The dialog should carry the spec's title");
        Assert.AreEqual(text, dialogs.Find(".mud-dialog-content").TextContent.Trim(), "The dialog should name the profile as the profile switch names it");
        Assert.AreSequenceEqual(PageTestHost.Expect(cancel, confirm), ButtonLabels(dialogs.FindAll(".mud-dialog-actions button")), "The dialog should offer Cancel, then Delete");
        Assert.Contains(_firstProfile, ProfileIds(host), "Opening the dialog should delete nothing");
    }

    /// <summary>
    /// Confirming deletes the profile the dialog named when it opened, also after the page moved on to another profile,
    /// closes the dialog and goes to <c>/regions</c>; the other profile stays.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    [TestCategory("AC9")]
    public async Task RegionsDeleteConfirmedDeletesProfileOfDialog()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);
        await StoreNamedProfileAsync(host, _secondProfile, SecondProfileName, 10);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, UiLanguage.English);

        ClickDeleteHistory(page, UiLanguage.English);

        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 1, PageTestHost.WaitBound);

        var dialogText = dialogs.Find(".mud-dialog-content").TextContent.Trim();

        page.Render(parameters => parameters.Add(regions => regions.Profile, _secondProfile.ToString()));

        await page.WaitForAssertionAsync(() => Assert.AreEqual(SecondProfileName, ProfileLine(page), "The page should have moved on to the second profile"), PageTestHost.WaitBound);

        // Act
        ClickDialogButton(dialogs, "Delete");

        // Assert
        var navigation = host.Context.Services.GetRequiredService<BunitNavigationManager>();

        await page.WaitForAssertionAsync(() => Assert.AreEqual("regions", navigation.ToBaseRelativePath(navigation.Uri), "The page should go to /regions after the deletion"), PageTestHost.WaitBound);

        Assert.AreEqual(FirstProfileDialogText, dialogText, "The dialog should have named the first profile");
        Assert.AreSequenceEqual(new[] { _secondProfile }, ProfileIds(host), "Only the profile the dialog named should be deleted");
        Assert.IsEmpty(dialogs.FindAll(".mud-dialog"), "The dialog should be closed after the deletion");
    }

    /// <summary>
    /// Cancelling closes the dialog without deleting or navigating.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsDeleteCancelledDeletesNothing()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, UiLanguage.English);

        ClickDeleteHistory(page, UiLanguage.English);

        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 1, PageTestHost.WaitBound);

        // Act
        ClickDialogButton(dialogs, "Cancel");

        // Assert
        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 0, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(new[] { _firstProfile }, ProfileIds(host), "Cancelling should delete nothing");
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "Cancelling should not navigate");
        Assert.HasCount(1, page.FindAll("tbody tr"), "The page should still show the profile's regions");
    }

    /// <summary>
    /// A deletion that fails keeps the dialog open with its text and the failure text of spec E9 in a second paragraph,
    /// keeps the profile and does not navigate.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsDeleteFailedShowsFailureText()
    {
        // Arrange
        var databaseFile = CreateDatabaseFile();

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, null, databaseFile);

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);
        ExecuteOn(databaseFile, CreateFailingDeleteTriggerSql);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, UiLanguage.English);

        ClickDeleteHistory(page, UiLanguage.English);

        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 1, PageTestHost.WaitBound);

        // Act
        ClickDialogButton(dialogs, "Delete");

        // Assert
        await dialogs.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(FirstProfileDialogText, DeleteFailedText), ParagraphTexts(dialogs), "The dialog should show its text and then the failure text"), PageTestHost.WaitBound);

        Assert.HasCount(1, dialogs.FindAll(".mud-dialog"), "The dialog should stay open after a failure");
        Assert.AreSequenceEqual(new[] { _firstProfile }, ProfileIds(host), "A failed deletion should keep the profile");
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "A failed deletion should not navigate");
    }

    /// <summary>
    /// On a page of a host the host rule does not trust, the button is disabled and followed by the refusal text in the
    /// language shown, and clicking it opens no dialog and deletes nothing.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="refusal">The refusal text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(UiLanguage.English, "Deleting needs this page opened by IP address or as localhost, or by a host name the setting AllowedHosts lists.")]
    [DataRow(UiLanguage.German, "Löschen setzt voraus, dass diese Seite über die IP-Adresse oder localhost geöffnet wurde, oder über einen Hostnamen, den die Einstellung AllowedHosts nennt.")]
    public async Task RegionsUntrustedHostDisablesButtonAndIgnoresEvent(UiLanguage language, string refusal)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.DeletionAllowed = false;

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, language);
        var buttons = Buttons(page, UiTexts.For(language).DeleteHistory);

        Assert.HasCount(1, buttons, "The page should show the Delete history button, disabled");

        // Act
        TryClick(buttons[0]);

        // Assert
        Assert.IsTrue(buttons[0].HasAttribute("disabled"), "The button should be disabled on a page of an untrusted host");
        Assert.Contains(refusal, PageTestHost.Text(page), "The page should show the refusal text");
        Assert.IsEmpty(dialogs.FindAll(".mud-dialog"), "Clicking the disabled button should open no dialog");
        Assert.AreSequenceEqual(new[] { _firstProfile }, ProfileIds(host), "Nothing should be deleted");
    }

    /// <summary>
    /// On a page of an untrusted host, a click event that reaches the disabled button's handler anyway, as a crafted
    /// event of the circuit could, opens no dialog and deletes nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsUntrustedHostEventAtButtonHandlerOpensNoDialog()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.DeletionAllowed = false;

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = await RenderReadyAsync(host, _firstProfile, UiLanguage.English);
        var button = page.FindComponents<MudButton>().Single();

        // Act
        await page.InvokeAsync(() => button.Instance.OnClick.InvokeAsync(new MouseEventArgs()));

        // Assert
        Assert.IsTrue(button.Instance.Disabled, "The handler should belong to the disabled Delete history button");
        Assert.IsEmpty(dialogs.FindAll(".mud-dialog"), "An event at the handler of an untrusted page should open no dialog");
        Assert.AreSequenceEqual(new[] { _firstProfile }, ProfileIds(host), "Nothing should be deleted");
    }

    /// <summary>
    /// The button opened while the page still waits for its read names the profile in the dialog as the profile switch
    /// names it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsDeleteDialogWhileLoadingNamesProfileAsSwitch()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        await host.StoreProfileAsync(_firstProfile, FirstProfileName);

        var dialogs = host.Context.Render<MudDialogProvider>();
        var page = RenderPicked(host, _firstProfile);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);
        await page.WaitForAssertionAsync(() => Assert.Contains("Loading…", PageTestHost.Text(page), "The page should still wait for its read"), PageTestHost.WaitBound);

        // Act
        ClickDeleteHistory(page, UiLanguage.English);

        // Assert
        await dialogs.WaitForStateAsync(() => dialogs.FindAll(".mud-dialog").Count == 1, PageTestHost.WaitBound);

        var text = dialogs.Find(".mud-dialog-content").TextContent.Trim();

        source.ReleaseReads();

        Assert.AreEqual(FirstProfileDialogText, text, "The dialog should name the profile as the profile switch names it, before the page shows it");
    }

    /// <summary>
    /// The overview without a profile in its address, which follows the newest profile, offers no <c>Delete history</c>
    /// button and no refusal text.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsNewestAddressOffersNoDeleteHistory()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);

        // Act
        var page = RenderUnpicked(host, UiLanguage.English);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.IsEmpty(Buttons(page, "Delete history"), "The overview of the newest profile should offer no Delete history button");
        Assert.DoesNotContain("Deleting needs this page", PageTestHost.Text(page), "The overview of the newest profile should show no refusal text");
    }

    /// <summary>
    /// A page that shows a profile shows the not-found text and no table once that profile is deleted.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task RegionsDeletedProfileShowsNotFound()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await StoreNamedProfileAsync(host, _firstProfile, FirstProfileName, 0);
        await StoreNamedProfileAsync(host, _secondProfile, SecondProfileName, 10);

        var page = await RenderReadyAsync(host, _firstProfile, UiLanguage.English);
        var retention = host.Context.Services.GetRequiredService<HistoryRetention>();

        // Act
        var outcome = await retention.DeleteProfileAsync(_firstProfile, TestContext.CancellationToken);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Not found.", PageTestHost.Text(page), "The page of the deleted profile should show the not-found text"), PageTestHost.WaitBound);

        Assert.AreEqual(HistoryOutcome.Done, outcome, "The deletion should be done");
        Assert.IsEmpty(page.FindAll("table"), "The page of the deleted profile should show no table");
    }

    /// <summary>
    /// The overview lists the profile's regions ordered by name with their island count and key figures, each region a
    /// link to its island list.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task RegionsShowsRegionsWithFiguresAndLinks()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_firstProfile,
                                                     0,
                                                     PageTestHost.Island(SecondSession, 1, "Isle B", PageTestHost.Entry(1, 1f, 2f, -1f, 2, 50f), PageTestHost.Entry(2, 1f, 0f, 1f, 0, 50f)),
                                                     PageTestHost.Island(FirstSession, 1, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0.5f, 3, 100f))));

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 2, PageTestHost.WaitBound);

        Assert.AreEqual("Regions", page.Find("h1").TextContent.Trim(), "The heading should name the overview");
        Assert.AreSequenceEqual(PageTestHost.Expect("Region", "Islands", "Buildings", "Produced goods", "Goods in deficit"), PageTestHost.Headers(page), "The table should have the overview's columns in order");
        Assert.AreSequenceEqual(PageTestHost.Expect("Region 3245|1|3|1|0", "Region 6627|1|2|1|1"), PageTestHost.Describe(page), "The regions should be listed by name with their figures");
        Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession), RegionLink(_firstProfile, SecondSession)), PageTestHost.RowLinks(page), "Each region should link to its island list");
    }

    /// <summary>
    /// A region whose islands have no values in the latest tick keeps its island count and shows a dash for its figures.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task RegionsRegionWithoutValuesShowsDash()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_firstProfile, 0, PageTestHost.Island(FirstSession, 1, "Isle A", PageTestHost.Entry(1, 1f, 1f, -1f, 3, 100f))),
                                   PageTestHost.Tick(_firstProfile, 10));

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreSequenceEqual(new[] { $"Region 3245|1|{Dash}|{Dash}|{Dash}" }, PageTestHost.Describe(page), "A region without values should keep its island count and show dashes");
    }

    /// <summary>
    /// A stored tick of the shown profile replaces the shown figures without a navigation.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionsTickOfShownProfileUpdatesWithoutReload()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 10, 5));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Region 3245|1|5|1|0"), PageTestHost.Describe(page), "The page should show the figures of the new tick"), PageTestHost.WaitBound);
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "The update should not navigate or reload");
    }

    /// <summary>
    /// A tick of another profile leaves a picked profile shown, even when that tick is the newest.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionsTickOfOtherProfileKeepsPickedProfile()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(SecondSessionTick(_secondProfile, 10), FirstSessionTick(_firstProfile, 20, 5));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Region 3245|1|5|1|0"), PageTestHost.Describe(page), "The picked profile should stay shown with its newest figures"), PageTestHost.WaitBound);
        Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession)), PageTestHost.RowLinks(page), "The links should stay with the picked profile");
    }

    /// <summary>
    /// A stored catalog renames the shown regions without a navigation.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionsCatalogChangeUpdatesNames()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        await host.StoreCatalogAsync(PageTestHost.RegionEntry(FirstSession, 900001, "Latium", "Latium DE"));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Latium|1|3|1|0"), PageTestHost.Describe(page), "The region should take the catalog's name"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// The overview without a picked profile shows the profile whose latest tick is newest and follows a newer one.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task RegionsWithoutPickFollowsNewestProfile()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderUnpicked(host, UiLanguage.English);

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession)), PageTestHost.RowLinks(page), "The overview should show the only profile"), PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(SecondSessionTick(_secondProfile, 10));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_secondProfile, SecondSession)), PageTestHost.RowLinks(page), "The overview should follow the profile with the newest tick"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A profile picked in the address is shown, although another profile's tick is newer.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task RegionsPickedProfileComesFromAddress()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3), SecondSessionTick(_secondProfile, 10));

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession)), PageTestHost.RowLinks(page), "The page should show the profile of its address"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// Without any profile the overview shows the no-data text and no table.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="expected">The text of the spec</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(UiLanguage.English, "No data yet. The pages fill once the Companion sends data.")]
    [DataRow(UiLanguage.German, "Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.")]
    public async Task RegionsWithoutDataShowsNoDataText(UiLanguage language, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var page = RenderUnpicked(host, language);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains(expected, PageTestHost.Text(page), "The overview should show the no-data text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "The overview should show no table without data");
    }

    /// <summary>
    /// A profile that has only said hello, without a stored tick, shows the no-data text.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsProfileWithoutTickShowsNoDataText()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreProfileAsync(_firstProfile, "Hello only");

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("No data yet. The pages fill once the Companion sends data.", PageTestHost.Text(page), "A profile without a tick should show the no-data text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "A profile without a tick should show no table");
    }

    /// <summary>
    /// An unknown or malformed profile in the address shows the not-found text, without a table and without echoing the
    /// address.
    /// </summary>
    /// <param name="segment">The repeated part of the address segment</param>
    /// <param name="repeat">The number of repetitions</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("3f2504e0-4f89-41d3-9a0c-0305e82c3301", 1)]
    [DataRow("x", 1)]
    [DataRow("a", 2000)]
    [DataRow("%3Cb%3E", 1)]
    public async Task RegionsUnknownOrMalformedAddressShowsNotFound(string segment, int repeat)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var address = string.Concat(Enumerable.Repeat(segment, repeat));

        // Act
        var page = host.Render<Regions>(UiLanguage.English, parameters => parameters.Add(regions => regions.Profile, address));

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Not found.", PageTestHost.Text(page), "An unknown or malformed address should show the not-found text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "An unknown or malformed address should show no table");
        Assert.DoesNotContain(address, PageTestHost.Text(page), "The page should not echo the address");
    }

    /// <summary>
    /// A failed read shows the reload text and no table.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsReadFailureShowsReloadText()
    {
        // Arrange
        using var source = new FakeProfileStateSource(false);

        source.Store(_firstProfile,
                     new ProfileStateRead
                     {
                         State = null,
                         Failure = new SqliteException("test read failure", 1)
                     });

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("The data cannot be read right now. Reload the page to try again.", PageTestHost.Text(page), "A failed read should show the reload text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "A failed read should show no table");
    }

    /// <summary>
    /// A page waiting for the read of a profile not held shows the loading text, then the figures.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsWaitingReadShowsLoading()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        // Act
        var page = RenderPicked(host, _firstProfile);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.Contains("Loading…", PageTestHost.Text(page), "A waiting page should show the loading text"), PageTestHost.WaitBound);
        Assert.IsEmpty(page.FindAll("table"), "A waiting page with nothing shown should show no table");

        source.ReleaseReads();

        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession)), PageTestHost.RowLinks(page), "The page should show the figures once the read ended"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A disposed page cancels its read in flight, so the next read through the gate starts without waiting for it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task RegionsDisposedPageCancelsItsRead()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));
        source.Store(_secondProfile, StoredRead(CreateState(_secondProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        RenderPicked(host, _firstProfile);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await host.Context.DisposeComponentsAsync();

        // Assert
        var laterRead = host.Context.Services.GetRequiredService<PageReader>().ReadAsync(_secondProfile, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { _firstProfile, _secondProfile }, source.Reads.ToList(), "The next read should start once the disposed page's read was cancelled");

        source.ReleaseReads();

        var read = await laterRead.WaitAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.AreEqual(_secondProfile, read.View?.ProfileId, "The next read should give its profile's view");
    }

    /// <summary>
    /// Once the overview shows its figures, no reference to the profile state it read stays reachable from it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task RegionsPageHoldsNoState()
    {
        // Arrange
        using var source = new FakeProfileStateSource(false);

        var state = StoreUnreachableState(source, _firstProfile);

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        source.Forget(_firstProfile);
#pragma warning disable S1215 // The test checks that the page leaves the state collectable, which only a collection shows
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#pragma warning restore S1215

        // Assert
        Assert.IsFalse(state.IsAlive, "The page should hold no reference to the profile state it read");
        Assert.HasCount(1, page.FindAll("tbody tr"), "The page should still show its figures");
    }

    /// <summary>
    /// A region name with markup shows its characters in the table, not markup.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task RegionsRegionNameWithMarkupShowsCharacters()
    {
        // Arrange
        const string name = "<b>Latium</b> & <i>co</i>";

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreCatalogAsync(PageTestHost.RegionEntry(FirstSession, 900001, name, name));
        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual(name, PageTestHost.Rows(page)[0][0], "The region name should show its characters");
        Assert.IsEmpty(page.FindAll("tbody b, tbody i"), "The region name should not be rendered as markup");
    }

    /// <summary>
    /// A new profile in the address while the read of the old one is in flight discards that read: the page never shows the
    /// old profile and ends at the new one after one more read.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task RegionsAddressChangedDuringReadShowsOnlyNewProfile()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));
        source.Store(_secondProfile, StoredRead(CreateState(_secondProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderPicked(host, _firstProfile);
        var markups = new ConcurrentQueue<string>();

        page.OnMarkupUpdated += (_, _) => markups.Enqueue(page.Markup);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        page.Render(parameters => parameters.Add(regions => regions.Profile, _secondProfile.ToString()));
        source.ReleaseReads();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_secondProfile, FirstSession)), PageTestHost.RowLinks(page), "The page should show the profile of its new address"), PageTestHost.WaitBound);

        Assert.AreSequenceEqual(new[] { _firstProfile, _secondProfile }, source.Reads.ToList(), "The page should read the new profile once the old read ended");
        Assert.IsEmpty(markups.Where(markup => markup.Contains(RegionLink(_firstProfile, FirstSession), StringComparison.Ordinal)).ToList(), "The page should never show the profile of its old address");
    }

    /// <summary>
    /// A tick of the shown profile stored while its read is in flight causes one more read once that read ended.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionsTickDuringReadReadsOnceMore()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderPicked(host, _firstProfile);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 10, 5));
        source.ReleaseReads();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.HasCount(2, source.Reads, "The tick during the read should cause one more read"), PageTestHost.WaitBound);
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect(RegionLink(_firstProfile, FirstSession)), PageTestHost.RowLinks(page), "The page should show its profile after the reads"), PageTestHost.WaitBound);
        Assert.AreSequenceEqual(new[] { _firstProfile, _firstProfile }, source.Reads.ToList(), "Both reads should read the shown profile");
    }

    /// <summary>
    /// A Companion notice changes the banner of an open page without a read; a later catalog notice reads again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task RegionsCompanionNoticeChangesBannerWithoutRead()
    {
        // Arrange
        using var source = new FakeProfileStateSource(false);

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        Assert.AreEqual("The Companion is not connected.", page.Find(".mud-alert").TextContent.Trim(), "The page should start with the banner of no Companion");

        // Act
        host.State.SetCompanionConnected();

        // Assert
        await page.WaitForAssertionAsync(() => Assert.IsEmpty(page.FindAll(".mud-alert"), "The banner should go once the Companion is connected"), PageTestHost.WaitBound);

        await host.StoreCatalogAsync(PageTestHost.RegionEntry(FirstSession, 900001, "Latium", "Latium"));

        await page.WaitForAssertionAsync(() => Assert.HasCount(2, source.Reads, "The catalog notice should cause one more read"), PageTestHost.WaitBound);
        Assert.AreSequenceEqual(new[] { _firstProfile, _firstProfile }, source.Reads.ToList(), "Only the first read and the catalog's should read, never the Companion notice");
    }

    /// <summary>
    /// A hello that renames the shown profile updates the profile line of the open page without a reload.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC6")]
    public async Task RegionsHelloRenameUpdatesProfileLine()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreProfileAsync(_firstProfile, "Old name");
        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForAssertionAsync(() => Assert.AreEqual("Old name", ProfileLine(page), "The page should show the stored profile name"), PageTestHost.WaitBound);

        // Act
        await host.StoreProfileAsync(_firstProfile, "New name");

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreEqual("New name", ProfileLine(page), "The page should show the new profile name without a reload"), PageTestHost.WaitBound);
        Assert.IsEmpty(host.Context.Services.GetRequiredService<BunitNavigationManager>().History, "The update should not navigate or reload");
    }

    /// <summary>
    /// After a failed read, neither a catalog notice nor a tick of the profile reads again: the reload text stays until the
    /// page is reloaded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task RegionsReadFailureIsNotRetriedOnNotice()
    {
        // Arrange
        using var source = new FakeProfileStateSource(false);

        source.Store(_firstProfile,
                     new ProfileStateRead
                     {
                         State = null,
                         Failure = new SqliteException("test read failure", 1)
                     });
        source.Store(_secondProfile, StoredRead(CreateState(_secondProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var page = RenderPicked(host, _firstProfile);
        var witness = RenderPicked(host, _secondProfile);

        await page.WaitForAssertionAsync(() => Assert.Contains(ReloadText, PageTestHost.Text(page), "The failed read should show the reload text"), PageTestHost.WaitBound);
        await witness.WaitForStateAsync(() => witness.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        // Act
        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 10, 5));
        await host.StoreCatalogAsync(PageTestHost.RegionEntry(FirstSession, 900001, "Latium", "Latium"));

        // Assert
        await witness.WaitForAssertionAsync(() => Assert.HasCount(2, source.Reads.Where(read => read == _secondProfile).ToList(), "The witness page should read again on the catalog notice"), PageTestHost.WaitBound);

        Assert.HasCount(1, source.Reads.Where(read => read == _firstProfile).ToList(), "The failed page should not read again on a notice");
        Assert.Contains(ReloadText, PageTestHost.Text(page), "The reload text should stay");
    }

    /// <summary>
    /// An aborted request of a prerendered page cancels the page's read in flight, so the next read through the gate starts
    /// without waiting for it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task RegionsAbortedPrerenderCancelsItsRead()
    {
        // Arrange
        using var source = new FakeProfileStateSource(true);
        using var abort = new CancellationTokenSource();

        source.Store(_firstProfile, StoredRead(CreateState(_firstProfile)));
        source.Store(_secondProfile, StoredRead(CreateState(_secondProfile)));

        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken, source);

        var request = new DefaultHttpContext
                      {
                          RequestAborted = abort.Token
                      };

        host.Context.Render<Regions>(parameters => parameters.AddCascadingValue(UiLanguage.English)
                                                             .AddCascadingValue(UiTexts.For(UiLanguage.English))
                                                             .AddCascadingValue<HttpContext>(request)
                                                             .Add(regions => regions.Profile, _firstProfile.ToString()));

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        // Act
        await abort.CancelAsync();

        // Assert
        var laterRead = host.Context.Services.GetRequiredService<PageReader>().ReadAsync(_secondProfile, null, TestContext.CancellationToken);

        await source.WaitForReadAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { _firstProfile, _secondProfile }, source.Reads.ToList(), "The next read should start once the aborted page's read was cancelled");

        source.ReleaseReads();

        var read = await laterRead.WaitAsync(PageTestHost.WaitBound, TestContext.CancellationToken);

        Assert.AreEqual(_secondProfile, read.View?.ProfileId, "The next read should give its profile's view");
    }

    /// <summary>
    /// Building counts whose sum passes <see cref="int.MaxValue" /> show their whole sum on the overview.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task RegionsBuildingCountsPastInt32MaxShowWholeSum()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(PageTestHost.Tick(_firstProfile,
                                                     0,
                                                     PageTestHost.Island(FirstSession, 1, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0.5f, int.MaxValue, 100f)),
                                                     PageTestHost.Island(FirstSession, 2, "Isle B", PageTestHost.Entry(1, 1f, 1f, 0.5f, int.MaxValue, 100f))));

        // Act
        var page = RenderPicked(host, _firstProfile);

        // Assert
        await page.WaitForAssertionAsync(() => Assert.AreSequenceEqual(PageTestHost.Expect("Region 3245|2|4294967294|2|0"), PageTestHost.Describe(page), "The region should show the whole sum of its buildings"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A page disposed a second time does nothing, although its cancellation source is already disposed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    public async Task RegionsDisposedTwiceDoesNothing()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        await host.StoreTicksAsync(FirstSessionTick(_firstProfile, 0, 3));

        var page = RenderPicked(host, _firstProfile);

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        var instance = page.Instance;

        await host.Context.DisposeComponentsAsync();

        // Act
        var failure = Capture(instance.Dispose);

        // Assert
        Assert.IsNull(failure, "A second dispose should neither cancel nor dispose the disposed cancellation source again");
    }

    /// <summary>
    /// Runs an action and gives the <see cref="ObjectDisposedException" /> it threw.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>The exception, or <see langword="null" /> when the action returned</returns>
    private static ObjectDisposedException? Capture(Action action)
    {
        try
        {
            action();

            return null;
        }
        catch (ObjectDisposedException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Gives the text of the profile line under the heading.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <returns>The text, or an empty text while there is none</returns>
    private static string ProfileLine(IRenderedComponent<Regions> page)
    {
        return page.FindAll("h1 + p").Select(line => line.TextContent.Trim()).FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Renders the overview of a picked profile in English.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="profileId">The picked profile</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<Regions> RenderPicked(PageTestHost host, Guid profileId)
    {
        return host.Render<Regions>(UiLanguage.English, parameters => parameters.Add(regions => regions.Profile, profileId.ToString()));
    }

    /// <summary>
    /// Renders the overview without a picked profile.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static IRenderedComponent<Regions> RenderUnpicked(PageTestHost host, UiLanguage language)
    {
        return host.Render<Regions>(language,
                                    _ =>
                                    {
                                    });
    }

    /// <summary>
    /// Creates a tick with one island in the first session: one entry with the given building count and a positive delta.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds</param>
    /// <param name="buildings">The building count</param>
    /// <returns>The tick</returns>
    private static ContractTick FirstSessionTick(Guid profileId, long receiveSeconds, int buildings)
    {
        return PageTestHost.Tick(profileId, receiveSeconds, PageTestHost.Island(FirstSession, 1, "Isle A", PageTestHost.Entry(1, 1f, 1f, 0.5f, buildings, 100f)));
    }

    /// <summary>
    /// Creates a tick with one island in the second session.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds</param>
    /// <returns>The tick</returns>
    private static ContractTick SecondSessionTick(Guid profileId, long receiveSeconds)
    {
        return PageTestHost.Tick(profileId, receiveSeconds, PageTestHost.Island(SecondSession, 1, "Isle B", PageTestHost.Entry(1, 1f, 1f, 0.5f, 1, 100f)));
    }

    /// <summary>
    /// Creates a state of a profile with one island in the first session.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The state</returns>
    private static StoredProfileState CreateState(Guid profileId)
    {
        var tick = FirstSessionTick(profileId, 0, 3);

        return new StoredProfileState
               {
                   LatestTradeRoutes = null,
                   Profile = new StoredProfile
                             {
                                 Id = profileId,
                                 Name = "Profile " + profileId.ToString()[..4]
                             },
                   LatestTick = tick,
                   Islands = [
                                 new StoredIsland
                                 {
                                     SessionGuid = FirstSession,
                                     IslandId = 1,
                                     AreaIndex = 0,
                                     Name = "Isle A"
                                 }
                             ]
               };
    }

    /// <summary>
    /// Gives the read result of a stored state.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The read result</returns>
    private static ProfileStateRead StoredRead(StoredProfileState state)
    {
        return new ProfileStateRead
               {
                   State = state,
                   Failure = null
               };
    }

    /// <summary>
    /// Stores a state in the source that only the source references, and gives a weak reference to it.
    /// </summary>
    /// <param name="source">The source</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The weak reference to the state</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference StoreUnreachableState(FakeProfileStateSource source, Guid profileId)
    {
        var state = CreateState(profileId);

        source.Store(profileId, StoredRead(state));

        return new WeakReference(state);
    }

    /// <summary>
    /// Gives the buttons whose trimmed text is a label.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="rendered">The rendered component</param>
    /// <param name="label">The label</param>
    /// <returns>The buttons</returns>
    private static List<IElement> Buttons<TComponent>(IRenderedComponent<TComponent> rendered, string label)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        return [.. rendered.FindAll("button").Where(button => button.TextContent.Trim() == label)];
    }

    /// <summary>
    /// Gives the trimmed texts of buttons.
    /// </summary>
    /// <param name="buttons">The buttons</param>
    /// <returns>The texts</returns>
    private static List<string> ButtonLabels(IEnumerable<IElement> buttons)
    {
        return [.. buttons.Select(button => button.TextContent.Trim())];
    }

    /// <summary>
    /// Gives the trimmed texts of the paragraphs of the open dialog's content.
    /// </summary>
    /// <param name="dialogs">The rendered dialog provider</param>
    /// <returns>The texts</returns>
    private static List<string> ParagraphTexts(IRenderedComponent<MudDialogProvider> dialogs)
    {
        return [.. dialogs.FindAll(".mud-dialog-content p").Select(paragraph => paragraph.TextContent.Trim())];
    }

    /// <summary>
    /// Clicks the page's one <c>Delete history</c> button in a language, failing when the page offers none.
    /// </summary>
    /// <param name="page">The rendered page</param>
    /// <param name="language">The language shown</param>
    private static void ClickDeleteHistory(IRenderedComponent<Regions> page, UiLanguage language)
    {
        var buttons = Buttons(page, UiTexts.For(language).DeleteHistory);

        Assert.HasCount(1, buttons, "The page should offer one Delete history button");

        buttons[0].Click();
    }

    /// <summary>
    /// Clicks the one button of the open dialog with a label, failing when there is none.
    /// </summary>
    /// <param name="dialogs">The rendered dialog provider</param>
    /// <param name="label">The label</param>
    private static void ClickDialogButton(IRenderedComponent<MudDialogProvider> dialogs, string label)
    {
        var buttons = Buttons(dialogs, label);

        Assert.HasCount(1, buttons, $"The dialog should offer one {label} button");

        buttons[0].Click();
    }

    /// <summary>
    /// Clicks an element and ignores that it has no click handler, as a disabled button may render none.
    /// </summary>
    /// <param name="element">The element</param>
    private static void TryClick(IElement element)
    {
        try
        {
            element.Click();
        }
        catch (MissingEventHandlerException)
        {
            // A disabled button may carry no click handler: the click does nothing, which is what the test expects
        }
    }

    /// <summary>
    /// Gives the identities of the profiles the state lists, in its order.
    /// </summary>
    /// <param name="host">The host</param>
    /// <returns>The identities</returns>
    private static List<Guid> ProfileIds(PageTestHost host)
    {
        return [.. host.State.Profiles.Select(profile => profile.Id)];
    }

    /// <summary>
    /// Runs a constant statement of this class over a connection of its own, without the pool, to a SQLite file.
    /// </summary>
    /// <param name="databaseFile">The file</param>
    /// <param name="sql">The statement</param>
    private static void ExecuteOn(string databaseFile, string sql)
    {
        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };

        using var connection = new SqliteConnection(builder.ToString());

        connection.Open();

        using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Constant statements of this class only
        command.CommandText = sql;
#pragma warning restore CA2100

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Stores a profile with a name, when one is given, and a tick of it in the first session.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The name, or <see langword="null" /> for none</param>
    /// <param name="receiveSeconds">The receive time of the tick in seconds</param>
    /// <returns>A task that completes when the profile and its tick are stored</returns>
    private static async Task StoreNamedProfileAsync(PageTestHost host, Guid profileId, string? name, long receiveSeconds)
    {
        if (name is not null)
        {
            await host.StoreProfileAsync(profileId, name);
        }

        await host.StoreTicksAsync(FirstSessionTick(profileId, receiveSeconds, 3));
    }

    /// <summary>
    /// Renders the overview of a profile and waits until it shows its regions.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="profileId">The profile</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered page</returns>
    private static async Task<IRenderedComponent<Regions>> RenderReadyAsync(PageTestHost host, Guid profileId, UiLanguage language)
    {
        var page = host.Render<Regions>(language, parameters => parameters.Add(regions => regions.Profile, profileId.ToString()));

        await page.WaitForStateAsync(() => page.FindAll("tbody tr").Count == 1, PageTestHost.WaitBound);

        return page;
    }

    /// <summary>
    /// Gives the address of a region's island list.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="sessionGuid">The smallest session GUID of the region</param>
    /// <returns>The address</returns>
    private static string RegionLink(Guid profileId, int sessionGuid)
    {
        return $"/profiles/{profileId}/sessions/{sessionGuid}";
    }

    /// <summary>
    /// Creates the path of a database file in a new temporary directory.
    /// </summary>
    /// <returns>The full path of the database file, not yet created</returns>
    private string CreateDatabaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-regions-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);

        return Path.Combine(directory, "censio.db");
    }

    #endregion // Methods
}