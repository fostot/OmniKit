# Browser facade lifecycle checks

```powershell
dotnet run --project tests/BrowserFacadeTests/BrowserFacadeTests.csproj -c Release
```

The production `WikiBrowser.cs` is compiled unchanged. Its small process boundary is replaced with synthetic in-memory helpers; host/profile access throws. No browser executable, game, account, credentials, network or real process is accessed.

Checks cover 64 concurrent submissions accepting exactly eight commands including the active write; rejection without semaphore waiters; readiness requiring a validated owned HWND; error, closure and process exit revoking readiness; startup and ready-handshake timeout; cancellation-ignoring pipe/shutdown stalls; and unload while native startup remains pending. A late synthetic startup is terminated and never published. Shutdown never overlaps an outstanding write or sends queued navigation after unload.

The production defaults are ten seconds for startup plus its ready handshake, five seconds per pipe write, and three seconds for graceful shutdown. Calls enqueue without waiting. Windows does not provide cancellation for `Process.Start`; a timed-out start retains one cleanup owner, prevents additional starts while pending, and terminates a late returned process. These tests prove the facade's ownership/state behavior, not operating-system termination guarantees or live game integration.
