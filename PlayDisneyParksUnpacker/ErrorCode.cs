namespace PlayDisneyParksUnpacker;

public enum ErrorCode
{
	None = 0,
	Unknown = -1,
	NoManifest = -2,
	MalformedManifest = -3,
	UnsupportedManifest = -4,
	DestFileExists = -5,
	JsPackageImportConflict = -6,
	HtmlPatchFailed = -7
}