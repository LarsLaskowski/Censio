using System.Reflection;
using System.Security.Cryptography;

using Censio.Contracts;
using Censio.GameData;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LiveCatalog" />.
/// </summary>
[TestClass]
public sealed class LiveCatalogTests
{
    #region Methods

    /// <summary>
    /// A catalog without icons is written with every icon reference <c>null</c>, and its identity is the SHA-256 of the
    /// document's bytes (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void LiveCatalogWithoutIconsWritesNullIconReferences()
    {
        // Arrange
        var data = CatalogTestData.Create("A", 2, false);
        var live = LiveCatalog.Create(data.Catalog, null, data.Build);

        // Act
        var upload = live.WriteUpload();

        // Assert
        Assert.IsNotNull(upload, "The catalog should be written");

        var message = Assert.IsInstanceOfType<ContractCatalogUpload>(ContractReader.Read(upload.Value.Utf8Json.Span).Message, "The bytes should be a catalog upload");
        var document = CatalogDocumentReader.Read(message.Document.Span).Document;

        Assert.IsNotNull(document, "The document of the upload should be readable");
        Assert.HasCount(3, document.Entries, "The region and both goods should be in the document");
        Assert.IsTrue(document.Entries.All(entry => entry.IconReference is null), "No entry should carry an icon reference");
        Assert.AreEqual(upload.Value.Id, message.Id, "The message should carry the id of the written message");
        Assert.AreEqual(data.Identity, live.Identity, "The identity should be that of the document without icons");
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(message.Document.Span)), live.Identity, "The identity should be the SHA-256 of the document's bytes");
    }

    /// <summary>
    /// The catalog keeps no document model, no document bytes and no written message: its fields hold the catalog, the icons,
    /// the build, the identity and the icons by reference, and a write leaves none behind (N2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC12")]
    public void LiveCatalogHoldsNoDocumentOrMessageBytes()
    {
        // Arrange
        var data = CatalogTestData.Create("A", 3);
        var live = LiveCatalog.Create(data.Catalog, data.Icons, data.Build);

        // Act
        var upload = live.WriteUpload();
        var icon = live.WriteIcon(data.References[0]);
        var fields = typeof(LiveCatalog).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Assert
        Assert.IsNotNull(upload, "The catalog should be written");
        Assert.IsNotNull(icon, "The icon should be written");
        Assert.AreEqual(data.Identity, live.Identity, "The identity should be that of the document");
        Assert.IsNotEmpty(fields, "The catalog should keep its state in fields");
        Assert.IsEmpty(fields.Where(field => HoldsBytes(field.FieldType)).Select(field => field.Name).ToArray(), "No field should hold a document, document bytes or a written message");
    }

    /// <summary>
    /// An icon is written as one <c>iconImage</c> of its reference and its PNG, with a new id on every call, and an unknown
    /// reference is not written (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC12")]
    public void LiveCatalogWriteIconWritesOneIconPerCall()
    {
        // Arrange
        var data = CatalogTestData.Create("A", 3);
        var live = LiveCatalog.Create(data.Catalog, data.Icons, data.Build);

        // Act
        var first = live.WriteIcon(data.References[0]);
        var second = live.WriteIcon(data.References[1]);
        var again = live.WriteIcon(data.References[0]);
        var unknown = live.WriteIcon(new string('f', 64));

        // Assert
        Assert.IsNotNull(first, "The first icon should be written");
        Assert.IsNotNull(second, "The second icon should be written");
        Assert.IsNotNull(again, "The first icon should be written again on request");
        Assert.IsNull(unknown, "An unknown reference should not be written");

        var firstIcon = Assert.IsInstanceOfType<ContractIconImage>(ContractReader.Read(first.Value.Utf8Json.Span).Message, "The first bytes should be an icon image");
        var secondIcon = Assert.IsInstanceOfType<ContractIconImage>(ContractReader.Read(second.Value.Utf8Json.Span).Message, "The second bytes should be an icon image");

        Assert.AreEqual(data.References[0], firstIcon.Reference, "The first message should carry the first reference");
        Assert.AreSequenceEqual(data.Pngs[0], firstIcon.Png.ToArray(), "The first message should carry the PNG of the first icon only");
        Assert.AreEqual(first.Value.Id, firstIcon.Id, "The first message should carry its id");
        Assert.AreEqual(data.References[1], secondIcon.Reference, "The second message should carry the second reference");
        Assert.AreSequenceEqual(data.Pngs[1], secondIcon.Png.ToArray(), "The second message should carry the PNG of the second icon only");
        Assert.AreNotEqual(first.Value.Id, again.Value.Id, "Every call should write a message with a new id");
    }

    /// <summary>
    /// Checks whether a type holds a catalog document, bytes or a contract message, also as a type argument.
    /// </summary>
    /// <param name="type">The type</param>
    /// <returns><see langword="true" /> when it does</returns>
    private static bool HoldsBytes(Type type)
    {
        if (type == typeof(byte[]) || type == typeof(ReadOnlyMemory<byte>) || type == typeof(Memory<byte>) || type == typeof(LiveCatalogMessage) || type == typeof(CatalogDocument))
        {
            return true;
        }

        return typeof(ContractMessage).IsAssignableFrom(type) || type.GenericTypeArguments.Any(HoldsBytes);
    }

    #endregion // Methods
}