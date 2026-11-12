using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Censio.Contracts.Tests;

/// <summary>
/// Tests for <see cref="ContractWriter" />.
/// </summary>
[TestClass]
public sealed class ContractWriterTests
{
    #region Constants

    /// <summary>
    /// The fixed text of a message that breaks a value rule.
    /// </summary>
    private const string InvalidText = "Contract message breaks a rule of contract version 1";

    /// <summary>
    /// The fixed text of a message above a size bound.
    /// </summary>
    private const string TooLargeText = "Contract message exceeds a size bound";

    /// <summary>
    /// A valid icon reference or catalog identity: 64 lowercase hex digits.
    /// </summary>
    private const string Icon = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// A second valid icon reference, above <see cref="Icon" /> in ordinal order.
    /// </summary>
    private const string IconTwo = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The SHA-256 in lowercase hex of the empty catalog document <see cref="_catalog" />.
    /// </summary>
    private const string CatalogIdentity = "0cdc38f80e0882d1bb621266f90d1fb371984f4de46ae7045d89ff84c3c242d8";

    /// <summary>
    /// The text of the profile identity <see cref="_profileA" />.
    /// </summary>
    private const string ProfileAText = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// A message identity.
    /// </summary>
    private static readonly Guid _id = Guid.Parse("11111111-2222-4333-8444-555555555555");

    /// <summary>
    /// A second message identity.
    /// </summary>
    private static readonly Guid _idTwo = Guid.Parse("66666666-7777-4888-9999-000000000001");

    /// <summary>
    /// A profile identity.
    /// </summary>
    private static readonly Guid _profileA = Guid.Parse(ProfileAText);

    /// <summary>
    /// A second profile identity.
    /// </summary>
    private static readonly Guid _profileB = Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");

