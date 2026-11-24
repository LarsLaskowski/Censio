using Censio.Contracts;
using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// The catalog a live sender offers to the server: its identity and the messages written from it on demand.
/// </summary>
public sealed class LiveCatalog
{
    #region Fields

    /// <summary>
    /// The catalog.
    /// </summary>
    private readonly GameCatalog _catalog;

    /// <summary>
    /// The icons, or <c>null</c> when there are none.
    /// </summary>
    private readonly GameIconSet? _icons;

    /// <summary>
    /// The game build, or <c>null</c> when unknown.
    /// </summary>
    private readonly string? _build;

    /// <summary>
    /// The icons by their reference.
    /// </summary>
    private readonly Dictionary<string, GameIcon> _iconsByReference;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCatalog" /> class.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <param name="identity">The identity of the catalog document, or <c>null</c> when the contract writer refused it</param>
    /// <param name="iconsByReference">The icons by their reference</param>
    private LiveCatalog(GameCatalog catalog, GameIconSet? icons, string? build, string? identity, Dictionary<string, GameIcon> iconsByReference)
    {
        _catalog = catalog;
        _icons = icons;
        _build = build;
        Identity = identity;
        _iconsByReference = iconsByReference;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the identity of the catalog document, or <c>null</c> when the contract writer refused it.
    /// </summary>
    public string? Identity { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the catalog state and computes its identity.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <returns>The catalog state</returns>
    public static LiveCatalog Create(GameCatalog catalog, GameIconSet? icons, string? build)
    {
        var iconsByReference = new Dictionary<string, GameIcon>();

        if (icons is not null)
        {
            foreach (var icon in icons.Icons.Values.Distinct())
            {
                iconsByReference[GameCatalogDocumentConverter.ToIconReference(icon)] = icon;
            }
        }

        var written = WriteDocument(catalog, icons, build);
        string? identity = null;

        if (written.Error is null)
        {
            identity = new ContractCatalogUpload
                       {
                           Id = Guid.Empty,
                           Document = written.Utf8Json
                       }.ComputeIdentity();
        }

        return new LiveCatalog(catalog, icons, build, identity, iconsByReference);
    }

    /// <summary>
    /// Writes the catalog upload message.
    /// </summary>
    /// <returns>The written message, or <c>null</c> when the contract writer refused it</returns>
    public LiveCatalogMessage? WriteUpload()
    {
        var document = WriteDocument(_catalog, _icons, _build);

        if (document.Error is not null)
        {
            return null;
        }

        var id = Guid.NewGuid();
        var written = ContractWriter.Write(new ContractCatalogUpload
                                           {
                                               Id = id,
                                               Document = document.Utf8Json
                                           });

        return ToMessage(id, written);
    }

    /// <summary>
    /// Writes the icon message of one icon reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <returns>The written message, or <c>null</c> for an unknown reference or a refusal of the contract writer</returns>
    public LiveCatalogMessage? WriteIcon(string reference)
    {
        if (_iconsByReference.TryGetValue(reference, out var icon) == false)
        {
            return null;
        }

        var id = Guid.NewGuid();
        var written = ContractWriter.Write(new ContractIconImage
                                           {
                                               Id = id,
                                               Reference = reference,
                                               Png = icon.Png
                                           });

        return ToMessage(id, written);
    }

    /// <summary>
    /// Converts the catalog and writes its document.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <returns>The result of the document writer</returns>
    private static CatalogDocumentWriteResult WriteDocument(GameCatalog catalog, GameIconSet? icons, string? build)
    {
        return CatalogDocumentWriter.Write(GameCatalogDocumentConverter.ToDocument(catalog, icons, build));
    }

    /// <summary>
    /// Turns the result of the contract writer into a message.
    /// </summary>
    /// <param name="id">The id of the message</param>
    /// <param name="written">The result of the contract writer</param>
    /// <returns>The message, or <c>null</c> when the writer refused it</returns>
    private static LiveCatalogMessage? ToMessage(Guid id, ContractWriteResult written)
    {
        return written.Error is null
                   ? new LiveCatalogMessage
                     {
                         Id = id,
                         Utf8Json = written.Utf8Json
                     }
                   : null;
    }

    #endregion // Methods
}