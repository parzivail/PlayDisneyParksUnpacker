using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PlayDisneyParksUnpacker;

public static class Program
{
	public static int Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;

		try
		{
			Parser.Default
				.ParseArguments<Config>(args)
				.WithParsed(config =>
				{
					AnsiConsole
						.Progress()
						.Columns(
							new TaskDescriptionColumn(),
							new ProgressBarColumn(),
							new TotalColumn(),
							new SpinnerColumn(Spinner.Known.Dots)
						)
						.Start(ctx =>
						{
							if (Directory.Exists(config.DestPath))
								Directory.Delete(config.DestPath, true);
							UnpackXapk(ctx, config);
							WithManifest(ctx, config, manifest => RunSplitSwgeRuntime(ctx, manifest, config));
						});
				})
				.WithNotParsed(errors => throw new UnpackException(ErrorCode.ArgumentParseFailed, "Unable to parse arguments"));

			return (int)ErrorCode.None;
		}
		catch (UnpackException e)
		{
			Console.Error.WriteLine(e);
			return (int)e.Code;
		}
		catch (Exception e)
		{
			Console.Error.WriteLine(e);
			return (int)ErrorCode.Unknown;
		}
	}

	private static void WithManifest(ProgressContext ctx, Config config, Action<XapkManifest> consumer)
	{
		var task = ctx.AddTask("Reading XAPK manifest", maxValue: 1);

		const string manifestPath = @"unknown\manifest.json";

		var manifestFilename = Path.Combine(config.DestPath, "unpacked_xapk", manifestPath);
		if (!File.Exists(manifestFilename))
			throw new UnpackException(ErrorCode.NoManifest, "Could not find manifest");

		try
		{
			var manifest = JsonSerializer.Deserialize<XapkManifest>(File.ReadAllText(manifestFilename));
			if (manifest == null)
				throw new UnpackException(ErrorCode.MalformedManifest, "Unknown manifest parse error");

			if (manifest.XapkVersion != 2)
				throw new UnpackException(ErrorCode.UnsupportedManifest, "Unsupported XAPK manifest version");

			task.Increment(1);
			task.StopTask();

			consumer(manifest);
		}
		catch (JsonException e)
		{
			throw new UnpackException(ErrorCode.MalformedManifest, e);
		}
	}

	private static void RunSplitSwgeRuntime(ProgressContext ctx, XapkManifest manifest, Config config)
	{
		const string basePackage = "base";
		const string playPackage = "play";
		const string swgePackage = "starWarsGalaxysEdgeGame";

		var xapkUnpackedPath = Path.Combine(config.DestPath, "unpacked_xapk");
		var idToPath = manifest.SplitApks.ToDictionary(apk => apk.Id, apk => Path.Combine(xapkUnpackedPath, "unknown", apk.File));
		var targetApks = new[] { basePackage, playPackage, swgePackage };

		var unpackTask = ctx.AddTask("Unpack APKs", maxValue: targetApks.Length);

		var tempOutDir = Path.Combine(config.DestPath, "unpacked_modules");

		using var process = new ApktoolProcess(config.ApktoolPath);
		foreach (var targetApk in targetApks)
		{
			var outDir = Path.Combine(tempOutDir, targetApk);
			process.Run(idToPath[targetApk], outDir, false);
			unpackTask.Increment(1);
		}

		Directory.Delete(xapkUnpackedPath, true);

		unpackTask.StopTask();

		var packageMap = new Dictionary<string, string>
		{
			[Path.Combine(tempOutDir, basePackage, "assets", "MockedResponses")] = Path.Combine(config.DestPath, "mocked_api_responses"),
			[Path.Combine(tempOutDir, basePackage, "assets", "shared-libraries")] = Path.Combine(config.DestPath, "shared_js_libraries"),
			[Path.Combine(tempOutDir, basePackage, "res", "raw")] = Path.Combine(config.DestPath, "android_raw"),
			[Path.Combine(tempOutDir, basePackage, "res", "values")] = Path.Combine(config.DestPath, "android_values"),
			[Path.Combine(tempOutDir, playPackage, "assets", "ar", "database")] = Path.Combine(config.DestPath, "ar_marker_database"),
			[Path.Combine(tempOutDir, playPackage, "assets", "db")] = Path.Combine(config.DestPath, "local_database"),
			[Path.Combine(tempOutDir, playPackage, "assets", "firestore-db")] = Path.Combine(config.DestPath, "firestore_database"),
			[Path.Combine(tempOutDir, swgePackage, "assets", "games", "swge.bundle")] = Path.Combine(config.DestPath, "game")
		};

		var moveTask = ctx.AddTask("Move relevant assets", maxValue: packageMap.Count);

		foreach (var (source, dest) in packageMap)
			MoveAll(moveTask, source, dest);

		moveTask.Value = moveTask.MaxValue;

		Directory.Delete(tempOutDir, true);
		moveTask.StopTask();

		var sortJsonTask = ctx.AddTask("Sort JSON keys", maxValue: 1);
		SortJsonKeys(sortJsonTask, config.DestPath);
		sortJsonTask.Value = sortJsonTask.MaxValue;
		sortJsonTask.StopTask();

		var parseModulesTask = ctx.AddTask("Extract JS modules", maxValue: 1);

		var indexJs = Path.Combine(config.DestPath, "game", "index.js");
		var (indexModules, indexAuxModule) = ParseUglifiedModules(parseModulesTask, indexJs);

		parseModulesTask.Value = parseModulesTask.MaxValue;
		parseModulesTask.StopTask();

		var totalModules = (indexAuxModule == null ? indexModules : indexModules.Concat(new[] { indexAuxModule })).ToArray();

		var patchAndWriteModulesTask = ctx.AddTask("Patch and write modules", maxValue: totalModules.Length);

		foreach (var module in totalModules)
		{
			SimplifyImports(module);

			var outputPath = Path.Combine(config.DestPath, "game", "app", module.Name + ".js");
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
			File.WriteAllText(outputPath, module.Content.ToString());

			patchAndWriteModulesTask.Increment(1);
		}

		File.Delete(indexJs);

		patchAndWriteModulesTask.StopTask();

		var patchHtmlTask = ctx.AddTask("Patch index.html", maxValue: 1);

		var indexHtml = Path.Combine(config.DestPath, "game", "index.html");
		if (!PatchIndexHtml(indexHtml, indexAuxModule != null))
			throw new UnpackException(ErrorCode.HtmlPatchFailed, "Unable to patch index.html");

		patchHtmlTask.Increment(1);
		patchHtmlTask.StopTask();
	}

	private static void SortJsonKeys(ProgressTask task, string directory)
	{
		var dirs = Directory.GetDirectories(directory);
		var files = Directory.GetFiles(directory, "*.json");

		task.MaxValue += dirs.Length + files.Length;

		foreach (var subDir in dirs)
		{
			SortJsonKeys(task, subDir);
			task.Increment(1);
		}

		foreach (var jsonFilename in files)
		{
			var jobject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(jsonFilename));
			if (jobject == null)
				throw new UnpackException(ErrorCode.MalformedJsonData, "Unable to parse JSON document for sorting");

			jobject.Sort();

			File.WriteAllText(jsonFilename, JsonConvert.SerializeObject(jobject, Formatting.Indented));

			task.Increment(1);
		}
	}

	private static bool PatchIndexHtml(string indexFilename, bool hasAuxModule)
	{
		var newContent = new StringBuilder();
		var injected = false;

		using (var sr = new StreamReader(indexFilename))
		{
			while (sr.ReadLine() is { } line)
			{
				// Search for original script import and replace with split script import(s)
				if (line == "    <script src=\"lib/require.js\" data-main=\"index\" async></script>")
				{
					newContent.AppendLine("    <script src=\"interop/app_polyfill.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/polyglot.min.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/phaser.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/three.min.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/playAPI.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/autolayout.kiwi.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/play-container.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/play-phaser-autolayout.js\"></script>");
					newContent.AppendLine("    <script src=\"shared_js_libraries/phaser-state-transition.min.js\"></script>");

					if (hasAuxModule)
						newContent.AppendLine("    <script src=\"app/_aux.js\"></script>");
					newContent.AppendLine("    <script src=\"lib/require.js\" data-main=\"app/index\" async></script>");

					injected = true;
				}
				else
					newContent.AppendLine(line);
			}
		}

		if (injected)
			File.WriteAllText(indexFilename, newContent.ToString());

		return injected;
	}

	private static readonly Regex UglyModuleNameRegex = new("(.+)_(\\d+)", RegexOptions.Compiled);

	private static void SimplifyImports(JsModule module)
	{
		if (module.Imports.Length <= 2)
			return;

		var injectedJs = new StringBuilder();

		for (var i = 2; i < module.Imports.Length; i++)
		{
			var import = module.Imports[i];
			var arg = module.ImportArgs[i];
			var commentArg = arg;

			// Prevent diff spam across versions by normalizing comment
			var match = UglyModuleNameRegex.Match(arg);
			if (match.Success)
				commentArg = $"{match.Groups[1].Value}_n";

			var (className, newClassName, newArg) = ParsePackagePath(import, module.Imports);

			// Search for module subproperty access
			var classReference = $"{arg}.{className}";
			if (module.Content.IndexOf(classReference) != -1)
			{
				// If one exists, create a "static import"
				module.Content.Replace(classReference, newClassName);
				module.Content.Replace(arg, newArg);

				injectedJs.AppendLine($"    const {className} = {newArg}.{className}; // PDPU: {commentArg} -> {newArg}");
			}
			else
			{
				// Otherwise, other properties are accessed, just rename the argument
				module.Content.Replace(arg, newClassName);
				injectedJs.AppendLine($"    // PDPU: {commentArg} -> {newClassName}: only direct accessors");
			}
		}

		// Inject "static imports"
		var injectTarget = $"    \"use strict\";{Environment.NewLine}";
		var injectTargetPos = module.Content.IndexOf(injectTarget) + injectTarget.Length;
		module.Content.Insert(injectTargetPos, injectedJs);
	}

	private static (string ClassName, string DestClassName, string ImportArg) ParsePackagePath(string path, string[] moduleImports)
	{
		var pathParts = path.Split('/');
		var className = pathParts[^1];
		var importArg = string.Join("$", pathParts.Select(s => Regex.Replace(s, "[\\W_]", "_")));

		// prevent args for modules in the root directory (i.e. no extra path) from
		// conflicting with the resulting variable name
		if (pathParts.Length == 1)
			importArg = $"_{importArg}";

		// Don't modify the first two args (require and exports)
		if (moduleImports.Length <= 2)
			return (className, className, importArg);

		// Make sure two imported classes don't have the same name but different paths
		// because the resulting variable name conflict edge case isn't handled 
		if (moduleImports.Skip(2).Count(s => s.Split('/')[^1] == className) > 1)
			throw new UnpackException(ErrorCode.JsPackageImportConflict, "Importing two classes of the same name is unsupported");

		// TODO: suffix with a number if two classes have the same name but different packages
		return (className, className, importArg);
	}

	private static (List<JsModule> NamedModules, JsModule? AuxiliaryModule) ParseUglifiedModules(ProgressTask task, string uglifiedJsFilename)
	{
		var moduleDefinitionRegex = new Regex("define\\(\"(.*?)\", \\[(.*?)\\], function \\((.*?)\\) \\{", RegexOptions.Compiled);
		var nonModuleCode = new StringBuilder();

		var modules = new List<JsModule>();

		JsModule? currentModule = null;

		using var sr = new StreamReader(uglifiedJsFilename);
		while (sr.ReadLine() is { } line)
		{
			var match = moduleDefinitionRegex.Match(line);
			if (match.Success)
			{
				// New module start
				var modulePath = match.Groups[1].Value;
				var importedModulePaths = ParseStringArray(match.Groups[2].Value);
				var importedModuleArguments = ParseArgumentArray(match.Groups[3].Value);
				currentModule = new JsModule(modulePath, importedModulePaths, importedModuleArguments);

				currentModule.Content.AppendLine(line);
			}
			else if (line == "});")
			{
				// Module end
				if (currentModule == null)
					throw new InvalidOperationException("Attempted to terminate undefined module");

				currentModule.Content.AppendLine(line);

				modules.Add(currentModule);
				currentModule = null;
			}
			else if (currentModule != null)
				currentModule.Content.AppendLine(line);
			else
				nonModuleCode.AppendLine(line);

			task.Value = task.MaxValue = modules.Count;
		}

		if (nonModuleCode.Length == 0)
			return (modules, null);

		task.Value = task.MaxValue = modules.Count + 1;
		return (modules, new JsModule("_aux", Array.Empty<string>(), Array.Empty<string>(), nonModuleCode));
	}

	private static string[] ParseArgumentArray(string arrValue)
	{
		return arrValue
			.Split(", ")
			.ToArray();
	}

	private static string[] ParseStringArray(string arrValue)
	{
		return ParseArgumentArray(arrValue)
			.Select(s => s[1..^1]) // strip quotes
			.ToArray();
	}

	private static void UnpackXapk(ProgressContext ctx, Config config)
	{
		using var process = new ApktoolProcess(config.ApktoolPath);

		var task = ctx.AddTask("Unpack XAPK", maxValue: 1);
		process.Run(config.SourcePath, Path.Combine(config.DestPath, "unpacked_xapk"));

		task.Increment(1);
		task.StopTask();
	}

	public static void MoveAll(ProgressTask task, string src, string dest)
	{
		MoveAll(task, new DirectoryInfo(src), new DirectoryInfo(dest));
	}

	public static void MoveAll(ProgressTask task, DirectoryInfo src, DirectoryInfo dest, string? tag = null)
	{
		if (src.FullName == dest.FullName)
			return;

		Directory.CreateDirectory(dest.FullName);

		var srcFiles = src.GetFiles();
		var srcDirs = src.GetDirectories();

		task.MaxValue += srcFiles.Length + srcDirs.Length;

		foreach (var fileInfo in srcFiles)
		{
			var destFileName = Path.Combine(dest.ToString(), fileInfo.Name);
			if (File.Exists(destFileName))
			{
				if (tag == null)
					throw new UnpackException(ErrorCode.DestFileExists, $"Destination file {destFileName} exists and no overwrite identifier tag was defined");
				destFileName = Path.Combine(
					Path.GetDirectoryName(destFileName) ?? throw new InvalidOperationException(),
					$"{Path.GetFileNameWithoutExtension(destFileName)}-{tag}{Path.GetExtension(destFileName)}"
				);
			}

			fileInfo.MoveTo(destFileName, true);
			task.Increment(1);
		}

		foreach (var dirInfo in srcDirs)
		{
			MoveAll(task, dirInfo, dest.CreateSubdirectory(dirInfo.Name), tag);
			task.Increment(1);
		}
	}
}