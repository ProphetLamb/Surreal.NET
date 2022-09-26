namespace SurrealDB.Shared.Tests;

public static class SurrealInstance {
    public static Process Create() {
        // Wait until port is closed
        TcpHelper.SpinUntilClosed($"{TestHelper.Loopback}:{TestHelper.Port}");
        // We have a race condition between when the SurrealDB socket opens (or closes), and when it accepts connections.
        // 50ms should be enough to run the tests on most devices.
        // Thread.Sleep(50);
        // Assume we have surreal as a command in PATH
        string? path = GetFullPath("surreal");
        Debug.Assert(path is not null);

        // Start new instances
        var p = Instantiate(path!);
        Debug.Assert(p is not null);
        TcpHelper.SpinUntilListening($"{TestHelper.Loopback}:{TestHelper.Port}");
        // Thread.Sleep(50);
        Debug.Assert(!p.HasExited);
        return p;
    }

    private static Process? Instantiate(string path) {
        ProcessStartInfo pi = new() {
            CreateNoWindow = true,
            UseShellExecute = false,
            FileName = path,
            Arguments = $"start memory -b {TestHelper.Loopback}:{TestHelper.Port} -u {TestHelper.User} -p {TestHelper.Pass} --log debug",
            WorkingDirectory = Environment.CurrentDirectory
        };
        return Process.Start(pi);
    }

    public static string? GetFullPath(string file)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            file += ".exe";
        }

        if (File.Exists(file)) {
            return Path.GetFullPath(file);
        }

        var values = Environment.GetEnvironmentVariable("PATH");
        if (values is null) {
            return null;
        }
        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, file);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }

}
