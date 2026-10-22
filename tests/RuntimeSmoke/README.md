# Packaged dependency smoke checks

This executable uses reflection to load dependency DLLs from the supplied release
directory. It does not reference NuGet copies of those assemblies, load OmniKit or
Dalamud game code, start a microphone, access credentials, or make network calls.

After the Windows x64 release build:

```powershell
dotnet run --project tests/RuntimeSmoke/RuntimeSmoke.csproj -c Release -- src/OmniKit/bin/Release
```

Checks: a temporary SQLite database round-trip using the packaged native DLL,
current-user Windows DPAPI with synthetic bytes, and enumeration of installed
speech recognizers through the packaged Windows System.Speech implementation.
The temporary database is removed after the check.

A successful run establishes dependency availability in a Windows x64 .NET
process. It does not prove loading inside Dalamud's plugin context, recognition
quality, microphone permissions, or correct gameplay/UI behavior. Zero installed
recognizers is reported without opening a microphone and requires Windows speech
setup before voice input can work.
