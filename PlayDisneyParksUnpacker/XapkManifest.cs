using System.Text.Json.Serialization;

namespace PlayDisneyParksUnpacker;

public record XapkManifest(
	[property: JsonPropertyName("xapk_version")] int XapkVersion,
	[property: JsonPropertyName("package_name")] string PackageName,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("version_code")] string VersionCode,
	[property: JsonPropertyName("version_name")] string VersionName,
	[property: JsonPropertyName("min_sdk_version")] string MinSdkVersion,
	[property: JsonPropertyName("target_sdk_version")] string TargetSdkVersion,
	[property: JsonPropertyName("permissions")] IReadOnlyList<string> Permissions,
	[property: JsonPropertyName("split_configs")] IReadOnlyList<string> SplitConfigs,
	[property: JsonPropertyName("total_size")] int TotalSize,
	[property: JsonPropertyName("icon")] string Icon,
	[property: JsonPropertyName("split_apks")] IReadOnlyList<SplitApk> SplitApks
);