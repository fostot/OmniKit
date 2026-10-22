using System.Collections;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Console.Error.WriteLine("Usage: RuntimeSmoke <release-directory> on Windows x64. No game, microphone or network access is used.");
            return 2;
        }

        var context = new PackageContext(Path.GetFullPath(args[0]));
        try
        {
            CheckSqlite(context);
            CheckDpapi(context);
            CheckSpeech(context);
            Console.WriteLine("Packaged runtime dependency checks passed; no game code was loaded.");
            return 0;
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: not null } invocation
                ? invocation.InnerException! : exception;
            Console.Error.WriteLine($"Packaged runtime check failed ({cause.GetType().Name}).");
            return 1;
        }
    }

    private static void CheckSqlite(PackageContext context)
    {
        var folder = Path.Combine(Path.GetTempPath(), "OmniKit-runtime-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var assembly = context.LoadPackage("Microsoft.Data.Sqlite");
            var type = assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection", true)!;
            using var connection = (DbConnection)Activator.CreateInstance(type,
                "Data Source=" + Path.Combine(folder, "probe.db") + ";Pooling=False")!;
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Probe(Value INTEGER NOT NULL); INSERT INTO Probe VALUES (42); SELECT Value FROM Probe;";
            if (Convert.ToInt64(command.ExecuteScalar()) != 42) throw new InvalidOperationException();
            command.CommandText = "SELECT sqlite_version();";
            var sqliteVersion = (string)command.ExecuteScalar()!;
            if (Version.Parse(sqliteVersion) < new Version(3, 50, 2)) throw new InvalidOperationException();
            Console.WriteLine($"SQLite {sqliteVersion}: packaged native library and temporary database read/write passed.");
        }
        finally
        {
            var fullPath = Path.GetFullPath(folder);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullPath).StartsWith("OmniKit-runtime-smoke-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the generated test directory.");
            Directory.Delete(fullPath, true);
        }
    }

    private static void CheckDpapi(PackageContext context)
    {
        var assembly = context.LoadPackage("System.Security.Cryptography.ProtectedData");
        var type = assembly.GetType("System.Security.Cryptography.ProtectedData", true)!;
        var scopeType = assembly.GetType("System.Security.Cryptography.DataProtectionScope", true)!;
        var scope = Enum.Parse(scopeType, "CurrentUser");
        var protect = type.GetMethod("Protect", [typeof(byte[]), typeof(byte[]), scopeType])!;
        var unprotect = type.GetMethod("Unprotect", [typeof(byte[]), typeof(byte[]), scopeType])!;
        byte[] input = Encoding.UTF8.GetBytes("OmniKit synthetic runtime probe");
        byte[] entropy = Encoding.UTF8.GetBytes("OmniKit.RuntimeSmoke");
        byte[]? encrypted = null;
        byte[]? recovered = null;
        try
        {
            encrypted = (byte[])protect.Invoke(null, [input, entropy, scope])!;
            recovered = (byte[])unprotect.Invoke(null, [encrypted, entropy, scope])!;
            if (!input.AsSpan().SequenceEqual(recovered) || input.AsSpan().SequenceEqual(encrypted))
                throw new InvalidOperationException();
            Console.WriteLine("DPAPI: packaged assembly current-user encryption round-trip passed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(entropy);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (recovered is not null) CryptographicOperations.ZeroMemory(recovered);
        }
    }

    private static void CheckSpeech(PackageContext context)
    {
        var assembly = context.LoadPackage("System.Speech");
        var type = assembly.GetType("System.Speech.Recognition.SpeechRecognitionEngine", true)!;
        var installed = (IEnumerable)type.GetMethod("InstalledRecognizers", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var count = installed.Cast<object>().Count();
        Console.WriteLine($"System.Speech: packaged Windows implementation enumerated {count} installed recognizer(s); microphone untouched.");
        if (count == 0) Console.WriteLine("Voice recognition requires an installed Windows speech recognizer before use.");
    }
}

/// <summary>Uses only dependency files from the supplied release directory.</summary>
internal sealed class PackageContext(string directory) : AssemblyLoadContext("OmniKit runtime smoke")
{
    private static readonly HashSet<string> Packages = new(StringComparer.Ordinal)
    {
        "Microsoft.Data.Sqlite", "SQLitePCLRaw.batteries_v2", "SQLitePCLRaw.core",
        "SQLitePCLRaw.provider.e_sqlite3", "System.Security.Cryptography.ProtectedData", "System.Speech",
    };

    public Assembly LoadPackage(string name) => LoadFromAssemblyPath(Path.Combine(directory, name + ".dll"));

    protected override Assembly? Load(AssemblyName name) =>
        name.Name is not null && Packages.Contains(name.Name) ? LoadPackage(name.Name) : null;

    protected override nint LoadUnmanagedDll(string name)
    {
        if (name is not ("e_sqlite3" or "e_sqlite3.dll")) return 0;
        // RID-specific packaging is expected to flatten this into the release root.
        var library = Path.Combine(directory, "e_sqlite3.dll");
        if (!File.Exists(library)) throw new DllNotFoundException();
        return LoadUnmanagedDllFromPath(library);
    }
}