    /// <summary>
    /// A receive time with seven fractional digits.
    /// </summary>
    private static readonly DateTimeOffset _time = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.Zero).AddTicks(1234567);

    /// <summary>
    /// The four PNG bytes 89 50 4E 47.
    /// </summary>
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// The bytes of a valid, empty catalog document.
    /// </summary>
    private static readonly byte[] _catalog = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[]}");

    /// <summary>
    /// The messages that break a rule or a bound, built on demand: the name of the case, the kind it is refused with and its builder.
    /// </summary>
    private static readonly Dictionary<string, (ContractErrorKind Kind, Func<ContractMessage> Create)> _refusals = new()
                                                                                                                   {
                                                                                                                       ["token empty"] = (ContractErrorKind.Invalid, () => Hello(string.Empty, "0.3.0", _profileA, "Alice")),
                                                                                                                       ["companion version empty"] = (ContractErrorKind.Invalid, () => Hello("t", string.Empty, _profileA, "Alice")),
                                                                                                                       ["profile name empty"] = (ContractErrorKind.Invalid, () => Hello("t", "0.3.0", _profileA, string.Empty)),
                                                                                                                       ["profile name null"] = (ContractErrorKind.Invalid, () => Hello("t", "0.3.0", _profileA, null!)),
                                                                                                                       ["profile name with a lone surrogate"] = (ContractErrorKind.Invalid, () => Hello("t", "0.3.0", _profileA, "Al" + (char)0xD800)),
                                                                                                                       ["token with a lone surrogate"] = (ContractErrorKind.Invalid, () => Hello(string.Concat((char)0xDC00, "t"), "0.3.0", _profileA, "Alice")),
                                                                                                                       ["profile identity all zero"] = (ContractErrorKind.Invalid, () => Hello("t", "0.3.0", Guid.Empty, "Alice")),
                                                                                                                       ["server version empty"] = (ContractErrorKind.Invalid, () => Accepted(string.Empty, null)),
                                                                                                                       ["catalog identity uppercase"] = (ContractErrorKind.Invalid, () => Accepted("0.3.0", Icon.ToUpperInvariant())),
                                                                                                                       ["catalog identity of 63 digits"] = (ContractErrorKind.Invalid, () => Accepted("0.3.0", Icon[..63])),
                                                                                                                       ["catalog identity empty"] = (ContractErrorKind.Invalid, () => Accepted("0.3.0", string.Empty)),
                                                                                                                       ["supported versions empty"] = (ContractErrorKind.Invalid, () => Refused(ContractHelloRefusal.UnsupportedVersion, 2)),
                                                                                                                       ["supported versions descending"] = (ContractErrorKind.Invalid, () => Refused(ContractHelloRefusal.UnsupportedVersion, 3, 2, 1)),
                                                                                                                       ["supported versions repeated"] = (ContractErrorKind.Invalid, () => Refused(ContractHelloRefusal.UnsupportedVersion, 2, 1, 1)),
                                                                                                                       ["supported version zero"] = (ContractErrorKind.Invalid, () => Refused(ContractHelloRefusal.UnsupportedVersion, 2, 0)),
                                                                                                                       ["supported version negative"] = (ContractErrorKind.Invalid, () => Refused(ContractHelloRefusal.UnsupportedVersion, 2, -1, 1)),
                                                                                                                       ["supported versions null"] = (ContractErrorKind.Invalid, () => new ContractHelloRefused
                                                                                                                                                                                       {
                                                                                                                                                                                           Reason = ContractHelloRefusal.TokenRefused,
                                                                                                                                                                                           ReceivedVersion = 1,
                                                                                                                                                                                           SupportedVersions = null!
                                                                                                                                                                                       }),
                                                                                                                       ["undefined hello refusal"] = (ContractErrorKind.Invalid, () => Refused((ContractHelloRefusal)99, 1, 1)),
                                                                                                                       ["catalog upload identity all zero"] = (ContractErrorKind.Invalid, () => new ContractCatalogUpload
                                                                                                                                                                                                {
                                                                                                                                                                                                    Id = Guid.Empty,
                                                                                                                                                                                                    Document = _catalog
                                                                                                                                                                                                }),
                                                                                                                       ["icon identity all zero"] = (ContractErrorKind.Invalid, () => new ContractIconImage
                                                                                                                                                                                      {
                                                                                                                                                                                          Id = Guid.Empty,
                                                                                                                                                                                          Reference = Hash(_png),
                                                                                                                                                                                          Png = _png
                                                                                                                                                                                      }),
                                                                                                                       ["icon reference not the PNG hash"] = (ContractErrorKind.Invalid, () => new ContractIconImage
                                                                                                                                                                                               {
                                                                                                                                                                                                   Id = _id,
                                                                                                                                                                                                   Reference = Icon,
                                                                                                                                                                                                   Png = _png
                                                                                                                                                                                               }),
                                                                                                                       ["icon reference uppercase"] = (ContractErrorKind.Invalid, () => new ContractIconImage
                                                                                                                                                                                        {
                                                                                                                                                                                            Id = _id,
                                                                                                                                                                                            Reference = Hash(_png).ToUpperInvariant(),
                                                                                                                                                                                            Png = _png
                                                                                                                                                                                        }),
                                                                                                                       ["icon PNG empty"] = (ContractErrorKind.Invalid, () => new ContractIconImage
                                                                                                                                                                              {
                                                                                                                                                                                  Id = _id,
                                                                                                                                                                                  Reference = Hash([]),
                                                                                                                                                                                  Png = ReadOnlyMemory<byte>.Empty
                                                                                                                                                                              }),
                                                                                                                       ["tick identity all zero"] = (ContractErrorKind.Invalid, () => Tick(Guid.Empty, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5)))),
                                                                                                                       ["tick profile all zero"] = (ContractErrorKind.Invalid, () => Tick(_id, Guid.Empty, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5)))),
                                                                                                                       ["tick receive time with an offset"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, new DateTimeOffset(2026, 11, 12, 11, 20, 30, TimeSpan.FromHours(1)), 42, Island(1, 1, 0, 2, "Isle", Entry(5)))),
                                                                                                                       ["tick without islands"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42)),
                                                                                                                       ["island key twice"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle"), Island(1, 1, 0, 3, "Other"))),
                                                                                                                       ["island name with a lone surrogate"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Is" + (char)0xD800))),
                                                                                                                       ["generation NaN"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(float.NaN, 0f)))),
                                                                                                                       ["income positive infinity"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(0f, float.PositiveInfinity)))),
                                                                                                                       ["generation negative infinity"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(float.NegativeInfinity, 0f)))),
                                                                                                                       ["islands null"] = (ContractErrorKind.Invalid, () => new ContractTick
                                                                                                                                                                            {
                                                                                                                                                                                Id = _id,
                                                                                                                                                                                ProfileId = _profileA,
                                                                                                                                                                                ReceiveTime = _time,
                                                                                                                                                                                GameTime = 42,
                                                                                                                                                                                Islands = null!
                                                                                                                                                                            }),
                                                                                                                       ["island null"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, null!)),
                                                                                                                       ["entry null"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", null!))),
                                                                                                                       ["island name null"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, null!))),
                                                                                                                       ["map null"] = (ContractErrorKind.Invalid, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5, null!, Map())))),
                                                                                                                       ["session start identity all zero"] = (ContractErrorKind.Invalid, () => new ContractSessionStart
                                                                                                                                                                                               {
                                                                                                                                                                                                   Id = Guid.Empty,
                                                                                                                                                                                                   ProfileId = _profileA,
                                                                                                                                                                                                   ReceiveTime = _time
                                                                                                                                                                                               }),
                                                                                                                       ["session start receive time with an offset"] = (ContractErrorKind.Invalid, () => new ContractSessionStart
                                                                                                                                                                                                         {
                                                                                                                                                                                                             Id = _id,
                                                                                                                                                                                                             ProfileId = _profileA,
                                                                                                                                                                                                             ReceiveTime = new DateTimeOffset(2026, 11, 12, 8, 20, 30, TimeSpan.FromHours(-2))
                                                                                                                                                                                                         }),
                                                                                                                       ["session end profile all zero"] = (ContractErrorKind.Invalid, () => new ContractSessionEnd
                                                                                                                                                                                            {
                                                                                                                                                                                                Id = _id,
                                                                                                                                                                                                ProfileId = Guid.Empty,
                                                                                                                                                                                                ReceiveTime = _time
                                                                                                                                                                                            }),
                                                                                                                       ["undefined pipe state"] = (ContractErrorKind.Invalid, () => new ContractStatus
                                                                                                                                                                                    {
                                                                                                                                                                                        PipeState = (ContractPipeState)99
                                                                                                                                                                                    }),
                                                                                                                       ["acknowledgement identity all zero"] = (ContractErrorKind.Invalid, () => Acknowledgement(Guid.Empty, null, null)),
                                                                                                                       ["undefined acknowledgement refusal"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, (ContractAcknowledgementRefusal)99, null)),
                                                                                                                       ["accepted with a refused version"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, null, 2)),
                                                                                                                       ["unsupported version without a refused version"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, ContractAcknowledgementRefusal.UnsupportedVersion, null)),
                                                                                                                       ["catalog unsupported version without a refused version"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogUnsupportedVersion, null)),
                                                                                                                       ["invalid with a refused version"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, ContractAcknowledgementRefusal.Invalid, 2)),
                                                                                                                       ["refusal with missing icons"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, ContractAcknowledgementRefusal.Malformed, null, Icon)),
                                                                                                                       ["missing icon not hex"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, null, null, "XYZ")),
                                                                                                                       ["missing icon uppercase"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, null, null, Icon.ToUpperInvariant())),
                                                                                                                       ["missing icon twice"] = (ContractErrorKind.Invalid, () => Acknowledgement(_id, null, null, Icon, Icon)),
                                                                                                                       ["missing icons null"] = (ContractErrorKind.Invalid, () => new ContractAcknowledgement
                                                                                                                                                                                  {
                                                                                                                                                                                      Id = _id,
                                                                                                                                                                                      Refusal = null,
                                                                                                                                                                                      RefusedVersion = null,
                                                                                                                                                                                      MissingIcons = null!
                                                                                                                                                                                  }),
                                                                                                                       ["token of 1,025 bytes"] = (ContractErrorKind.TooLarge, () => Hello(new string('a', 1025), "0.3.0", _profileA, "Alice")),
                                                                                                                       ["profile name of 1,026 bytes"] = (ContractErrorKind.TooLarge, () => Hello("t", "0.3.0", _profileA, new string('ä', 513))),
                                                                                                                       ["companion version of 1,025 bytes"] = (ContractErrorKind.TooLarge, () => Hello("t", new string('1', 1025), _profileA, "Alice")),
                                                                                                                       ["server version of 1,025 bytes"] = (ContractErrorKind.TooLarge, () => Accepted(new string('1', 1025), null)),
                                                                                                                       ["island name of 766 bytes"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, new string('a', 766)))),
                                                                                                                       ["island name of 256 replacement characters"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, new string((char)0xFFFD, 256)))),
                                                                                                                       ["257 islands"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, Islands(257))),
                                                                                                                       ["1,025 workforce pairs"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5, Pairs(ContractReader.MaxPairsPerMap + 1), Map())))),
                                                                                                                       ["1,025 building pairs"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5, Map(), Pairs(ContractReader.MaxPairsPerMap + 1))))),
                                                                                                                       ["524,289 entries and pairs"] = (ContractErrorKind.TooLarge, () => Tick(_id, _profileA, _time, 42, LimitIslands(ContractReader.MaxEntriesAndPairs + 1))),
                                                                                                                       ["17 supported versions"] = (ContractErrorKind.TooLarge, () => Refused(ContractHelloRefusal.UnsupportedVersion, 18, [.. Enumerable.Range(1, 17)])),
                                                                                                                       ["16,385 missing icons"] = (ContractErrorKind.TooLarge, () => Acknowledgement(_id, null, null, MissingIcons(ContractReader.MaxMissingIcons + 1))),
                                                                                                                       ["PNG above the binary bound"] = (ContractErrorKind.TooLarge, () => IconOf(BinaryOfLength(ContractReader.MaxBinaryBytes + 1))),
                                                                                                                       ["document above the binary bound"] = (ContractErrorKind.TooLarge, () => new ContractCatalogUpload
                                                                                                                                                                                                {
                                                                                                                                                                                                    Id = _id,
                                                                                                                                                                                                    Document = new byte[ContractReader.MaxBinaryBytes + 1]
                                                                                                                                                                                                })
                                                                                                                   };

    /// <summary>
    /// The messages of every kind, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<ContractMessage>> _everyKind = new()
                                                                                   {
                                                                                       ["hello"] = () => Hello("secret-token", "0.3.0", _profileA, "Alice"),
                                                                                       ["hello without token"] = () => Hello(null, "0.3.0", _profileA, "Alice"),
                                                                                       ["hello accepted"] = () => Accepted("0.3.0", Icon),
                                                                                       ["hello accepted without catalog"] = () => Accepted("0.3.0", null),
                                                                                       ["hello refused"] = () => Refused(ContractHelloRefusal.UnsupportedVersion, 2, 1),
                                                                                       ["catalog upload"] = () => new ContractCatalogUpload
                                                                                                                  {
                                                                                                                      Id = _id,
                                                                                                                      Document = _catalog
                                                                                                                  },
                                                                                       ["icon image"] = () => IconOf(_png),
                                                                                       ["tick"] = FullTick,
                                                                                       ["session start"] = () => new ContractSessionStart
                                                                                                                 {
                                                                                                                     Id = _id,
                                                                                                                     ProfileId = _profileA,
                                                                                                                     ReceiveTime = _time
                                                                                                                 },
                                                                                       ["session end"] = () => new ContractSessionEnd
                                                                                                               {
                                                                                                                   Id = _id,
                                                                                                                   ProfileId = _profileA,
                                                                                                                   ReceiveTime = _time
                                                                                                               },
                                                                                       ["status"] = () => new ContractStatus
                                                                                                          {
                                                                                                              PipeState = ContractPipeState.PipeBusy
                                                                                                          },
                                                                                       ["acknowledgement accepted"] = () => Acknowledgement(_id, null, null, Icon, IconTwo),
                                                                                       ["acknowledgement refused"] = () => Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogUnsupportedVersion, 2)
                                                                                   };

    /// <summary>
    /// The ticks of the edge cases of E7 and N1, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<ContractTick>> _edgeTicks = new()
                                                                                {
                                                                                    ["product GUID twice in one island"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5), Entry(5, Map((1, 2)), Map()))),
                                                                                    ["island without entries"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Empty"), Island(1, 2, 0, 2, "Full", Entry(5))),
                                                                                    ["all islands without entries"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "A"), Island(1, 2, 0, 2, "B")),
                                                                                    ["empty island name"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, string.Empty, Entry(5))),
                                                                                    ["negative GUIDs"] = () => Tick(_id, _profileA, _time, 42, Island(-180023, 1, 0, 2, "Isle", Entry(-5, Map((-9, -1), (-3, 4)), Map((int.MinValue, int.MinValue))))),
                                                                                    ["extreme GUIDs and amounts"] = () => Tick(_id, _profileA, _time, 42, Island(int.MaxValue, 255, 255, 255, "Isle", Entry(int.MinValue, Map((int.MinValue, int.MaxValue), (int.MaxValue, int.MinValue)), Map()))),
                                                                                    ["game time minimum"] = () => Tick(_id, _profileA, _time, long.MinValue, Island(1, 1, 0, 2, "Isle")),
                                                                                    ["game time maximum"] = () => Tick(_id, _profileA, _time, long.MaxValue, Island(1, 1, 0, 2, "Isle")),
                                                                                    ["same island ID in two areas"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "A"), Island(1, 1, 1, 2, "B"), Island(2, 1, 0, 2, "C")),
                                                                                    ["negative zero"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(-0f, -0f))),
                                                                                    ["subnormal floats"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(float.Epsilon, -BitConverter.Int32BitsToSingle(0x007FFFFF)))),
                                                                                    ["extreme floats"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(float.MaxValue, float.MinValue))),
                                                                                    ["floats without short form"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(1f / 3f, 0.1f))),
                                                                                    ["every float no value"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", EntryWithFloat(null, null))),
                                                                                    ["receive time of one tick"] = () => Tick(_id, _profileA, new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero).AddTicks(1), 42, Island(1, 1, 0, 2, "Isle")),
                                                                                    ["receive time of seven nines"] = () => Tick(_id, _profileA, new DateTimeOffset(2026, 11, 12, 23, 59, 59, TimeSpan.Zero).AddTicks(9999999), 42, Island(1, 1, 0, 2, "Isle")),
                                                                                    ["receive time of a whole second"] = () => Tick(_id, _profileA, new DateTimeOffset(2026, 11, 12, 23, 59, 59, TimeSpan.Zero), 42, Island(1, 1, 0, 2, "Isle")),
                                                                                    ["name of characters JSON escapes"] = () => Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, $"Quote \" back\\slash tab\t line\n <>&' Größe 木 {char.ConvertFromUtf32(0x1F332)}{(char)0x2028}"))
                                                                                };

    /// <summary>
    /// The catalog documents at the catalog's bounds, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> _catalogsAtBound = new()
                                                                                {
                                                                                    ["document of the catalog's byte bound"] = () => PaddedCatalog(CatalogDocumentReader.MaxDocumentBytes),
                                                                                    ["document of every count at the catalog's bound"] = CountsCatalog
                                                                                };

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the names of the messages of every kind.
    /// </summary>
    private static IEnumerable<object[]> EveryKindRows => _everyKind.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets the names of the edge-case ticks.
    /// </summary>
    private static IEnumerable<object[]> EdgeTickRows => _edgeTicks.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets the names of the refused messages, the kind and the text each is refused with.
    /// </summary>
    private static IEnumerable<object[]> RefusalRows => _refusals.Select(pair => new object[] { pair.Key, pair.Value.Kind, pair.Value.Kind == ContractErrorKind.Invalid ? InvalidText : TooLargeText });

    /// <summary>
    /// Gets the names of the catalog documents at the catalog's bounds.
    /// </summary>
    private static IEnumerable<object[]> CatalogAtBoundRows => _catalogsAtBound.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets the hellos: the name of the case and the hello.
    /// </summary>
    private static IEnumerable<object[]> HelloRows
    {
        get
        {
            yield return new object[] { "with token", Hello("secret-token", "0.3.0", _profileA, "Alice") };
            yield return new object[] { "without token", Hello(null, "0.3.0", _profileA, "Alice") };
            yield return new object[] { "non-ASCII and escaped characters", Hello("t\"o\\k", "1.0.0-beta+7", _profileB, "Bäume 木 \" \\ / " + char.ConvertFromUtf32(0x1F332)) };
            yield return new object[] { "strings at the bound", Hello(new string('a', 1024), new string('1', 1024), _profileA, new string('木', 341) + "a") };
        }
    }

    /// <summary>
    /// Gets the refused hello replies: the reason, the received version and the supported versions.
    /// </summary>
    private static IEnumerable<object[]> RefusedRows
    {
        get
        {
            yield return new object[] { ContractHelloRefusal.UnsupportedVersion, 2, new[] { 1 } };
            yield return new object[] { ContractHelloRefusal.UnsupportedVersion, int.MinValue, new[] { 1, 2, 3 } };
            yield return new object[] { ContractHelloRefusal.TokenRefused, 1, new[] { 1 } };
            yield return new object[] { ContractHelloRefusal.CompanionConnected, 1, Enumerable.Range(1, 16).ToArray() };
        }
    }

    /// <summary>
    /// Gets the acknowledgements: the acknowledgement and the refusal text it gives, <see langword="null" /> when accepted.
    /// </summary>
    private static IEnumerable<object?[]> AcknowledgementRows
    {
        get
        {
            yield return new object?[] { Acknowledgement(_id, null, null), null };
            yield return new object?[] { Acknowledgement(_id, null, null, Icon, IconTwo), null };
            yield return new object?[] { Acknowledgement(_id, null, null, MissingIcons(ContractReader.MaxMissingIcons)), null };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.Malformed, null), "Contract message is malformed" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.UnsupportedVersion, 2), "Contract version 2 is not supported; supported: 1" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.UnsupportedVersion, -7), "Contract version -7 is not supported; supported: 1" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.Invalid, null), InvalidText };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.TooLarge, null), TooLargeText };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogMalformed, null), "Catalog document is malformed" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogUnsupportedVersion, 3), "Catalog schema version 3 is not supported; supported: 1" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogInvalid, null), "Catalog document breaks a rule of schema version 1" };
            yield return new object?[] { Acknowledgement(_id, ContractAcknowledgementRefusal.CatalogTooLarge, null), "Catalog document exceeds a size bound" };
        }
    }

    /// <summary>
    /// Gets the messages of the frozen form with their exact bytes: the message and the JSON text.
    /// </summary>
    private static IEnumerable<object[]> FrozenFormRows
    {
        get
        {
            yield return new object[] { Refused(ContractHelloRefusal.UnsupportedVersion, 2, 1), "{\"type\":\"helloRefused\",\"contractVersion\":1,\"reason\":\"unsupportedVersion\",\"receivedVersion\":2,\"supportedVersions\":[1]}" };
            yield return new object[] { Refused(ContractHelloRefusal.TokenRefused, 1, 1), "{\"type\":\"helloRefused\",\"contractVersion\":1,\"reason\":\"tokenRefused\",\"receivedVersion\":1,\"supportedVersions\":[1]}" };
            yield return new object[] { Refused(ContractHelloRefusal.CompanionConnected, 1, 1), "{\"type\":\"helloRefused\",\"contractVersion\":1,\"reason\":\"companionConnected\",\"receivedVersion\":1,\"supportedVersions\":[1]}" };
            yield return new object[] { Hello(null, "0.3.0", _profileA, "Alice"), $"{{\"type\":\"hello\",\"contractVersion\":1,\"token\":null,\"companionVersion\":\"0.3.0\",\"profileId\":\"{ProfileAText}\",\"profileName\":\"Alice\"}}" };
            yield return new object[] { Hello("secret", "0.3.0", _profileA, "Alice"), $"{{\"type\":\"hello\",\"contractVersion\":1,\"token\":\"secret\",\"companionVersion\":\"0.3.0\",\"profileId\":\"{ProfileAText}\",\"profileName\":\"Alice\"}}" };
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A hello, with and without token, is written and read back equal.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="hello">The hello</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(HelloRows))]
    public void ContractWriterHelloRoundTripsEqual(string label, ContractHello hello)
    {
        // Arrange
        // Act
        var written = ContractWriter.Write(hello);

        // Assert
        AssertReadsBackEqual(hello, written, label);
    }

    /// <summary>
    /// An accepted hello reply, with a catalog identity and without, is written and read back equal.
    /// </summary>
    /// <param name="identity">The catalog identity</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(Icon)]
    [DataRow(null)]
    public void ContractWriterHelloAcceptedRoundTripsEqual(string? identity)
    {
        // Arrange
        var accepted = Accepted("0.3.0", identity);

        // Act
        var written = ContractWriter.Write(accepted);

        // Assert
        AssertReadsBackEqual(accepted, written, identity ?? "no catalog");
    }

    /// <summary>
    /// A refused hello reply of each reason is written and read back equal.
    /// </summary>
    /// <param name="reason">The reason</param>
    /// <param name="received">The received version</param>
    /// <param name="supported">The supported versions</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC5")]
    [DynamicData(nameof(RefusedRows))]
    public void ContractWriterHelloRefusedRoundTripsEqual(ContractHelloRefusal reason, int received, int[] supported)
    {
        // Arrange
        var refused = Refused(reason, received, supported);

        // Act
        var written = ContractWriter.Write(refused);

        // Assert
        AssertReadsBackEqual(refused, written, reason.ToString());
    }

    /// <summary>
    /// The hello and the refused hello reply are written in their frozen form: type and version first, every property in its order.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="expected">The expected JSON text</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(FrozenFormRows))]
    public void ContractWriterFrozenFormIsWrittenExactly(ContractMessage message, string expected)
    {
        // Arrange
        // Act
        var written = ContractWriter.Write(message);

        // Assert
        Assert.IsNull(written.Error, $"The message should be written: {expected}");
        Assert.AreEqual(expected, Encoding.UTF8.GetString(written.Utf8Json.Span), "The message should be written in its frozen form");
    }

    /// <summary>
    /// A catalog upload is written and read back equal, and its identity is the SHA-256 of the document in lowercase hex.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ContractWriterCatalogUploadRoundTripsEqual()
    {
        // Arrange
        var upload = new ContractCatalogUpload
                     {
                         Id = _id,
                         Document = _catalog
                     };

        // Act
        var written = ContractWriter.Write(upload);

        // Assert
        var read = Assert.IsInstanceOfType<ContractCatalogUpload>(AssertReadsBackEqual(upload, written, "catalog upload"), "The upload should read back as an upload");

        Assert.AreEqual(CatalogIdentity, read.ComputeIdentity(), "The identity should be the SHA-256 of the document in lowercase hex");
        Assert.AreEqual(CatalogIdentity, upload.ComputeIdentity(), "The identity of the written upload should be the same");
    }

    /// <summary>
    /// An icon image is written and read back equal.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ContractWriterIconImageRoundTripsEqual()
    {
        // Arrange
        var icon = IconOf(_png);

        // Act
        var written = ContractWriter.Write(icon);

        // Assert
        AssertReadsBackEqual(icon, written, "icon image");
    }

    /// <summary>
    /// A session start is written and read back equal.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ContractWriterSessionStartRoundTripsEqual()
    {
        // Arrange
        var start = new ContractSessionStart
                    {
                        Id = _id,
                        ProfileId = _profileA,
                        ReceiveTime = _time
                    };

        // Act
        var written = ContractWriter.Write(start);

        // Assert
        AssertReadsBackEqual(start, written, "session start");
    }

    /// <summary>
    /// A session end is written and read back equal.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ContractWriterSessionEndRoundTripsEqual()
    {
        // Arrange
        var end = new ContractSessionEnd
                  {
                      Id = _idTwo,
                      ProfileId = _profileB,
                      ReceiveTime = _time.AddTicks(1)
                  };

        // Act
        var written = ContractWriter.Write(end);

        // Assert
        AssertReadsBackEqual(end, written, "session end");
    }

    /// <summary>
    /// A status of each pipe state is written and read back equal.
    /// </summary>
    /// <param name="state">The pipe state</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(ContractPipeState.WaitingForGame)]
    [DataRow(ContractPipeState.Connected)]
    [DataRow(ContractPipeState.Receiving)]
    [DataRow(ContractPipeState.PipeBusy)]
    [DataRow(ContractPipeState.ProtocolMismatch)]
    [DataRow(ContractPipeState.Error)]
    public void ContractWriterStatusRoundTripsEqual(ContractPipeState state)
    {
        // Arrange
        var status = new ContractStatus
                     {
                         PipeState = state
                     };

        // Act
        var written = ContractWriter.Write(status);

        // Assert
        AssertReadsBackEqual(status, written, state.ToString());
    }

    /// <summary>
    /// An acknowledgement, accepted with missing icons or refused with each kind, is written and read back equal and gives the
    /// text of its kind.
    /// </summary>
    /// <param name="acknowledgement">The acknowledgement</param>
    /// <param name="text">The refusal text, <see langword="null" /> when accepted</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(AcknowledgementRows))]
    public void ContractWriterAcknowledgementRoundTripsEqual(ContractAcknowledgement acknowledgement, string? text)
    {
        // Arrange
        // Act
        var written = ContractWriter.Write(acknowledgement);

        // Assert
        var read = Assert.IsInstanceOfType<ContractAcknowledgement>(AssertReadsBackEqual(acknowledgement, written, $"{acknowledgement.Refusal}"), "The acknowledgement should read back as one");

        Assert.AreEqual(text, read.GetRefusalText(), $"The read acknowledgement should give the text of its kind: {acknowledgement.Refusal}");
        Assert.AreEqual(text, acknowledgement.GetRefusalText(), $"The written acknowledgement should give the text of its kind: {acknowledgement.Refusal}");
    }

    /// <summary>
    /// A message read back and written again gives the same bytes, for every kind.
    /// </summary>
    /// <param name="label">The name of the case</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(EveryKindRows))]
    public void ContractWriterReadMessageWrittenAgainGivesSameBytes(string label)
    {
        // Arrange
        var first = ContractWriter.Write(_everyKind[label]());
        var read = ContractReader.Read(first.Utf8Json.Span);

        // Act
        var second = ContractWriter.Write(read.Message!);

        // Assert
        Assert.IsNull(first.Error, $"The message should be written: {label}");
        Assert.IsNull(read.Error, $"The message should be read back: {label}");
        Assert.IsNull(second.Error, $"The read message should be written again: {label}");
        Assert.AreEqual(Hex(first.Utf8Json), Hex(second.Utf8Json), $"Writing the read message again should give the same bytes: {label}");
    }

    /// <summary>
    /// Maps and missing icons in another order give the same bytes: they are written sorted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ContractWriterMapsInAnyOrderGiveSameBytes()
    {
        // Arrange
        var ascending = ContractWriter.Write(Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5, Map((-3, 1), (2, 2), (9, 3)), Map((1, 1), (4, 4))))));
        var descending = Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5, Map((9, 3), (2, 2), (-3, 1)), Map((4, 4), (1, 1)))));

        // Act
        var written = ContractWriter.Write(descending);

        // Assert
        Assert.IsNull(written.Error, "A tick of maps in descending order should be written");
        Assert.AreEqual(Hex(ascending.Utf8Json), Hex(written.Utf8Json), "Maps in another order should give the same bytes");
    }

    /// <summary>
    /// A tick reads back every island with key, session ID and name and every entry with its eleven values and both maps,
    /// the receive time and the game time, exact.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ContractWriterTickRoundTripsEveryValueExact()
    {
        // Arrange
        var tick = FullTick();

        // Act
        var written = ContractWriter.Write(tick);

        // Assert
        var read = Assert.IsInstanceOfType<ContractTick>(AssertReadsBackEqual(tick, written, "full tick"), "The tick should read back as a tick");

        Assert.AreEqual(_time.UtcTicks, read.ReceiveTime.UtcTicks, "The receive time should read back to its full precision");
        Assert.AreEqual(BitConverter.SingleToInt32Bits(1f / 3f), BitConverter.SingleToInt32Bits(read.Islands[0].Entries[0].Generation!.Value), "A float should read back bit for bit");
    }

    /// <summary>
    /// The edge cases of a tick read back exact: a repeated product GUID, islands without entries, an empty name, negative
    /// GUIDs, the ends of the game time, negative zero, subnormal and extreme floats, and receive times of seven digits.
    /// </summary>
    /// <param name="label">The name of the case</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DynamicData(nameof(EdgeTickRows))]
    public void ContractWriterTickEdgeValuesRoundTripExact(string label)
    {
        // Arrange
        var tick = _edgeTicks[label]();

        // Act
        var written = ContractWriter.Write(tick);

        // Assert
        AssertReadsBackEqual(tick, written, label);
    }

    /// <summary>
    /// A tick and a session event read back with the profile identity they were written with, not the one of a hello.
    /// </summary>
    /// <param name="kind">The message type</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(ContractMessageType.Tick)]
    [DataRow(ContractMessageType.SessionStart)]
    [DataRow(ContractMessageType.SessionEnd)]
    public void ContractWriterTickAndSessionEventsKeepTheirProfile(ContractMessageType kind)
    {
        // Arrange
        var hello = ContractWriter.Write(Hello("t", "0.3.0", _profileB, "Profile B"));
        var message = ProfileMessage(kind, _profileA);

        // Act
        var written = ContractWriter.Write(message);

        // Assert
        Assert.IsNull(hello.Error, "The hello of profile B should be written");
        Assert.AreEqual(_profileA, ProfileOf(AssertReadsBackEqual(message, written, kind.ToString())), $"The {kind} should read back with its own profile A");
    }

    /// <summary>
    /// A message that breaks a rule of version 1 is refused as invalid, one above a bound as too large; nothing is written.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="kind">The kind it is refused with</param>
    /// <param name="text">The text it is refused with</param>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC7")]
    [DynamicData(nameof(RefusalRows))]
    public void ContractWriterRuleBreakIsRefusedAndWritesNothing(string label, ContractErrorKind kind, string text)
    {
        // Arrange
        var message = _refusals[label].Create();

        // Act
        var written = ContractWriter.Write(message);

        // Assert
        Assert.IsNotNull(written.Error, $"The message should be refused: {label}");
        Assert.AreEqual(kind, written.Error.Kind, $"The refusal should have the expected kind: {label}");
        Assert.AreEqual(text, written.Error.Text, $"The refusal should have the fixed text of its kind: {label}");
        Assert.IsTrue(written.Utf8Json.IsEmpty, $"A refused message should write nothing: {label}");
    }

    /// <summary>
    /// A tick at the pipe's island limit, with 1,024 pairs in a map and 524,288 entries and pairs, is written and read back.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractWriterTickAtPipeCountLimitsRoundTrips()
    {
        // Arrange
        var tick = Tick(_id, _profileA, _time, 42, LimitIslands(ContractReader.MaxEntriesAndPairs));

        // Act
        var written = ContractWriter.Write(tick);

        // Assert
        Assert.HasCount(ContractReader.MaxIslands, tick.Islands, "The tick should hold the island limit");
        Assert.AreEqual(ContractReader.MaxEntriesAndPairs, CountEntriesAndPairs(tick), "The tick should hold the entry-and-pair limit");
        AssertReadsBackEqual(tick, written, "tick at the pipe's limits");
    }

    /// <summary>
    /// A tick within every count bound whose message exceeds the byte bound is refused as too large and nothing is written.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractWriterTickAboveByteBoundIsTooLarge()
    {
        // Arrange
        var entry = new ContractTickEntry
                    {
                        ProductGuid = int.MinValue,
                        Generation = float.MinValue,
                        Consumption = -float.Epsilon,
                        Delta = float.MinValue,
                        PerfectGeneration = -1.17549435E-38f,
                        PerfectConsumption = float.MinValue,
                        BuildingCount = int.MinValue,
                        Maintenance = int.MinValue,
                        Income = float.MinValue,
                        Profit = int.MinValue,
                        SummedProductivity = float.MinValue,
                        AverageProductivity = float.MinValue,
                        WorkforceAmounts = Map(),
                        BuildingAmounts = Map()
                    };
        const int entries = 100000;
        var tick = Tick(_id, _profileA, _time, long.MinValue, Island(1, 1, 0, 2, "Isle", [.. Enumerable.Repeat(entry, entries)]));

        // Act
        var written = ContractWriter.Write(tick);

        // Assert
        Assert.IsLessThan(ContractReader.MaxEntriesAndPairs, entries, "The tick should stay within the entry-and-pair limit");
        Assert.IsGreaterThan(ContractReader.MaxMessageBytes, entries * 252, "The property names alone should exceed the byte bound");
        Assert.IsNotNull(written.Error, "The tick should be refused");
        Assert.AreEqual(ContractErrorKind.TooLarge, written.Error.Kind, "The tick should be refused as too large");
        Assert.AreEqual(TooLargeText, written.Error.Text, "The refusal should have the fixed text");
        Assert.IsTrue(written.Utf8Json.IsEmpty, "A refused tick should write nothing");
    }

    /// <summary>
    /// An icon PNG at the binary bound is written and read back equal.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractWriterIconAtBinaryBoundRoundTrips()
    {
        // Arrange
        var icon = IconOf(BinaryOfLength(ContractReader.MaxBinaryBytes));

        // Act
        var written = ContractWriter.Write(icon);

        // Assert
        AssertReadsBackEqual(icon, written, "icon at the binary bound");
    }

    /// <summary>
    /// A catalog document at the catalog's bounds is uploaded and read back equal.
    /// </summary>
    /// <param name="label">The name of the case</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(CatalogAtBoundRows))]
    public void ContractWriterCatalogAtCatalogBoundRoundTrips(string label)
    {
        // Arrange
        var upload = new ContractCatalogUpload
                     {
                         Id = _id,
                         Document = _catalogsAtBound[label]()
                     };

        // Act
        var written = ContractWriter.Write(upload);

        // Assert
        AssertReadsBackEqual(upload, written, label);
    }

    /// <summary>
    /// An island name of 255 replacement characters (765 UTF-8 bytes) is written and read back whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractWriterIslandNameOfReplacementCharactersRoundTrips()
    {
        // Arrange
        var name = new string((char)0xFFFD, 255);
        var tick = Tick(_id, _profileA, _time, 42, Island(1, 1, 0, 2, name, Entry(5)));

        // Act
        var written = ContractWriter.Write(tick);

        // Assert
        Assert.AreEqual(ContractReader.MaxIslandNameBytes, Encoding.UTF8.GetByteCount(name), "The name should be at the island name bound");
        AssertReadsBackEqual(tick, written, "name of 255 replacement characters");
    }

    /// <summary>
    /// Writing a taken message again keeps its identity: both writes read back with the identity it was taken with.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void ContractWriterWritingAgainKeepsTheIdentity()
    {
        // Arrange
        var end = new ContractSessionEnd
                  {
                      Id = _id,
                      ProfileId = _profileA,
                      ReceiveTime = _time
                  };
        var first = ContractWriter.Write(end);

        // Act
        var second = ContractWriter.Write(end);

        // Assert
        Assert.AreEqual(_id, Assert.IsInstanceOfType<ContractSessionEnd>(ContractReader.Read(first.Utf8Json.Span).Message, "The first write should read back").Id, "The first write should carry the identity");
        Assert.AreEqual(_id, Assert.IsInstanceOfType<ContractSessionEnd>(ContractReader.Read(second.Utf8Json.Span).Message, "The second write should read back").Id, "The second write should carry the same identity");
        Assert.AreEqual(Hex(first.Utf8Json), Hex(second.Utf8Json), "The resend should be the same bytes");
    }

    /// <summary>
    /// Two session ends of equal content taken separately are written with their own identities and receive times.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void ContractWriterSessionEndsTakenSeparatelyKeepTheirIdentities()
    {
        // Arrange
        var ends = new[]
                   {
                       new ContractSessionEnd
                       {
                           Id = _id,
                           ProfileId = _profileA,
                           ReceiveTime = _time
                       },
                       new ContractSessionEnd
                       {
                           Id = _idTwo,
                           ProfileId = _profileA,
                           ReceiveTime = _time.AddTicks(3)
                       }
                   };

        // Act
        var written = ends.Select(ContractWriter.Write).ToArray();

        // Assert
        var first = Assert.IsInstanceOfType<ContractSessionEnd>(ContractReader.Read(written[0].Utf8Json.Span).Message, "The first end should read back");
        var second = Assert.IsInstanceOfType<ContractSessionEnd>(ContractReader.Read(written[1].Utf8Json.Span).Message, "The second end should read back");

        Assert.AreEqual(_id, first.Id, "The first end should keep its identity");
        Assert.AreEqual(_idTwo, second.Id, "The second end should keep its own identity");
        Assert.AreEqual(_time, first.ReceiveTime, "The first end should keep its receive time");
        Assert.AreEqual(_time.AddTicks(3), second.ReceiveTime, "The second end should keep its own receive time");
    }

    /// <summary>
    /// Returns the lowercase hex SHA-256 of bytes.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The hash in lowercase hex</returns>
    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Returns the length and hash of written bytes, for a short comparison.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The length and the hash</returns>
    private static string Hex(ReadOnlyMemory<byte> bytes)
    {
        return $"{bytes.Length}:{Hash(bytes.Span)}";
    }

    /// <summary>
    /// Returns bytes of a length in a repeating pattern.
    /// </summary>
    /// <param name="length">The length</param>
    /// <returns>The bytes</returns>
    private static byte[] BinaryOfLength(int length)
    {
        var bytes = new byte[length];

        for (var index = 0; index < length; index++)
        {
            bytes[index] = (byte)(index * 7);
        }

        return bytes;
    }

    /// <summary>
    /// Returns a valid catalog document padded with trailing spaces to a length.
    /// </summary>
    /// <param name="length">The length</param>
    /// <returns>The document bytes</returns>
    private static byte[] PaddedCatalog(int length)
    {
        var document = new byte[length];

        document.AsSpan().Fill((byte)' ');
        _catalog.CopyTo(document, 0);

        return document;
    }

    /// <summary>
    /// Returns the catalog writer's document with every count at the catalog's bound.
    /// </summary>
    /// <returns>The document bytes</returns>
    private static byte[] CountsCatalog()
    {
        var names = Enumerable.Range(0, CatalogDocumentReader.MaxNamesPerEntry).ToDictionary(index => new string([(char)('a' + (index / 26)), (char)('a' + (index % 26))]), _ => "x");
        var first = new CatalogDocumentEntry
                    {
                        AssetGuid = 1,
                        Kind = CatalogEntryKind.Region,
                        Names = names,
                        RegionGuid = 1,
                        RegionTypes = [.. Enumerable.Range(0, CatalogDocumentReader.MaxRegionTypesPerEntry).Select(index => "T" + index)],
                        IconReference = Icon
                    };
        var others = Enumerable.Range(2, CatalogDocumentReader.MaxEntries - 1).Select(guid => new CatalogDocumentEntry
                                                                                              {
                                                                                                  AssetGuid = guid,
                                                                                                  Kind = CatalogEntryKind.Good,
                                                                                                  Names = new Dictionary<string, string>
                                                                                                          {
                                                                                                              ["en"] = "x"
                                                                                                          },
                                                                                                  RegionGuid = null,
                                                                                                  RegionTypes = null,
                                                                                                  IconReference = null
                                                                                              });
        var document = new CatalogDocument
                       {
                           SchemaVersion = CatalogDocument.CurrentSchemaVersion,
                           GameVersion = "1.2.3.4",
                           Entries = [first, .. others]
                       };

        return CatalogDocumentWriter.Write(document).Utf8Json.ToArray();
    }

    /// <summary>
    /// Builds a hello.
    /// </summary>
    /// <param name="token">The token</param>
    /// <param name="version">The Companion's product version</param>
    /// <param name="profile">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <returns>The hello</returns>
    private static ContractHello Hello(string? token, string version, Guid profile, string name)
    {
        return new ContractHello
               {
                   Token = token,
                   CompanionVersion = version,
                   ProfileId = profile,
                   ProfileName = name
               };
    }

    /// <summary>
    /// Builds an accepted hello reply.
    /// </summary>
    /// <param name="version">The server's product version</param>
    /// <param name="identity">The catalog identity</param>
    /// <returns>The reply</returns>
    private static ContractHelloAccepted Accepted(string version, string? identity)
    {
        return new ContractHelloAccepted
               {
                   ServerVersion = version,
                   CatalogIdentity = identity
               };
    }

    /// <summary>
    /// Builds a refused hello reply.
    /// </summary>
    /// <param name="reason">The reason</param>
    /// <param name="received">The received version</param>
    /// <param name="supported">The supported versions</param>
    /// <returns>The reply</returns>
    private static ContractHelloRefused Refused(ContractHelloRefusal reason, int received, params int[] supported)
    {
        return new ContractHelloRefused
               {
                   Reason = reason,
                   ReceivedVersion = received,
                   SupportedVersions = supported
               };
    }

    /// <summary>
    /// Builds an icon image with the reference of its PNG data.
    /// </summary>
    /// <param name="png">The PNG data</param>
    /// <returns>The icon image</returns>
    private static ContractIconImage IconOf(byte[] png)
    {
        return new ContractIconImage
               {
                   Id = _id,
                   Reference = Hash(png),
                   Png = png
               };
    }

    /// <summary>
    /// Builds an acknowledgement.
    /// </summary>
    /// <param name="id">The identity of the confirmed message</param>
    /// <param name="refusal">The refusal kind</param>
    /// <param name="version">The refused version</param>
    /// <param name="missing">The missing icon references</param>
    /// <returns>The acknowledgement</returns>
    private static ContractAcknowledgement Acknowledgement(Guid id, ContractAcknowledgementRefusal? refusal, int? version, params string[] missing)
    {
        return new ContractAcknowledgement
               {
                   Id = id,
                   Refusal = refusal,
                   RefusedVersion = version,
                   MissingIcons = missing
               };
    }

    /// <summary>
    /// Builds distinct, ascending icon references of 64 lowercase hex digits.
    /// </summary>
    /// <param name="count">The number of references</param>
    /// <returns>The references</returns>
    private static string[] MissingIcons(int count)
    {
        return [.. Enumerable.Range(1, count).Select(index => index.ToString("x64", CultureInfo.InvariantCulture))];
    }

    /// <summary>
    /// Builds a tick.
    /// </summary>
    /// <param name="id">The identity</param>
    /// <param name="profile">The profile identity</param>
    /// <param name="time">The receive time</param>
    /// <param name="gameTime">The game time</param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static ContractTick Tick(Guid id, Guid profile, DateTimeOffset time, long gameTime, params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = id,
                   ProfileId = profile,
                   ReceiveTime = time,
                   GameTime = gameTime,
                   Islands = islands
               };
    }

    /// <summary>
    /// Builds an island.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island ID</param>
    /// <param name="areaIndex">The area index</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="name">The name</param>
    /// <param name="entries">The entries</param>
    /// <returns>The island</returns>
    private static ContractTickIsland Island(int sessionGuid, byte islandId, byte areaIndex, byte sessionId, string name, params ContractTickEntry[] entries)
    {
        return new ContractTickIsland
               {
                   SessionGuid = sessionGuid,
                   IslandId = islandId,
                   AreaIndex = areaIndex,
                   SessionId = sessionId,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Builds an entry of distinct values and maps.
    /// </summary>
    /// <param name="guid">The product GUID</param>
    /// <param name="workforce">The workforce map</param>
    /// <param name="building">The building map</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int guid, IReadOnlyDictionary<int, int> workforce, IReadOnlyDictionary<int, int> building)
    {
        return new ContractTickEntry
               {
                   ProductGuid = guid,
                   Generation = 1f / 3f,
                   Consumption = 0.1f,
                   Delta = -2.5f,
                   PerfectGeneration = 123456.79f,
                   PerfectConsumption = 1e-30f,
                   BuildingCount = 17,
                   Maintenance = -250,
                   Income = null,
                   Profit = int.MaxValue,
                   SummedProductivity = 3.4e38f,
                   AverageProductivity = 0.999999f,
                   WorkforceAmounts = workforce,
                   BuildingAmounts = building
               };
    }

    /// <summary>
    /// Builds an entry of distinct values and small maps.
    /// </summary>
    /// <param name="guid">The product GUID</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry Entry(int guid)
    {
        return Entry(guid, Map((1010052, 40), (1010053, 0)), Map((1010343, 3)));
    }

    /// <summary>
    /// Builds an entry whose eight floats take two values in turn, and empty maps.
    /// </summary>
    /// <param name="first">The value of the first, third, fifth and seventh float</param>
    /// <param name="second">The value of the second, fourth, sixth and eighth float</param>
    /// <returns>The entry</returns>
    private static ContractTickEntry EntryWithFloat(float? first, float? second)
    {
        return new ContractTickEntry
               {
                   ProductGuid = 5,
                   Generation = first,
                   Consumption = second,
                   Delta = first,
                   PerfectGeneration = second,
                   PerfectConsumption = first,
                   BuildingCount = 0,
                   Maintenance = 0,
                   Income = second,
                   Profit = 0,
                   SummedProductivity = first,
                   AverageProductivity = second,
                   WorkforceAmounts = Map(),
                   BuildingAmounts = Map()
               };
    }

    /// <summary>
    /// Builds a map in the order given.
    /// </summary>
    /// <param name="pairs">The pairs of GUID and amount</param>
    /// <returns>The map</returns>
    private static Dictionary<int, int> Map(params (int Guid, int Amount)[] pairs)
    {
        return pairs.ToDictionary(pair => pair.Guid, pair => pair.Amount);
    }

    /// <summary>
    /// Builds a map of ascending GUIDs from 1.
    /// </summary>
    /// <param name="count">The number of pairs</param>
    /// <returns>The map</returns>
    private static Dictionary<int, int> Pairs(int count)
    {
        return Enumerable.Range(1, count).ToDictionary(guid => guid, guid => guid % 97);
    }

    /// <summary>
    /// Builds islands with distinct keys and no entries.
    /// </summary>
    /// <param name="count">The number of islands</param>
    /// <returns>The islands</returns>
    private static ContractTickIsland[] Islands(int count)
    {
        return [.. Enumerable.Range(0, count).Select(index => Island(180023, (byte)(index % 256), (byte)(index / 256), 2, "Isle " + index))];
    }

    /// <summary>
    /// Builds 256 islands of one entry each, whose entries and pairs add up to a total: each entry holds 1,024 workforce
    /// pairs and 1,023 building pairs, the last island's entry the rest as building pairs.
    /// </summary>
    /// <param name="total">The number of entries and pairs: 524,288 or 524,289</param>
    /// <returns>The islands</returns>
    private static ContractTickIsland[] LimitIslands(int total)
    {
        var workforce = Pairs(ContractReader.MaxPairsPerMap);
        var building = Pairs(ContractReader.MaxPairsPerMap - 1);
        var islands = Enumerable.Range(0, ContractReader.MaxIslands - 1).Select(index => Island(180023, (byte)index, 0, 2, "Isle " + index, Entry(index + 1, workforce, building))).ToList();
        var rest = total - ((ContractReader.MaxIslands - 1) * (1 + workforce.Count + building.Count));

        islands.Add(Island(180023, 255, 0, 2, "Last", Entry(ContractReader.MaxIslands, workforce, Pairs(rest - 1 - workforce.Count))));

        return [.. islands];
    }

    /// <summary>
    /// Counts the entries and map pairs of a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The number of entries and pairs</returns>
    private static int CountEntriesAndPairs(ContractTick tick)
    {
        var count = 0;

        foreach (var entry in tick.Islands.SelectMany(island => island.Entries))
        {
            count += 1 + entry.WorkforceAmounts.Count + entry.BuildingAmounts.Count;
        }

        return count;
    }

    /// <summary>
    /// Builds a tick of every value: three islands, entries with distinct values, a missing value and maps.
    /// </summary>
    /// <returns>The tick</returns>
    private static ContractTick FullTick()
    {
        return Tick(_id,
                    _profileA,
                    _time,
                    987654321012,
                    Island(180023, 1, 0, 2, "Isle Alpha", Entry(1010017), Entry(1010018, Map(), Map((5, 6)))),
                    Island(180045, 7, 3, 4, "Größe 木", Entry(1010019, Map((-1, -2)), Map())),
                    Island(180023, 2, 0, 2, "Third"));
    }

    /// <summary>
    /// Builds a tick, session start or session end of a profile.
    /// </summary>
    /// <param name="kind">The message type</param>
    /// <param name="profile">The profile identity</param>
    /// <returns>The message</returns>
    private static ContractMessage ProfileMessage(ContractMessageType kind, Guid profile)
    {
        return kind switch
               {
                   ContractMessageType.Tick => Tick(_id, profile, _time, 42, Island(1, 1, 0, 2, "Isle", Entry(5))),
                   ContractMessageType.SessionStart => new ContractSessionStart
                                                       {
                                                           Id = _id,
                                                           ProfileId = profile,
                                                           ReceiveTime = _time
                                                       },
                   _ => new ContractSessionEnd
                        {
                            Id = _id,
                            ProfileId = profile,
                            ReceiveTime = _time
                        }
               };
    }

    /// <summary>
    /// Returns the profile identity of a tick or session event.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The profile identity</returns>
    private static Guid ProfileOf(ContractMessage message)
    {
        return message switch
               {
                   ContractTick tick => tick.ProfileId,
                   ContractSessionStart start => start.ProfileId,
                   ContractSessionEnd end => end.ProfileId,
                   _ => Guid.Empty
               };
    }

    /// <summary>
    /// Asserts that a write succeeded and that its bytes read back to a message equal to the one written, and returns it.
    /// </summary>
    /// <param name="expected">The message written</param>
    /// <param name="written">The write result</param>
    /// <param name="context">What the case was, for the messages</param>
    /// <returns>The message read back</returns>
    private static ContractMessage AssertReadsBackEqual(ContractMessage expected, ContractWriteResult written, string context)
    {
        Assert.IsNull(written.Error, "The message should be written: " + context);
        Assert.IsFalse(written.Utf8Json.IsEmpty, "The message should be written to bytes: " + context);

        var read = ContractReader.Read(written.Utf8Json.Span);

        Assert.IsNull(read.Error, "The written message should be read back: " + context);
        Assert.IsNotNull(read.Message, "The written message should be returned: " + context);
        Assert.AreEqual(Describe(expected), Describe(read.Message), "The message read back should be equal in every value: " + context);

        return read.Message;
    }

    /// <summary>
    /// Describes every value of a message exactly: floats by their bits, times by their ticks and offset, bytes by length and hash.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The description</returns>
    private static string Describe(ContractMessage message)
    {
        return message switch
               {
                   ContractHello hello => $"hello|{Text(hello.Token)}|{Text(hello.CompanionVersion)}|{hello.ProfileId}|{Text(hello.ProfileName)}",
                   ContractHelloAccepted accepted => $"accepted|{Text(accepted.ServerVersion)}|{Text(accepted.CatalogIdentity)}",
                   ContractHelloRefused refused => $"refused|{refused.Reason}|{refused.ReceivedVersion}|{string.Join(",", refused.SupportedVersions)}",
                   ContractCatalogUpload upload => $"catalog|{upload.Id}|{Hex(upload.Document)}",
                   ContractIconImage icon => $"icon|{icon.Id}|{Text(icon.Reference)}|{Hex(icon.Png)}",
                   ContractTick tick => $"tick|{tick.Id}|{tick.ProfileId}|{Time(tick.ReceiveTime)}|{tick.GameTime}|{string.Join(";", tick.Islands.Select(DescribeIsland))}",
                   ContractSessionStart start => $"start|{start.Id}|{start.ProfileId}|{Time(start.ReceiveTime)}",
                   ContractSessionEnd end => $"end|{end.Id}|{end.ProfileId}|{Time(end.ReceiveTime)}",
                   ContractStatus status => $"status|{status.PipeState}",
                   ContractAcknowledgement acknowledgement => $"ack|{acknowledgement.Id}|{acknowledgement.Refusal?.ToString() ?? "-"}|{acknowledgement.RefusedVersion?.ToString(CultureInfo.InvariantCulture) ?? "-"}|{string.Join(",", acknowledgement.MissingIcons)}",
                   _ => message.GetType().Name
               };
    }

    /// <summary>
    /// Describes every value of an island exactly.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The description</returns>
    private static string DescribeIsland(ContractTickIsland island)
    {
        return $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex}/{island.SessionId}/{Text(island.Name)}[{string.Join(" ", island.Entries.Select(DescribeEntry))}]";
    }

    /// <summary>
    /// Describes every value of an entry exactly, the maps in GUID order.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The description</returns>
    private static string DescribeEntry(ContractTickEntry entry)
    {
        return $"{entry.ProductGuid}:{Bits(entry.Generation)},{Bits(entry.Consumption)},{Bits(entry.Delta)},{Bits(entry.PerfectGeneration)},{Bits(entry.PerfectConsumption)},{entry.BuildingCount},{entry.Maintenance},{Bits(entry.Income)},{entry.Profit},{Bits(entry.SummedProductivity)},{Bits(entry.AverageProductivity)},w{DescribeMap(entry.WorkforceAmounts)},b{DescribeMap(entry.BuildingAmounts)}";
    }

    /// <summary>
    /// Describes a map in GUID order.
    /// </summary>
    /// <param name="map">The map</param>
    /// <returns>The description</returns>
    private static string DescribeMap(IReadOnlyDictionary<int, int> map)
    {
        return string.Join(",", map.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    /// Describes a float by its bits, or a dash for no value.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The description</returns>
    private static string Bits(float? value)
    {
        return value is null ? "-" : BitConverter.SingleToInt32Bits(value.Value).ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes a time by its UTC ticks and offset.
    /// </summary>
    /// <param name="time">The time</param>
    /// <returns>The description</returns>
    private static string Time(DateTimeOffset time)
    {
        return $"{time.UtcTicks}{time.Offset}";
    }

    /// <summary>
    /// Describes a text with its length, or a dash for none.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The description</returns>
    private static string Text(string? text)
    {
        return text is null ? "-" : $"{text.Length}:{text}";
    }

    #endregion // Methods
}