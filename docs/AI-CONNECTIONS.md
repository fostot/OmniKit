# Optional AI connections

Open **Settings → Companion → AI**. Choose a connection, connect it, choose a
model where required, then enable AI chat. ChatGPT and API connections can answer
questions; CLI chat is paused as described below. The launcher, voice action matcher,
HUD and soft interaction work without AI. **No AI** leaves those local features
available and sends no questions to a provider.

Available chat connections use the selected companion's XIV/OmniKit instructions.
These instructions guide answers; they are not an absolute prompt-injection
guarantee. AI replies are displayed as text and cannot invoke the game's action
catalogue, run commands, change settings or operate another plugin.

Each explicit **Ask companion** submission makes at most one inference request.
The service rejects overlapping submissions immediately instead of queuing them.
It does not retry failures, switch providers, regenerate, continue tool calls or
send follow-up questions automatically. Cancellation and timeout do not trigger
another request. A spoken question fills the question field; press **Ask
companion** to send it. The roaming companion's comments are local text and
never call AI. Sign-in, token refresh and model-list requests are authentication
or metadata operations, not inference.

This limits OmniKit's requests, not the provider's token price or bill. A single
answer can use a variable number of tokens. Canceling cannot guarantee that the
provider stops processing or billing; there is no hard billing cap.

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

OmniKit sends the question, bounded local session context and companion instructions to the official Responses
endpoint with storage disabled. A streamed reply is accepted only after a
successful completion event. It does not read ChatGPT conversation history,
upload game state or buy additional credits. No action tools are supplied. The
subscription flow does not accept the API's `max_output_tokens` option, so OmniKit
does not send that unsupported field. Input and displayed answer length remain
bounded locally; that display limit is not a provider token budget.

## Codex CLI and Claude CLI

**CLI chat is paused: the single-request limit cannot be verified. Use ChatGPT
or API for questions.** Neither CLI's generation entry point discovers an
executable or starts a process. Saved CLI choices and model preferences remain
intact.

Connection and sign-in controls use a separately installed native CLI and its own
authentication. **Check connection** checks installation, isolation support and
login without asking the model a question. **Sign in** invokes the CLI's own
browser login. OmniKit does not read, copy or export its credentials.

One CLI process is not proof of one inference: the CLI may retry internally or
continue an agent loop. Earlier isolation checks established restricted tool
access, not a single-request guarantee. Those checks used Codex
`0.163.0-alpha.11.1`; Claude's restricted/safe-mode options were checked with
`2.1.304`. The connection check keeps those compatibility checks, but passing
them does not enable generation. Re-enabling CLI chat requires a separately
verified supported request limit.

The suspended transport also showed that Codex adds global instructions and
runtime context; Claude can remain subject to managed policies. Those contexts
must not be described as only the submitted question. Process-isolation tests
use synthetic executables and do not establish subscription entitlement or
provider cost.

## API key or local Responses server

Choose **API key**, enter a Responses-compatible HTTPS endpoint and model, and
save its key. The key is encrypted separately and bound to that exact endpoint.
Changing endpoints does not send the old key to a different provider. HTTP is
accepted only for a literal loopback address, for a local server. Redirects,
embedded URL credentials, query strings and fragments are rejected.

API billing is separate from a ChatGPT or Claude subscription. A compatible
local server can work without a key. The submitted question, bounded successful
session turns and companion instructions are included; no game state is collected. One
Responses POST supplies an empty tool list and requests `max_output_tokens=1000`
with storage disabled. Provider errors are shown without an automatic retry or
fallback.

## Saved chat session

API and ChatGPT chat keep up to eight completed question/answer pairs, within
24,000 characters of prior context. Oldest complete pairs drop off first; the
current question can add up to 4,000 characters. No background summarization or
extra model requests are used. Failed, canceled or incomplete replies do not
become context. This is conversational text memory, not a full provider-side
reasoning trace or an import of your ChatGPT history.

The whole payload is encrypted using Windows DPAPI CurrentUser in separate
`omnikit.db` tables. It survives plugin and game restarts under the same Windows
account, and the last successful answer reappears when chat opens. Settings
JSON never includes it. A storage warning means current context is memory-only;
unreadable/future data is preserved until an explicit reset.

**New session** clears saved local context, the visible answer and the question
field. It sends no request, cancels/detaches pending UI work, and prevents an old
completion from restoring cleared context. A failed disk clear is reported and
does not claim success. This does not delete information already received by a
provider or cancel charges already incurred.

Changing connection type, API endpoint or selected ChatGPT account isolates
context. Replacing/removing an API key also clears the session; a failed clear
blocks further questions until resolved. Other game instances changing saved
context cause a conflict notice rather than silently overwriting it.

Each follow-up is still at most one inference request. Replayed context uses
input tokens and can increase costs; a new session reduces the next input.
Manual user/assistant history follows the [official OpenAI conversation-state
guide](https://developers.openai.com/api/docs/guides/conversation-state).

## Other local data

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
