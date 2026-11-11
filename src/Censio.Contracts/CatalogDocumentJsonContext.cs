using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The source-generated serialization context of the catalog document.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                             AllowDuplicateProperties = false,
                             RespectNullableAnnotations = true,
                             NumberHandling = JsonNumberHandling.Strict,
                             PropertyNameCaseInsensitive = false,
                             ReadCommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
                             AllowTrailingCommas = false,
                             Converters = [typeof(CatalogEntryKindJsonConverter)])]
[JsonSerializable(typeof(CatalogDocument))]
internal sealed partial class CatalogDocumentJsonContext : JsonSerializerContext;