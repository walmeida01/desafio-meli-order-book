using System.Text.Json.Serialization;

namespace OrderBook.FunctionalTests.Contracts;

public sealed record ErrorRecord([property: JsonPropertyName("code")] string Code);
