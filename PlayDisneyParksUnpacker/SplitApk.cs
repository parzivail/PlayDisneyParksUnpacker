using System.Text.Json.Serialization;

namespace PlayDisneyParksUnpacker;

public record SplitApk(
	[property: JsonPropertyName("file")] string File,
	[property: JsonPropertyName("id")] string Id
);