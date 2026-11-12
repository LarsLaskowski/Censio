using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The source-generated serialization context of the contract messages: the catalog's strict options (no unknown, repeated
/// or missing property, exact case, no number as text) and the converters of the contract's own forms.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                             AllowDuplicateProperties = false,
                             RespectNullableAnnotations = true,
                             NumberHandling = JsonNumberHandling.Strict,
                             PropertyNameCaseInsensitive = false,
                             ReadCommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
                             AllowTrailingCommas = false,
                             Converters = [
                                              typeof(ContractEnumJsonConverter<ContractMessageType>),
                                              typeof(ContractEnumJsonConverter<ContractHelloRefusal>),
                                              typeof(ContractEnumJsonConverter<ContractPipeState>),
                                              typeof(ContractEnumJsonConverter<ContractAcknowledgementRefusal>),
                                              typeof(ContractFloatJsonConverter),
                                              typeof(ContractGuidJsonConverter),
                                              typeof(ContractTimeJsonConverter),
                                              typeof(ContractBinaryJsonConverter),
                                              typeof(ContractMapJsonConverter),
                                              typeof(ContractTextListJsonConverter)
                                          ])]
[JsonSerializable(typeof(ContractHello))]
[JsonSerializable(typeof(ContractHelloAccepted))]
[JsonSerializable(typeof(ContractHelloRefused))]
[JsonSerializable(typeof(ContractCatalogUpload))]
[JsonSerializable(typeof(ContractIconImage))]
[JsonSerializable(typeof(ContractTick))]
[JsonSerializable(typeof(ContractSessionStart))]
[JsonSerializable(typeof(ContractSessionEnd))]
[JsonSerializable(typeof(ContractStatus))]
[JsonSerializable(typeof(ContractAcknowledgement))]
internal sealed partial class ContractJsonContext : JsonSerializerContext;