namespace OmniKit.Browser;

internal sealed class OwnedProfile : IDisposable
{
    private const string Marker = ".omnikit-browser-profile";
    private const string Signature = "OmniKit.Browser profile v1";
    private readonly FileStream ownership;
    internal string Path { get; }

    internal OwnedProfile(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path)) throw new ArgumentException();
        Path = System.IO.Path.GetFullPath(path);
        if (Path.TrimEnd(System.IO.Path.DirectorySeparatorChar).Length <= (System.IO.Path.GetPathRoot(Path)?.Length ?? 0)) throw new ArgumentException();
        if (Directory.Exists(Path) && (File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException();
        Directory.CreateDirectory(Path);
        var marker = System.IO.Path.Combine(Path, Marker);
        if (!File.Exists(marker))
        {
            if (Directory.EnumerateFileSystemEntries(Path).Any()) throw new InvalidOperationException();
            File.WriteAllText(marker, Signature);
        }
        if (new FileInfo(marker).Length > 128 || File.ReadAllText(marker) != Signature) throw new InvalidOperationException();
        ownership = new FileStream(System.IO.Path.Combine(Path, ".omnikit-browser-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public void Dispose() => ownership.Dispose();
}
