using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using Censio.Contracts;
using Censio.Server.Data;
using Censio.Server.Data.Tests;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the HTTP surface of <see cref="Program" />.
/// </summary>
[TestClass]
public sealed class ProgramTests
{
    #region Fields

    /// <summary>
    /// The pattern of a URL that names a scheme or another host.
    /// </summary>
    private static readonly Regex _foreignUrl = new("^([A-Za-z][A-Za-z0-9+.-]*:|//|\\\\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The pattern of the own style sheet, fingerprinted or not.
    /// </summary>
    private static readonly Regex _censioStyleSheet = new("(^|/)css/censio(\\.[A-Za-z0-9]+)?\\.css$", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The pattern of the Blazor script element on the start page.
    /// </summary>
    private static readonly Regex _blazorScript = new("<script[^>]*\\ssrc=\"([^\"]*_framework/blazor\\.web(\\.[A-Za-z0-9]+)?\\.js)\"", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The expression the release check applies to each line of the start page (<c>sed 's|.*&lt;p&gt;Version \([^&lt;]*\)&lt;/p&gt;.*|\1|p'</c>).
    /// </summary>
    private static readonly Regex _releaseCheckVersion = new("^.*<p>Version ([^<]*)</p>.*$", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ProgramTests(TestContext testContext)
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
    /// The start page carries the title, the heading and the version.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        var expectedVersion = ProductVersion.Read(typeof(Program).Assembly);

        // Act
        using var response = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The start page should answer 200");
        Assert.IsTrue(Regex.IsMatch(body, "<title[^>]*>Censio</title>", RegexOptions.None, TimeSpan.FromSeconds(5)), "The title element should hold the text Censio");
        Assert.IsTrue(Regex.IsMatch(body, "<h1[^>]*>Censio</h1>", RegexOptions.None, TimeSpan.FromSeconds(5)), "The h1 element should hold the text Censio");
        Assert.Contains("Version " + expectedVersion, body, "The start page should show the version of the server assembly");
    }

    /// <summary>
    /// The script the start page references is served.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetRootReferencesServedBlazorScript()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        using var page = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await page.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var match = _blazorScript.Match(body);

        // Act
        using var script = await client.GetAsync(match.Groups[1].Value, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(match.Success, "The start page should reference _framework/blazor.web.js, fingerprinted or not");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, script.StatusCode, "The referenced Blazor script should answer 200");
    }

    /// <summary>
    /// The start page contains the marker of an interactive server component.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramGetRootRendersInteractiveServerMarker()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.Contains("<!--Blazor:{\"type\":\"server\"", body, "The page should contain the prerender marker of the interactive server render mode");
    }

    /// <summary>
    /// The health endpoint answers Healthy as plain text.
    /// </summary>
    /// <param name="environment">The hosting environment</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC12")]
    [DataRow("Production")]
    [DataRow("Development")]
    public async Task ProgramGetHealthReturnsHealthyPlainText(string environment)
    {
        // Arrange
        using var factory = new CensioServerFactory(environment);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The health endpoint should answer 200");
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType, "The health endpoint should answer text/plain");
        Assert.AreEqual("Healthy", body, "The health endpoint should answer exactly Healthy");
    }

    /// <summary>
    /// A request over HTTP is not redirected to HTTPS, even when an HTTPS port is known.
    /// </summary>
    /// <param name="path">The requested path</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/health")]
    public async Task ProgramGetOverHttpWithHttpsPortDoesNotRedirect(string path)
    {
        // Arrange
        var settings = new Dictionary<string, string?>
                       {
                           ["https_port"] = "443"
                       };
        using var factory = new CensioServerFactory("Production", settings);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                                                {
                                                    AllowAutoRedirect = false,
                                                    BaseAddress = new Uri("http://censio.example")
                                                });

        // Act
        using var response = await client.GetAsync(path, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The request over HTTP should be answered, not redirected");
        Assert.IsNull(response.Headers.Location, "The answer should carry no Location header");
    }

