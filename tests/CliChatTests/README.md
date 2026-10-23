# CLI chat backend checks

From the repository root on Windows with .NET 10:

```powershell
dotnet run --project tests/CliChatTests/CliChatTests.csproj
```

Production CLI generation is paused for both Codex and Claude. One process does not prove one model request: a CLI may retry or continue its own agent loop. The public generation entry point is checked to return the pause notice without executable discovery, process launch, or workspace creation. Sign-in, connection checks, and saved choices remain available.

The command uses native fake-process fixtures, not provider inference or real sign-in. Test-only reflection exercises the suspended private transport against the fixture executable; no production entry point invokes that transport. These checks cover literal UTF-8 stdin, absence of question text in arguments, environment filtering, fixed authentication/error messages, final-only JSON parsing, bounded stdout/stderr, required isolation options, and cancellation/timeout/unload cleanup. An orphan-child fixture verifies Windows job-object cleanup after its immediate parent exits. Fixtures and recordings remain under ignored local test/build directories.

Observed result:

```text
PASS: production CLI inference paused with zero process launches; suspended synthetic transport stdin/argument isolation, auth/error sanitizing, required flags/version gates, JSON final-only parsing, concurrent output bound, cancellation/timeout/unload/orphan process-tree shutdown.
```

The retained `CodexIsolationProbe.py` records earlier native transport evidence. It is not part of the current cost-guard checks. That probe used the official CLI with a synthetic loopback HTTP provider and provider authentication disabled; it did not use real inference, sign-in, or credential files.

Verified with **Codex CLI 0.163.0-alpha.11.1**:

- The actual tool manifest contained only `list_mcp_resources`, `list_mcp_resource_templates`, `read_mcp_resource`, and `request_user_input`.
- Resource and resource-template lists were empty. Reading from a nonexistent MCP server was rejected.
- The synthetic final answer completed successfully. No executable, image-reading, game-action, or connected-provider tool namespace was exposed.
- The CLI still included its global instructions. One additional run with the documented `model_instructions_file` override did not remove them. CLI mode therefore must not claim that only the submitted question is sent.

Both native runs returned two synthetic requests and passed all four resource/tool checks; both reported `global_guidance_included: true`. This is isolation evidence, not a single-request guarantee. Production CLI chat now fails closed for every version until a supported single-request limit is verified.

Claude's installed **2.1.304** help verified the restricted/safe-mode options, empty tool selection, strict MCP configuration, and nonpersistent JSON output. Its backend transport and error paths were tested with fake processes. These checks do not claim a real Claude inference, subscription entitlement, or browser sign-in test.

Persona instructions guide topic and character behavior; these tests do not establish an unbreakable prompt-injection boundary.
