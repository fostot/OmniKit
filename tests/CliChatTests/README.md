# CLI chat backend checks

From the repository root on Windows with .NET 10:

```powershell
dotnet run --project tests/CliChatTests/CliChatTests.csproj
python tests/CliChatTests/CodexIsolationProbe.py
```

The first command uses native fake-process fixtures, not provider inference or real sign-in. It checks literal UTF-8 stdin transport, absence of question text in arguments, environment filtering, fixed authentication/error messages, final-only JSON parsing, bounded concurrent stdout/stderr, required isolation options, and cancellation/timeout/unload cleanup. An orphan-child fixture also verifies Windows job-object cleanup after its immediate parent exits. Generated fixtures and recordings remain under ignored local test/build directories.

Observed result:

```text
PASS: CLI stdin/argument isolation, auth/error sanitizing, required flags/version gates, JSON final-only parsing, concurrent output bound, cancellation/timeout/unload/orphan process-tree shutdown.
```

The second command is an opt-in native Codex transport check. It requires the first command's built test apphost and the supported official Codex app binary. It points the actual CLI at a synthetic HTTP provider on loopback, with provider authentication disabled. It does not perform real inference or sign-in, inspect credential files, or print requests, account details, or provider stderr.

Verified with **Codex CLI 0.163.0-alpha.11.1**:

- The actual tool manifest contained only `list_mcp_resources`, `list_mcp_resource_templates`, `read_mcp_resource`, and `request_user_input`.
- Resource and resource-template lists were empty. Reading from a nonexistent MCP server was rejected.
- The synthetic final answer completed successfully. No executable, image-reading, game-action, or connected-provider tool namespace was exposed.
- The CLI still included its global instructions. One additional run with the documented `model_instructions_file` override did not remove them. CLI mode therefore must not claim that only the submitted question is sent.

Both native runs returned two synthetic requests and passed all four resource/tool checks; both reported `global_guidance_included: true`. Production Codex chat fails closed outside that exact tested version.

Claude's installed **2.1.304** help verified the restricted/safe-mode options, empty tool selection, strict MCP configuration, and nonpersistent JSON output. Its backend transport and error paths were tested with fake processes. These checks do not claim a real Claude inference, subscription entitlement, or browser sign-in test.

Persona instructions guide topic and character behavior; these tests do not establish an unbreakable prompt-injection boundary.
