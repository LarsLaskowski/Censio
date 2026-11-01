using System.Globalization;
using System.Text;

using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeRecordingFiles" />.
/// </summary>
[TestClass]
public sealed class PipeRecordingFilesTests
{
    #region Fields

    /// <summary>
    /// The instant the recordings of the tests start.
    /// </summary>
    private static readonly DateTimeOffset _started = new(2026, 11, 1, 17, 9, 0, 123, TimeSpan.Zero);

    /// <summary>
    /// The temporary folder of the test, which holds the recordings folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-recording-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The culture the test started with.
    /// </summary>
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Restores the culture and deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        CultureInfo.CurrentCulture = _culture;

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// The default folder is the Censio recordings folder in the local application data.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesDefaultFolderIsCensioRecordingsInLocalApplicationData()
    {
        // Arrange
        var data = Path.Combine(_root, "Local");

        // Act
        var folder = PipeRecordingFiles.DefaultFolder(data);

        // Assert
        Assert.AreEqual(Path.Combine(data, "Censio", "Recordings"), folder, "The folder should be Censio\\Recordings below the local application data");
    }

    /// <summary>
    /// A local application data folder that could not be determined gives a folder Windows cannot name, which the
    /// creation refuses without creating anything.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesDefaultFolderOfEmptyDataFolderIsRefusedAtCreate()
    {
        // Arrange
        var files = new PipeRecordingFiles(PipeRecordingFiles.DefaultFolder(string.Empty));

        // Act
        var action = () => files.Create(_started);

        // Assert
        Assert.ThrowsExactly<InvalidOperationException>(action, "A folder that is not a full path should be refused");
        Assert.IsFalse(Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Censio")), "Nothing should be created below the working directory");
    }

    /// <summary>
    /// The creation makes the missing folders and the file named after the start second.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesCreatesFolderAndFile()
    {
        // Arrange
        var folder = Path.Combine(_root, "Censio", "Recordings");
        var files = new PipeRecordingFiles(folder);

        // Act
        using (var stream = files.Create(_started))
        {
            stream.Write(Encoding.UTF8.GetBytes("line\n"));
        }

        // Assert
        var path = Path.Combine(folder, "recording-20261101-170900Z.jsonl");

        Assert.IsTrue(File.Exists(path), "The file should be named after the start second in UTC");
        Assert.AreEqual("line\n", File.ReadAllText(path), "The stream should write to the file");
    }

    /// <summary>
    /// The stream of the file can be written but not read.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesStreamIsWriteOnly()
    {
        // Arrange
        var files = new PipeRecordingFiles(Path.Combine(_root, "Recordings"));

        // Act
        using var stream = files.Create(_started);

        // Assert
        Assert.IsTrue(stream.CanWrite, "The stream should be writable");
        Assert.IsFalse(stream.CanRead, "The stream should not be readable");
    }

    /// <summary>
    /// A second recording that starts in the same second gets the suffix 2, a third the suffix 3.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesSameSecondGetsNextSuffix()
    {
        // Arrange
        var folder = Path.Combine(_root, "Recordings");
        var files = new PipeRecordingFiles(folder);

        // Act
        files.Create(_started).Dispose();
        files.Create(_started.AddMilliseconds(100)).Dispose();
        files.Create(_started.AddMilliseconds(200)).Dispose();

        // Assert
        string[] expected = ["recording-20261101-170900Z-2.jsonl", "recording-20261101-170900Z-3.jsonl", "recording-20261101-170900Z.jsonl"];
        var names = Directory.GetFiles(folder).Select(path => Path.GetFileName(path)).Order().ToArray();

        Assert.AreSequenceEqual(expected, names, "The files of one second should get the suffixes 2 and 3");
    }

    /// <summary>
    /// When the first 1,000 names of a second are taken, the creation fails and creates nothing more.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesAllNamesTakenThrowsIoException()
    {
        // Arrange
        var folder = Path.Combine(_root, "Recordings");

        Directory.CreateDirectory(folder);

        for (var sequence = 1; sequence <= 1000; sequence++)
        {
            File.WriteAllText(Path.Combine(folder, PipeRecordingFormat.FormatFileName(_started, sequence)), "taken");
        }

        var files = new PipeRecordingFiles(folder);

        // Act
        var action = () => files.Create(_started);

        // Assert
        var exception = Assert.ThrowsExactly<IOException>(action, "A second with no free name should be refused with an I/O error");

        Assert.AreEqual("No free recording file name is left for this second.", exception.Message, "The error should name the reason");
        Assert.HasCount(1000, Directory.GetFiles(folder), "No file beyond the 1,000th name should be created");
    }

    /// <summary>
    /// An existing file is never overwritten.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesNeverOverwrites()
    {
        // Arrange
        var folder = Path.Combine(_root, "Recordings");
        var existing = Path.Combine(folder, "recording-20261101-170900Z.jsonl");

        Directory.CreateDirectory(folder);
        File.WriteAllText(existing, "earlier");

        var files = new PipeRecordingFiles(folder);

        // Act
        using (var stream = files.Create(_started))
        {
            stream.Write(Encoding.UTF8.GetBytes("later"));
        }

        // Assert
        Assert.AreEqual("earlier", File.ReadAllText(existing), "The existing file should keep its content");
        Assert.AreEqual("later", File.ReadAllText(Path.Combine(folder, "recording-20261101-170900Z-2.jsonl")), "The new recording should go to the next suffix");
    }

    /// <summary>
    /// A failure that is not an existing file is not retried with another suffix.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesOtherCreateErrorIsNotRetried()
    {
        // Arrange
        var folder = Path.Combine(_root, "Recordings");

        Directory.CreateDirectory(Path.Combine(folder, "recording-20261101-170900Z.jsonl"));

        var files = new PipeRecordingFiles(folder);

        // Act
        var action = () => files.Create(_started);

        // Assert
        var exception = Assert.Throws<SystemException>(action, "A directory with the name of the file should make the creation fail");

        Assert.IsNotInstanceOfType<NotImplementedException>(exception, "The failure should come from the file system");
        Assert.IsTrue(exception is IOException or UnauthorizedAccessException, "The file system should refuse the name with an I/O or access error");
        Assert.HasCount(1, Directory.GetFileSystemEntries(folder), "No other suffix should have been tried");
    }

    /// <summary>
    /// A folder that is not a full path is refused and nothing is created for it.
    /// </summary>
    /// <param name="folder">The folder</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("Recordings")]
    [DataRow("./Recordings")]
    public void PipeRecordingFilesFolderNotFullyQualifiedIsRefused(string folder)
    {
        // Arrange
        var files = new PipeRecordingFiles(folder);

        // Act
        var action = () => files.Create(_started);

        // Assert
        Assert.ThrowsExactly<InvalidOperationException>(action, "A folder that is not a full path should be refused");
        Assert.IsFalse(Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Recordings")), "Nothing should be created below the working directory");
    }

    /// <summary>
    /// The file name uses the Gregorian calendar whatever the culture of the thread.
    /// </summary>
    [TestMethod]
    public void PipeRecordingFilesNameIsGregorianUnderOtherCulture()
    {
        // Arrange
        var folder = Path.Combine(_root, "Recordings");
        var files = new PipeRecordingFiles(folder);

        CultureInfo.CurrentCulture = new CultureInfo("th-TH");

        // Act
        files.Create(_started).Dispose();

        // Assert
        Assert.IsTrue(File.Exists(Path.Combine(folder, "recording-20261101-170900Z.jsonl")), "The year should be the Gregorian one, not the Buddhist 2569");
    }

    #endregion // Methods
}