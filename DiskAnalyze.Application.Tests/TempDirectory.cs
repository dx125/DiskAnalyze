using System.Diagnostics;

namespace DiskAnalyze.Application.Tests
{
    /// <summary>A unique scratch directory under %TEMP%, deleted on dispose.</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiskAnalyzeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        public string CreateDirectory(string relativePath) => Directory.CreateDirectory(Combine(relativePath)).FullName;

        public string WriteFile(string relativePath, byte[] content)
        {
            var fullPath = Combine(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
            return fullPath;
        }

        public string WriteFile(string relativePath, string content) =>
            WriteFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

        /// <summary>Creates an NTFS junction (no admin rights needed, unlike symbolic links).</summary>
        public string CreateJunction(string relativeLinkPath, string targetPath)
        {
            var linkPath = Combine(relativeLinkPath);
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"mklink failed: {process.StandardError.ReadToEnd()}");

            _junctions.Add(linkPath);
            return linkPath;
        }

        public void Dispose()
        {
            try
            {
                // Remove junctions first: a non-recursive delete removes the link itself, never its target.
                foreach (var junction in _junctions)
                {
                    Directory.Delete(junction, recursive: false);
                }

                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort; %TEMP% is cleaned eventually.
            }
        }

        private readonly List<string> _junctions = new();
    }

    /// <summary>Records reports synchronously (unlike <see cref="Progress{T}"/>, which posts them).</summary>
    internal sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly List<T> _reports = new();

        public IReadOnlyList<T> Reports
        {
            get { lock (_reports) return _reports.ToList(); }
        }

        public void Report(T value)
        {
            lock (_reports) _reports.Add(value);
        }
    }
}
