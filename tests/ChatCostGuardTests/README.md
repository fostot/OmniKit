# Explicit-request AI checks

Run from the repository root on Windows with .NET 10:

```powershell
dotnet run --project tests/ChatCostGuardTests/ChatCostGuardTests.csproj -c Release
```

The harness links the actual ChatService, ChatGPT connection, and CLI backend sources. Test-only reflection replaces HTTP transports with counting handlers before any operation. Generated DPAPI records contain only synthetic account and token strings in a new ignored test directory. No external sign-in, real credentials, provider requests, or CLI inference are used.

The 77 assertions check immediate rejection of concurrent submissions, exactly one inference POST for an accepted explicit API or ChatGPT question, provider/model selection, no queued duplicates, no automatic retry or fallback after failures, no tool continuation, cancellation, timeout, and unloading. They also check API output limits and the absence of unsupported `max_output_tokens` in ChatGPT subscription requests. Existing `ChatGptTests` separately cover authentication, refresh rotation, account pinning, and model metadata requests.

Both CLI providers return the paused notice before launching a process. `CliChatTests` verifies that public boundary and separately tests the suspended transport with fake executable fixtures. Process count is not treated as model-request count.

These checks establish OmniKit's request behavior, not a hard provider billing cap. Canceling a request cannot guarantee the provider stops processing or billing. A new explicit question can make a new request after the previous operation ends; no background AI is scheduled.
