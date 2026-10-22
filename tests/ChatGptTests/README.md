# Offline ChatGPT connection checks

```powershell
dotnet run --project tests/ChatGptTests/ChatGptTests.csproj -c Release
```

The 73 checks use synthetic RSA keys and identities, temporary Windows DPAPI
storage, a real loopback callback socket and fake HTTP responses. They do not
open a browser, contact a provider, read real credentials or perform inference.

Coverage includes PKCE fields, callback state/client/host/path/duplicate checks,
signed identity claims and critical-header rejection, pinned discovery/JWKS,
scope-denied and failed reauthorization preserving prior credentials, model
filtering, and sign-out. Distinct-account fixtures verify that stale instances
cannot send requests, reauthorize or sign out a different displayed account.

Refresh fixtures check cross-instance serialization, validation downloads before
rotation, and persistence of a verified replacement when cancellation arrives
after the full response. Streaming checks cover split UTF-8, many small events
exceeding 256 KiB, bounded individual events and output, and rejection of failed,
incomplete or malformed streams. These checks do not establish real account
entitlement, browser consent behavior or successful live provider inference.
