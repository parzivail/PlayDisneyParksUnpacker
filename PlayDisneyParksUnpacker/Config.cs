using CommandLine;

namespace PlayDisneyParksUnpacker;

public class Config
{
	[Value(0, MetaName = "apktoolPath", HelpText = "Path to the APKTool batch file")] public string ApktoolPath { get; set; }

	[Value(1, MetaName = "sourcePath", HelpText = "Path to the source XAPK file to unpack")] public string SourcePath { get; set; }

	[Value(2, MetaName = "destPath", HelpText = "Path where the parsed files will be output")] public string DestPath { get; set; }
}