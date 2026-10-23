# Optional AI connections

Open **Settings → Companion → AI**. Choose a connection, connect it, choose a
model where required, then enable AI chat. The launcher, voice action matcher,
HUD and soft interaction work without AI. **No AI** leaves those local features
available and sends no questions to a provider.

All connection choices use the selected companion's XIV/OmniKit instructions.
These instructions guide answers; they are not an absolute prompt-injection
guarantee. AI replies are displayed as text and cannot invoke the game's action
catalogue, run commands, change settings or operate another plugin.

## Continue with ChatGPT

Use **Continue with ChatGPT** to open the official sign-in/consent page in your
system browser. OmniKit registers its own public client using OpenAI's supported
open-source application flow. It does not borrow Codex's client ID or extract
browser cookies. Complete consent and return to settings to load the models
available to that account. An empty or unavailable saved choice defaults to a
lightweight text-chat model from that returned list; a valid manual choice is
kept. This preference uses model-name tiers rather than measured prices. You
can choose another model. Account availability, model access and plan limits
are determined by OpenAI.

If discovery fails, its status distinguishes connection, access, response-format
and size problems from a successfully empty list. Refreshing unsuccessfully
keeps earlier model choices. No unavailable model or alternate billing route is
silently substituted.

Multiple registrations can be kept locally and selected in settings. Credentials
are encrypted with Windows DPAPI for the current Windows user, separately from
the settings database. Sign-in checks PKCE, state, nonce, the callback client ID
and the ID token's signature and claims. Refreshes are serialized and stored
atomically. Signing out clears the selected account's local tokens; a status
message distinguishes any unconfirmed remote revocation.

OmniKit sends the question and companion instructions to the official Responses
endpoint with storage disabled. A streamed reply is accepted only after a
successful completion event. It does not read ChatGPT conversation history,
upload game state or buy additional credits.

## Codex CLI and Claude CLI

These choices run a separately installed native CLI and use that CLI's own
authentication. **Check connection** checks installation, isolation support and
login without asking the model a question. **Sign in** invokes the CLI's own
browser login. OmniKit does not read, copy or export its credentials.

Each request uses a new empty working directory, fixed arguments and the
question on standard input. OmniKit disables action tools and connectors,
bounds captured output, and stops the process tree on cancellation, timeout or
unload. No game or project directory is supplied. Output is accepted only from
the CLI's final assistant reply.

Codex can still add its usual global instructions and runtime context. Claude
uses restricted/safe mode and can remain subject to managed CLI policies.
These are CLI features, so CLI modes should not be treated as an empty provider
context identical to a direct API request. CLI requests consume the selected
CLI account's normal usage allowance.

Compatibility is checked before a request. Codex's tool surface changes between
versions; this preview supports the specifically tested `0.163.0-alpha.11.1`
native binary and refuses other versions rather than silently enabling tools.
Claude requires its restricted/safe-mode options (tested with `2.1.304`). A blank
CLI model field uses that CLI's default. Model aliases must be valid for that CLI.

## API key or local Responses server

Choose **API key**, enter a Responses-compatible HTTPS endpoint and model, and
save its key. The key is encrypted separately and bound to that exact endpoint.
Changing endpoints does not send the old key to a different provider. HTTP is
accepted only for a literal loopback address, for a local server. Redirects,
embedded URL credentials, query strings and fragments are rejected.

API billing is separate from a ChatGPT or Claude subscription. A compatible
local server can work without a key. Only the submitted question and companion
instructions are included in the API request; no game state is collected.

## Local data

Settings exports include the connection choice and model preferences, never
API keys, OAuth registrations/tokens, CLI credentials, conversations or audio.
An import cannot silently grant permission to send questions to a new
connection. Review custom endpoints and voice phrases before sharing exports.

Provider failures and login diagnostics are shown as bounded, generic status
messages. Raw tokens, browser authorization URLs and raw CLI error output are
not written to OmniKit logs or displayed as answers.

## Provider references

- [OpenAI sign-in flow](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [Profiles, sessions and refresh](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions)
- [Models and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference)
- [Preview limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
