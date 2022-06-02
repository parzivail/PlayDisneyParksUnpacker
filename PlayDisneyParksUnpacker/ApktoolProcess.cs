using System.Diagnostics;

namespace PlayDisneyParksUnpacker;

public sealed class ApktoolProcess : IDisposable
{
	private Process _process;

	public ApktoolProcess(string path)
	{
		_process = new Process();
		_process.StartInfo.FileName = path;
		_process.StartInfo.UseShellExecute = false;
		_process.StartInfo.CreateNoWindow = true;

		// _process.StartInfo.RedirectStandardOutput = true;
		// _process.StartInfo.RedirectStandardError = true;
		// _process.OutputDataReceived += OnProcessOnOutputDataReceived;
		// _process.ErrorDataReceived += OnProcessOnOutputDataReceived;
	}

	public void Run(string sourceFile, string? outputDir = null, bool baksmali = true)
	{
		var args = $"d \"{sourceFile}\"";

		if (!baksmali)
			args += " -s";

		if (outputDir != null)
			args += $" -f -o \"{outputDir}\"";

		_process.StartInfo.Arguments = args;
		_process.Start();
		// _process.BeginOutputReadLine();
		// _process.BeginErrorReadLine();
		_process.WaitForExit();
		// _process.CancelOutputRead();
		// _process.CancelErrorRead();
	}

	private static void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args)
	{
		if (string.IsNullOrWhiteSpace(args.Data))
			return;

		Console.WriteLine(args.Data);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_process.Dispose();
	}
}