    /// <summary>
    /// A request over HTTPS gets no HSTS header.
    /// </summary>
    /// <param name="path">The requested path</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/health")]
    public async Task ProgramGetOverHttpsSendsNoHstsHeader(string path)
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                                                {
                                                    AllowAutoRedirect = false,
                                                    BaseAddress = new Uri("https://censio.example")
                                                });

        // Act
        using var response = await client.GetAsync(path, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The request over HTTPS should be answered");
        Assert.IsFalse(response.Headers.Contains("Strict-Transport-Security"), "The answer should carry no Strict-Transport-Security header");
    }

    /// <summary>
    /// An unknown path answers 404 with an empty body.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC12")]
    public async Task ProgramGetUnknownPathReturnsNotFound()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/does-not-exist", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode, "An unknown path should answer 404");
        Assert.AreEqual(string.Empty, body, "The 404 answer should have an empty body");
    }

    /// <summary>
    /// A first visit shows the frame in the light theme with the English texts, the navigation open and the start page's
    /// entry marked.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    public async Task ProgramGetRootShowsFrameInLightThemeWithEnglishTexts()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var (status, body) = await GetRootAsync(client, null, null);

        // Assert
        var document = ParseHtml(body);
        var homeEntry = document.QuerySelector("a.mud-nav-link[href='/']");

        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.AreEqual("en", document.DocumentElement.GetAttribute("lang"), "The page language should be English");
        Assert.Contains("Censio", document.QuerySelector(".mud-appbar")?.TextContent ?? string.Empty, "The app bar should show the name Censio");
        Assert.AreEqual(1, CountLabel(document, "Open or close the navigation"), "The app bar should hold the navigation button");
        Assert.AreEqual(1, CountLabel(document, "Switch to the dark theme"), "The app bar should hold the theme switch of the light theme");
        Assert.AreEqual(1, CountLabel(document, "Language"), "The app bar should hold the language switch");
        Assert.AreEqual("Profile", document.QuerySelector(".mud-appbar button[disabled]")?.TextContent.Trim(), "The app bar should hold the disabled profile switch");
        Assert.Contains("mud-drawer--open", document.QuerySelector(".mud-drawer")?.ClassList.ToArray() ?? [], "The navigation should be open");
        Assert.AreEqual("Home", homeEntry?.TextContent.Trim(), "The navigation should list the start page");
        Assert.Contains("active", homeEntry?.ClassList.ToArray() ?? [], "The start page's entry should be marked");
        Assert.Contains(PaletteBackground(UiTheme.Light), body, "The page should carry the light palette");
        Assert.DoesNotContain(PaletteBackground(UiTheme.Dark), body, "The page should not carry the dark palette");
    }

    /// <summary>
    /// The release check's expression finds the product version on exactly one line of the start page in the frame.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ProgramGetRootShowsVersionInReleaseCheckMarkup()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        var expectedVersion = ProductVersion.Read(typeof(Program).Assembly);

        // Act
        var (status, body) = await GetRootAsync(client, null, null);

        // Assert
        var versions = body.Split('\n')
                           .Select(line => _releaseCheckVersion.Match(line))
                           .Where(match => match.Success)
                           .Select(match => match.Groups[1].Value)
                           .ToList();

        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.IsNotNull(ParseHtml(body).QuerySelector(".mud-appbar"), "The start page should be shown in the frame");
        Assert.AreSequenceEqual(new[] { expectedVersion }, versions, "The release check's expression should find the product version on exactly one line");
    }

    /// <summary>
    /// A language list that ranks German first shows the frame in German.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ProgramGetRootWithGermanLanguageListShowsGermanTexts()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var (status, body) = await GetRootAsync(client, null, "de-DE,en;q=0.8");

        // Assert
        var document = ParseHtml(body);

        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.AreEqual("de", document.DocumentElement.GetAttribute("lang"), "The page language should be German");
        Assert.AreEqual(1, CountLabel(document, "Navigation öffnen oder schließen"), "The navigation button should be named in German");
        Assert.AreEqual(1, CountLabel(document, "Zum dunklen Design wechseln"), "The theme switch should be named in German");
        Assert.AreEqual(1, CountLabel(document, "Sprache"), "The language switch should be named in German");
        Assert.AreEqual("Profil", document.QuerySelector(".mud-appbar button[disabled]")?.TextContent.Trim(), "The profile switch should read Profil");
        Assert.AreEqual("Startseite", document.QuerySelector("a.mud-nav-link[href='/']")?.TextContent.Trim(), "The start page's entry should read Startseite");
    }

    /// <summary>
    /// A stored dark theme is in the HTML the server sends, so the page paints dark from the start.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task ProgramGetRootWithDarkCookieRendersDarkPalette()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var (status, body) = await GetRootAsync(client, $"{UiPreferences.ThemeCookieName}=dark", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.Contains(PaletteBackground(UiTheme.Dark), body, "The page should carry the dark palette");
        Assert.DoesNotContain(PaletteBackground(UiTheme.Light), body, "The page should not carry the light palette");
        Assert.AreEqual(1, CountLabel(ParseHtml(body), "Switch to the light theme"), "The theme switch should be the one of the dark theme");
    }

    /// <summary>
    /// A stored language decides over the language list.
    /// </summary>
    /// <param name="storedLanguage">The stored language</param>
    /// <param name="acceptLanguage">The language list</param>
    /// <param name="expectedTag">The expected page language</param>
    /// <param name="expectedHomeEntry">The expected text of the start page's entry</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("de", "en", "de", "Startseite")]
    [DataRow("en", "de", "en", "Home")]
    public async Task ProgramGetRootWithLanguageCookieOverridesLanguageList(string storedLanguage, string acceptLanguage, string expectedTag, string expectedHomeEntry)
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var (status, body) = await GetRootAsync(client, $"{UiPreferences.LanguageCookieName}={storedLanguage}", acceptLanguage);

        // Assert
        var document = ParseHtml(body);

        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.AreEqual(expectedTag, document.DocumentElement.GetAttribute("lang"), "The page language should be the stored one");
        Assert.AreEqual(expectedHomeEntry, document.QuerySelector("a.mud-nav-link[href='/']")?.TextContent.Trim(), "The texts should be in the stored language");
    }

    /// <summary>
    /// Unknown stored values give the defaults and appear neither in the page nor in a log entry, raw or decoded.
    /// </summary>
    /// <param name="themeValue">The theme cookie as sent</param>
    /// <param name="languageValue">The language cookie as sent</param>
    /// <param name="decodedThemeValue">The theme cookie after percent-decoding</param>
    /// <param name="decodedLanguageValue">The language cookie after percent-decoding</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("%0Adark", "%3Cscript%3Ecensio-probe%3C%2Fscript%3E", "\ndark", "<script>censio-probe</script>")]
    [DataRow("%3Cscript%3Ecensio-probe%3C%2Fscript%3E", "%0Ade-censio-probe", "<script>censio-probe</script>", "\nde-censio-probe")]
    [DataRow("DARK", "fr-censio-probe", "DARK", "fr-censio-probe")]
    public async Task ProgramGetRootWithUnknownCookiesShowsDefaultsWithoutEcho(string themeValue, string languageValue, string decodedThemeValue, string decodedLanguageValue)
    {
        // Arrange
        var loggerProvider = new RecordingLoggerProvider();
        using var baseFactory = new CensioServerFactory("Production");
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace)));
        using var client = factory.CreateClient();
        var sentValues = new[] { themeValue, languageValue, decodedThemeValue, decodedLanguageValue };

        // Act
        var (status, body) = await GetRootAsync(client, $"{UiPreferences.ThemeCookieName}={themeValue}; {UiPreferences.LanguageCookieName}={languageValue}", null);

        // Assert
        var messages = loggerProvider.Logger.Entries.Select(entry => entry.Message).ToList();

        Assert.AreEqual(HttpStatusCode.OK, status, "The start page should answer 200");
        Assert.AreEqual("en", ParseHtml(body).DocumentElement.GetAttribute("lang"), "The page language should be the default");
        Assert.Contains(PaletteBackground(UiTheme.Light), body, "The page should carry the light palette");
        Assert.DoesNotContain(PaletteBackground(UiTheme.Dark), body, "The page should not carry the dark palette");
        Assert.IsEmpty(sentValues.Where(value => body.Contains(value, StringComparison.Ordinal)), "The page should not show a sent value");
        Assert.IsEmpty(sentValues.Where(value => messages.Any(message => message.Contains(value, StringComparison.Ordinal))), "No log entry should hold a sent value");
    }

    /// <summary>
    /// Every resource the start page loads comes from the server itself and is served.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task ProgramGetRootLoadsResourcesOnlyFromOwnOrigin()
    {
        // Arrange
        using var factory = new CensioServerFactory("Production");
        using var client = factory.CreateClient();
        var (_, body) = await GetRootAsync(client, null, null);

        var urls = ResourceUrls(ParseHtml(body));

        // Act
        var resources = await FetchAsync(client, urls.Where(url => _foreignUrl.IsMatch(url) == false));

        // Assert
        var styleSheets = resources.Where(resource => resource.MediaType == "text/css").ToList();

        Assert.IsEmpty(urls.Where(url => _foreignUrl.IsMatch(url)), "Every resource should be named by a path of the server itself");
        Assert.IsEmpty(resources.Where(resource => resource.Status != HttpStatusCode.OK).Select(resource => resource.Url), "Every resource should be served");
        Assert.Contains(url => _censioStyleSheet.IsMatch(url), urls, "The page should load css/censio.css");
        Assert.IsEmpty(styleSheets.Where(sheet => NamesAnotherHost(sheet.Body)).Select(sheet => sheet.Url), "No style sheet should import or name another host");
        Assert.IsFalse(NamesAnotherHost(body), "The page's own styles should not import or name another host");
    }

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
    /// A configured data directory holds a key file once the server answers.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task ProgramStartWithDataDirectoryWritesKeyRing()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        using var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory));
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health", TestContext.CancellationToken);

        // Assert
        var keyFiles = Directory.GetFiles(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName), "key-*.xml");

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, "The health endpoint should answer 200");
        Assert.HasCount(1, keyFiles, "The start should have created exactly one key file");
    }

    /// <summary>
    /// A second start on the same data directory reads the ring of the first one.
    /// </summary>
    [TestMethod]
    public void ProgramRestartWithSameDataDirectoryUnprotectsEarlierPayload()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var settings = CreateDataDirectorySettings(dataDirectory);
        var protectedPayload = ProtectWithNewServer(settings, "payload");

        // Act
        var restored = UnprotectWithNewServer(settings, protectedPayload);

        // Assert
        var keyFiles = Directory.GetFiles(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName), "key-*.xml");

        Assert.AreEqual("payload", restored, "The second instance should unprotect the payload of the first one");
        Assert.HasCount(1, keyFiles, "The second start should create no further key file");
    }

    /// <summary>
    /// A token file that holds no valid token stops the start with exit code 1 before the server listens: the listen
    /// address is taken by another socket, so a start that went on to listen would fail with an exception instead.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task ProgramStartWithInvalidTokenFileExitsWithCodeOne()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        await File.WriteAllTextAsync(tokenFile, "not a valid token", TestContext.CancellationToken);

        using var blocker = new TcpListener(IPAddress.Loopback, 0);

        blocker.Start();

        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        string[] arguments = [$"--{DataProtectionSetup.DataDirectoryKey}={dataDirectory}", $"--urls=http://127.0.0.1:{port}", $"--applicationName={typeof(Program).Assembly.GetName().Name}"];

        // Act
        var exitCode = await Task.Run(() => entryPoint.Invoke(null, [arguments]), TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, exitCode, "The start should end with exit code 1");
        Assert.AreEqual("not a valid token", await File.ReadAllTextAsync(tokenFile, TestContext.CancellationToken), "The token file should stay as it was");
    }

    /// <summary>
    /// A token file setting that names a missing file stops the start with exit code 1 before the server listens, and
    /// neither the configured file nor a token file in the data directory is created: the listen address is taken by
    /// another socket, so a start that went on to listen would fail with an exception instead.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task ProgramStartWithMissingTokenFileExitsWithCodeOne()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var configuredFile = Path.Combine(CreateTemporaryDirectory(), "token");

        using var blocker = new TcpListener(IPAddress.Loopback, 0);

        blocker.Start();

        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        string[] arguments = [$"--{DataProtectionSetup.DataDirectoryKey}={dataDirectory}", $"--{IngestTokenSetup.TokenFileKey}={configuredFile}", $"--urls=http://127.0.0.1:{port}", $"--applicationName={typeof(Program).Assembly.GetName().Name}"];

        // Act
        var exitCode = await Task.Run(() => entryPoint.Invoke(null, [arguments]), TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, exitCode, "The start should end with exit code 1");
        Assert.IsFalse(File.Exists(configuredFile), "The configured file should not be created");
        Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName)), "No token file should be created in the data directory");
    }

    /// <summary>
    /// A start with a data directory creates <c>censio.db</c> there at the current schema: a later open of the file
    /// finds nothing to apply.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ProgramStartWithDataDirectoryCreatesDatabase()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName);

        // Act
        using (var factory = new CensioServerFactory("Production", CreateDataDirectorySettings(dataDirectory)))
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/health", TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "The health endpoint should answer 200");
        }

        SqliteConnection.ClearAllPools();

        // Assert
        Assert.IsTrue(File.Exists(databaseFile), "The start should have created censio.db in the data directory");

        var reopened = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);

        reopened.Database?.Dispose();
        SqliteConnection.ClearAllPools();

        Assert.AreEqual(ServerDatabaseOpenKind.Opened, reopened.Kind, "The created file should hold the current schema");
    }

    /// <summary>
    /// A database file that is not a Censio database stops the start with exit code 1 before the server listens, and the
    /// file stays as it was: the listen address is taken by another socket, so a start that went on to listen would fail
    /// with an exception instead.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ProgramStartWithUnreadableDatabaseExitsWithCodeOne()
    {
        // Arrange
        const string content = "This is a plain text file and not a database, longer than the header of one hundred bytes of a SQLite file.";

        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName);

        await File.WriteAllTextAsync(databaseFile, content, TestContext.CancellationToken);

        using var blocker = new TcpListener(IPAddress.Loopback, 0);

        blocker.Start();

        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        string[] arguments = [$"--{DataProtectionSetup.DataDirectoryKey}={dataDirectory}", $"--urls=http://127.0.0.1:{port}", $"--applicationName={typeof(Program).Assembly.GetName().Name}"];

        // Act
        var exitCode = await Task.Run(() => entryPoint.Invoke(null, [arguments]), TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, exitCode, "The start should end with exit code 1");
        Assert.AreEqual(content, await File.ReadAllTextAsync(databaseFile, TestContext.CancellationToken), "The database file should stay as it was");
    }

    /// <summary>
    /// A start whose database opens but whose stored state cannot be read (the tick table renamed) ends with exit code 1
    /// before the server listens.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ProgramStartWithUnreadableStateExitsWithCodeOne()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName);
        var opened = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);

        opened.Database?.Dispose();
        SqliteConnection.ClearAllPools();
        RenameTicks(databaseFile);

        using var blocker = new TcpListener(IPAddress.Loopback, 0);

        blocker.Start();

        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        string[] arguments = [$"--{DataProtectionSetup.DataDirectoryKey}={dataDirectory}", $"--urls=http://127.0.0.1:{port}", $"--applicationName={typeof(Program).Assembly.GetName().Name}"];

        // Act
        var exitCode = await Task.Run(() => entryPoint.Invoke(null, [arguments]), TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(ServerDatabaseOpenKind.Created, opened.Kind, "The database should be created before the test breaks its state");
        Assert.AreEqual(1, exitCode, "The start should end with exit code 1");
    }

    /// <summary>
    /// The committed settings files set no listen address.
    /// </summary>
    /// <param name="environment">The hosting environment</param>
    [TestMethod]
    [DataRow("Production")]
    [DataRow("Development")]
    public void ProgramProductionConfigurationSetsNoListenAddress(string environment)
    {
        // Arrange
        // Only the JSON sources are read (the committed appsettings*.json), never environment variables, which machines set.
        // That the server listens on loopback by default is not proven here but by starting the built server.
        using var factory = new CensioServerFactory(environment);
        var root = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        var forbidden = new[] { "urls", "http_ports", "https_ports" };

        // Act
        var jsonProviders = root.Providers.OfType<JsonConfigurationProvider>().ToList();
        var keys = jsonProviders.SelectMany(provider => provider.GetChildKeys([], null)).ToList();
        var endpoints = jsonProviders.Where(provider => provider.GetChildKeys([], "Kestrel").Contains("Endpoints", StringComparer.OrdinalIgnoreCase)).ToList();

        // Assert
        Assert.IsNotEmpty(jsonProviders, "The committed settings files should be loaded as JSON sources");

        foreach (var key in forbidden)
        {
            Assert.DoesNotContain(key, keys, StringComparer.OrdinalIgnoreCase, $"The committed settings should not set {key}");
        }

        Assert.IsEmpty(endpoints, "The committed settings should not set Kestrel:Endpoints");
    }

    /// <summary>
    /// Parses an HTML page.
    /// </summary>
    /// <param name="body">The HTML</param>
    /// <returns>The document</returns>
    private static IDocument ParseHtml(string body)
    {
        return new HtmlParser().ParseDocument(body);
    }

    /// <summary>
    /// Counts the buttons of a document with an accessible name.
    /// </summary>
    /// <param name="document">The document</param>
    /// <param name="label">The accessible name</param>
    /// <returns>The number of buttons</returns>
    private static int CountLabel(IDocument document, string label)
    {
        return document.QuerySelectorAll($"button[aria-label='{label}']").Length;
    }

    /// <summary>
    /// Gets the declaration of the background variable the theme provider writes for a theme's palette.
    /// </summary>
    /// <param name="theme">The theme</param>
    /// <returns>The declaration</returns>
    private static string PaletteBackground(UiTheme theme)
    {
        var mudTheme = CensioTheme.Create();
        var color = theme == UiTheme.Dark ? mudTheme.PaletteDark.Background : mudTheme.PaletteLight.Background;

        return string.Create(CultureInfo.InvariantCulture, $"--mud-palette-background: rgba({color.R},{color.G},{color.B},1)");
    }

    /// <summary>
    /// Gets the URLs of every style sheet, script and image a document loads.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The URLs</returns>
    private static List<string> ResourceUrls(IDocument document)
    {
        var links = document.QuerySelectorAll("link[href]").Select(element => element.GetAttribute("href"));
        var sources = document.QuerySelectorAll("script[src], img[src]").Select(element => element.GetAttribute("src"));

        return links.Concat(sources).OfType<string>().ToList();
    }

    /// <summary>
    /// Tells whether a style text imports another file or names a host.
    /// </summary>
    /// <param name="styles">The style text</param>
    /// <returns><c>true</c> when it holds <c>@import</c>, <c>url(http</c> or <c>url(//</c></returns>
    private static bool NamesAnotherHost(string styles)
    {
        return styles.Contains("@import", StringComparison.OrdinalIgnoreCase)
               || styles.Contains("url(http", StringComparison.OrdinalIgnoreCase)
               || styles.Contains("url(//", StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the settings that point the server to a data directory.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <returns>The settings</returns>
    private static Dictionary<string, string?> CreateDataDirectorySettings(string dataDirectory)
    {
        return new Dictionary<string, string?>
               {
                   [DataProtectionSetup.DataDirectoryKey] = dataDirectory
               };
    }

    /// <summary>
    /// Starts a server, protects a payload with it and stops it.
    /// </summary>
    /// <param name="settings">The settings of the server</param>
    /// <param name="payload">The payload</param>
    /// <returns>The protected payload</returns>
    private static string ProtectWithNewServer(Dictionary<string, string?> settings, string payload)
    {
        using var factory = new CensioServerFactory("Production", settings);

        return factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Censio.Tests.Restart").Protect(payload);
    }

    /// <summary>
    /// Starts a server, unprotects a payload with it and stops it.
    /// </summary>
    /// <param name="settings">The settings of the server</param>
    /// <param name="protectedPayload">The protected payload</param>
    /// <returns>The payload</returns>
    private static string UnprotectWithNewServer(Dictionary<string, string?> settings, string protectedPayload)
    {
        using var factory = new CensioServerFactory("Production", settings);

        return factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Censio.Tests.Restart").Unprotect(protectedPayload);
    }

    /// <summary>
    /// Renames the tick table of a database file over a connection of its own, so that the state cannot be restored.
    /// </summary>
    /// <param name="databaseFile">The database file</param>
    private static void RenameTicks(string databaseFile)
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

        command.CommandText = "ALTER TABLE \"Ticks\" RENAME TO \"TicksMoved\";";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Requests the start page with an optional cookie header and language list.
    /// </summary>
    /// <param name="client">The client</param>
    /// <param name="cookie">The value of the cookie header, or <c>null</c> for none</param>
    /// <param name="acceptLanguage">The value of the language header, or <c>null</c> for none</param>
    /// <returns>The status and the body of the answer</returns>
    private async Task<(HttpStatusCode Status, string Body)> GetRootAsync(HttpClient client, string? cookie, string? acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");

        if (cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        using var response = await client.SendAsync(request, TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        return (response.StatusCode, body);
    }

    /// <summary>
    /// Requests every resource.
    /// </summary>
    /// <param name="client">The client</param>
    /// <param name="urls">The URLs of the resources</param>
    /// <returns>The URL, status, media type and body of each answer</returns>
    private async Task<List<(string Url, HttpStatusCode Status, string? MediaType, string Body)>> FetchAsync(HttpClient client, IEnumerable<string> urls)
    {
        var resources = new List<(string Url, HttpStatusCode Status, string? MediaType, string Body)>();

        foreach (var url in urls)
        {
            using var response = await client.GetAsync(url, TestContext.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

            resources.Add((url, response.StatusCode, response.Content.Headers.ContentType?.MediaType, body));
        }

        return resources;
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);

        return path;
    }

    #endregion // Methods
}