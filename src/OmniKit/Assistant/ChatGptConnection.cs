using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmniKit.Assistant;

public sealed record ChatGptAccountInfo(string Id, string Label, bool IsActive, bool IsConnected);

/// <summary>
/// Public-client OAuth for OpenAI's documented open-source sign-in flow.
/// https://developers.openai.com/siwc/token-sharing-open-source/sign-in
/// Never reads another application's authentication files or executes model output.
/// </summary>
public sealed class ChatGptConnection : IDisposable
{
    internal const string MetadataUrl = "https://auth.openai.com/.well-known/openid-configuration";
    internal const string JwksUrl = "https://auth.openai.com/.well-known/jwks.json";
    internal const string AuthorizeUrl = "https://auth.openai.com/api/accounts/authorize";
    internal const string TokenUrl = "https://auth.openai.com/api/accounts/oauth/token";
    private const string Resource = "https://api.openai.com/v1";
    private const string Scopes = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
    private const string AccountChangedStatus = "The selected ChatGPT account changed in another game instance. Select the intended account in OmniKit before continuing.";
    private readonly ChatGptAuthStore store;
    private readonly HttpClient client;
    private readonly Func<Uri, CancellationToken, Task> openBrowser;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object cancellationGate = new();
    private CancellationTokenSource session = new();
    private volatile ChatGptAccountInfo[] accounts = [];
    private volatile string status = "Not connected to ChatGPT.";
    private bool disposed;
    private int generating;

    public ChatGptConnection(DirectoryInfo configDirectory, Func<IDisposable>? externalBrowserScope = null) : this(configDirectory,
        new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false })
        { Timeout = TimeSpan.FromSeconds(120) }, (uri, _) =>
        {
            using var bypass = externalBrowserScope?.Invoke();
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return Task.CompletedTask;
        }) { }

    internal ChatGptConnection(DirectoryInfo configDirectory, HttpClient client, Func<Uri, CancellationToken, Task> openBrowser)
    {
        store = new ChatGptAuthStore(configDirectory); this.client = client; this.openBrowser = openBrowser;
        try { Publish(store.Read()); }
        catch { status = "Saved ChatGPT credentials could not be read from protected Windows storage."; }
    }
    public string Status => status;
    public IReadOnlyList<ChatGptAccountInfo> Accounts => Array.AsReadOnly(accounts);
    public bool IsConnected => accounts.Any(account => account.IsActive && account.IsConnected);

    public void SelectAccount(string id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!gate.Wait(0)) throw new InvalidOperationException("Wait for the current connection operation to finish.");
        try
        {
            using var fileLock = store.TryLock();
            var state = store.Read();
            if (!state.Accounts.Any(account => account.Id == id)) throw new InvalidOperationException();
            state.ActiveId = id; store.Save(state); CancelSession(); Publish(state);
            status = IsConnected ? "Saved ChatGPT account selected." : "Saved account selected. Continue with ChatGPT to reconnect.";
        }
        catch { throw new InvalidOperationException("The saved ChatGPT account could not be selected. Try again."); }
        finally { gate.Release(); }
    }

    public Task<string> SignInAsync(CancellationToken ct) => SignInAsync(ct, false);

    public async Task<string> SignInAsync(CancellationToken ct, bool addAccount)
    {
        var expectedAccount = accounts.FirstOrDefault(account => account.IsActive)?.Id;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        cancel.CancelAfter(TimeSpan.FromMinutes(3));
        var entered = false;
        try
        {
            await gate.WaitAsync(cancel.Token).ConfigureAwait(false); entered = true;
            ChatGptAuthState saved;
            using (await store.LockAsync(cancel.Token).ConfigureAwait(false))
            {
                saved = store.Read();
                if (!addAccount && !string.Equals(saved.ActiveId, expectedAccount ?? "", StringComparison.Ordinal))
                    throw new AccountChangedException();
                if (saved.HostId.Length == 0) { saved.HostId = "urn:uuid:" + Guid.NewGuid().ToString("D"); store.Save(saved); }
            }
            var selected = addAccount ? null : saved.Accounts.FirstOrDefault(account => account.Id == expectedAccount);
            if (selected is null && saved.Accounts.Count >= 32) return status = "The saved account limit has been reached.";
            await MetadataAsync(cancel.Token).ConfigureAwait(false);
            var state = Random(); var nonce = Random(); var verifier = Random();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            ChatGptCallback callback;
            var redirect = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/auth/callback");
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["client_id"] = selected?.ClientId ?? "dynamic_agent_client",
                    ["ext_agent_host_id"] = saved.HostId, ["response_type"] = "code",
                    ["redirect_uri"] = redirect.AbsoluteUri, ["scope"] = Scopes, ["resource"] = Resource,
                    ["state"] = state, ["nonce"] = nonce, ["code_challenge_method"] = "S256",
                    ["code_challenge"] = ChatGptTokenValidator.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
                };
                if (selected is null) parameters["agent_name_hint"] = "OmniKit";
                // Login/ID-token hints are optional. Omit them to keep identity tokens out of browser-launch URLs.
                var authorization = new Uri(AuthorizeUrl + "?" + string.Join("&", parameters.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))));
                status = "Complete sign-in in your browser, then return here. You can cancel.";
                await openBrowser(authorization, cancel.Token).ConfigureAwait(false);
                callback = await ChatGptLoopback.ReceiveAsync(listener, redirect, state, selected?.ClientId, cancel.Token).ConfigureAwait(false);
            }
            finally { listener.Stop(); }
            using var token = await TokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["client_id"] = callback.ClientId, ["code"] = callback.Code,
                ["code_verifier"] = verifier, ["redirect_uri"] = redirect.AbsoluteUri, ["resource"] = Resource,
            }, cancel.Token).ConfigureAwait(false);
            var idToken = ChatGptTokenValidator.Text(token.RootElement, "id_token");
            var keys = await GetAsync(JwksUrl, null, cancel.Token).ConfigureAwait(false);
            var identity = ChatGptTokenValidator.Validate(idToken, keys, callback.ClientId, nonce, DateTimeOffset.UtcNow);
            if (selected is not null && (identity.Subject != selected.Subject || callback.ClientId != selected.ClientId))
                throw new InvalidOperationException();
            var registration = new ChatGptRegistration
            {
                Id = selected?.Id ?? Guid.NewGuid().ToString("N"),
                ClientId = callback.ClientId, Subject = identity.Subject, Email = identity.Email,
                Label = selected?.Label ?? ((identity.Email.Length > 0 ? identity.Email : "ChatGPT account") + " · " + (saved.Accounts.Count + 1)),
            };
            ApplyTokens(registration, token.RootElement, requireScope: true);
            if (!registration.CanUsePlan)
                return status = "ChatGPT plan use was not granted. The previous account was kept. Authorize plan use or choose another connection.";
            using (await store.LockAsync(cancel.Token).ConfigureAwait(false))
            {
                var current = store.Read();
                if (!addAccount && !string.Equals(current.ActiveId, expectedAccount ?? "", StringComparison.Ordinal))
                    throw new AccountChangedException();
                if (current.Accounts.Any(account => account.ClientId == registration.ClientId && account.Subject != registration.Subject))
                    throw new InvalidOperationException();
                var existing = current.Accounts.FirstOrDefault(account => account.ClientId == registration.ClientId && account.Subject == registration.Subject);
                if (existing is not null) { registration.Id = existing.Id; registration.Label = existing.Label; current.Accounts.Remove(existing); }
                current.Accounts.Add(registration);
                current.ActiveId = registration.Id;
                store.Save(current); Publish(current);
            }
            CancelSession();
            return status = "Connected to ChatGPT. Refresh models to choose an available model.";
        }
        catch (AccountChangedException) { return status = AccountChangedStatus; }
        catch (OperationCanceledException) { return status = "ChatGPT sign-in cancelled or timed out. Your previous account was kept."; }
        catch { return status = "ChatGPT sign-in could not be verified or saved. Your previous account was kept. Start a fresh sign-in."; }
        finally { if (entered) gate.Release(); }
    }

    public async Task<string> SignOutAsync(CancellationToken ct)
    {
        var expectedAccount = accounts.FirstOrDefault(account => account.IsActive)?.Id;
        CancelSession();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        cancel.CancelAfter(TimeSpan.FromSeconds(30));
        var entered = false; var remoteConfirmed = false;
        try
        {
            await gate.WaitAsync(cancel.Token).ConfigureAwait(false); entered = true;
            using var fileLock = await store.LockAsync(cancel.Token).ConfigureAwait(false);
            var state = store.Read();
            if (!string.Equals(state.ActiveId, expectedAccount ?? "", StringComparison.Ordinal)) throw new AccountChangedException();
            var account = state.Accounts.FirstOrDefault(value => value.Id == expectedAccount);
            if (account is null) return status = "No ChatGPT account is selected.";
            try
            {
                var endpoint = await MetadataAsync(cancel.Token).ConfigureAwait(false);
                if (endpoint is not null && account.RefreshToken.Length > 0)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = account.RefreshToken, ["token_type_hint"] = "refresh_token", ["client_id"] = account.ClientId }) };
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token).ConfigureAwait(false);
                    remoteConfirmed = response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch { /* Local sign-out still proceeds, and the result reports unconfirmed remote revocation. */ }
            account.ClearTokens(); store.Save(state); Publish(state);
            return status = remoteConfirmed ? "Signed out of ChatGPT and revoked this session." :
                "Signed out locally. Remote revocation was not confirmed; disconnect OmniKit in ChatGPT Settings if needed.";
        }
        catch (AccountChangedException) { return status = AccountChangedStatus; }
        catch { return status = "ChatGPT sign-out could not finish. Try again; the saved registration was retained."; }
        finally { if (entered) gate.Release(); }
    }

    public async Task<IReadOnlyList<AiModelOption>> ListModelsAsync(CancellationToken ct)
    {
        var expectedAccount = accounts.FirstOrDefault(account => account.IsActive)?.Id;
        using var cancel = RequestCancellation(ct);
        try
        {
            var access = await AccessAsync(expectedAccount, cancel.Token).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, Resource + "/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new ModelCatalogException(ModelHttpStatus(response.StatusCode));
            // Catalog entries can include metadata much larger than the displayed
            // id/name pair. Keep this bound separate from token/JWKS responses.
            string body;
            try { body = await ReadBoundedAsync(response.Content, cancel.Token, 2 * 1024 * 1024).ConfigureAwait(false); }
            catch (InvalidOperationException) { throw new ModelCatalogException("The ChatGPT model catalog exceeded the supported size. No models were replaced."); }
            var result = ParseModels(body);
            status = result.Count > 0 ? $"Loaded {result.Count} available ChatGPT model(s)." :
                "ChatGPT returned no selectable models for this account. Sign-in succeeded, but no model was offered for selection.";
            return result;
        }
        catch (AccountChangedException) { status = AccountChangedStatus; throw new InvalidOperationException(status); }
        catch (ModelCatalogException error) { status = error.Message; throw new InvalidOperationException(status); }
        catch (OperationCanceledException) { status = ct.IsCancellationRequested ? "Model refresh canceled." : "The ChatGPT model request timed out. Try refreshing again."; throw new InvalidOperationException(status); }
        catch (HttpRequestException) { status = "Could not reach the ChatGPT model service. Check the connection and refresh again."; throw new InvalidOperationException(status); }
        catch (JsonException) { status = "ChatGPT returned an unreadable model catalog. Refresh again or update OmniKit."; throw new InvalidOperationException(status); }
        catch (InvalidDataException) { status = "ChatGPT returned an unsupported model catalog format. Refresh again or update OmniKit."; throw new InvalidOperationException(status); }
        catch { status = "The ChatGPT model catalog could not be loaded. Check the selected account and reconnect if needed."; throw new InvalidOperationException(status); }
    }

    internal static IReadOnlyList<AiModelOption> ParseModels(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        try { ChatGptTokenValidator.RejectDuplicates(root); }
        catch (InvalidOperationException) { throw new InvalidDataException(); }
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        // SIWC documents models/slug/display_name/visibility. The public Models
        // API also documents data/id without a visibility field. Prefer SIWC
        // whenever present, so a hidden or empty catalog cannot fall through.
        var siwc = root.TryGetProperty("models", out var models);
        if (!siwc && !root.TryGetProperty("data", out models)) throw new InvalidDataException();
        if (models.ValueKind != JsonValueKind.Array || models.GetArrayLength() > 1000) throw new InvalidDataException();
        var result = new List<AiModelOption>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in models.EnumerateArray())
        {
            if (model.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            if (siwc && ChatGptTokenValidator.Text(model, "visibility") != "list") continue;
            if (!siwc && model.TryGetProperty("visibility", out var visibility) &&
                (visibility.ValueKind != JsonValueKind.String || visibility.GetString() != "list")) continue;
            var id = ChatGptTokenValidator.Text(model, siwc ? "slug" : "id");
            if (!ValidModel(id) || string.IsNullOrWhiteSpace(id) || id != id.Trim()) throw new InvalidDataException();
            var name = siwc ? ChatGptTokenValidator.Text(model, "display_name") : id;
            if (string.IsNullOrWhiteSpace(name)) name = id;
            if (name.Length > 200 || name.Any(char.IsControl)) throw new InvalidDataException();
            if (ids.Add(id)) result.Add(new(id, name));
        }
        return result;
    }

    internal static string PreferredModel(IReadOnlyList<AiModelOption> models, string current)
    {
        if (models.Any(model => model.Id == current)) return current;
        // The catalog does not document per-model price/rank. This is a name-tier
        // preference, not a pricing measurement; never invent an unavailable id.
        return models.Where(IsTextChatModel).OrderBy(ModelTier)
            .ThenBy(model => model.Id, StringComparer.Ordinal).Select(model => model.Id).FirstOrDefault() ?? "";
    }

    private static bool IsTextChatModel(AiModelOption model)
    {
        var id = model.Id.ToLowerInvariant();
        var text = id + " " + model.Name.ToLowerInvariant();
        if (new[] { "embedding", "image", "audio", "realtime", "transcrib", "whisper", "tts", "moderation", "search", "instruct", "deep-research" }.Any(text.Contains)) return false;
        return id.StartsWith("gpt-", StringComparison.Ordinal) || id.StartsWith("chatgpt-", StringComparison.Ordinal) ||
            id.StartsWith("ft:gpt-", StringComparison.Ordinal) || (id.Length > 1 && id[0] == 'o' && char.IsAsciiDigit(id[1]));
    }

    private static int ModelTier(AiModelOption model)
    {
        var words = (model.Id + " " + model.Name).ToLowerInvariant()
            .Split(['-', '_', ' ', '.', ':', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => word is "pro" or "heavy" or "max" or "astra" or "reasoning")) return 6;
        if (words.Contains("nano")) return 0;
        if (words.Any(word => word is "mini" or "luna" or "small")) return 1;
        if (words.Any(word => word is "light" or "lite" or "fast" or "flash")) return 2;
        if (words.Contains("codex")) return 5;
        return model.Id.StartsWith("o", StringComparison.OrdinalIgnoreCase) ? 5 : 3;
    }

    private static string ModelHttpStatus(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "ChatGPT declined model access (401). Continue with ChatGPT to reconnect.",
        HttpStatusCode.Forbidden => "ChatGPT did not allow model discovery (403). Check plan-use consent or the selected account.",
        HttpStatusCode.TooManyRequests => "ChatGPT limited model discovery (429). Wait before refreshing again.",
        >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "ChatGPT redirected model discovery. Redirects are blocked; update OmniKit or try again later.",
        >= HttpStatusCode.InternalServerError => "The ChatGPT model service is unavailable. Try refreshing later.",
        _ => "ChatGPT did not accept the model-list request. Refresh again or update OmniKit.",
    };

    private sealed class ModelCatalogException(string message) : Exception(message);

    public async Task<string> AskAsync(string instructions, string question, string model, CancellationToken ct)
        => (await AskContextAsync(instructions, [new ChatTurn("user", question)], model, ct).ConfigureAwait(false)).Text;

    internal async Task<ChatReply> AskContextAsync(string instructions, IReadOnlyList<ChatTurn> input, string model, CancellationToken ct)
    {
        if (disposed || input.Count is 0 or > 17 || input[^1].Role != "user" || input[^1].Content.Length is 0 or > 4000
            || input.Sum(turn => turn.Content.Length) > 28000 || input.Any(turn => turn.Role is not ("user" or "assistant"))
            || instructions.Length > 16000 || !ValidModel(model))
            return new("Choose a ChatGPT model and ask a question of at most 4,000 characters.", false);
        if (Interlocked.CompareExchange(ref generating, 1, 0) != 0)
            return new("ChatGPT is still answering the previous question.", false);
        var expectedAccount = accounts.FirstOrDefault(account => account.IsActive)?.Id;
        try
        {
            using var cancel = RequestCancellation(ct);
            var access = await AccessAsync(expectedAccount, cancel.Token).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, Resource + "/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model, instructions, input,
                store = false, stream = true, tools = Array.Empty<object>(),
            }), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(HttpStatus(response.StatusCode), false);
            return new(await ReadEventsAsync(response.Content, cancel.Token).ConfigureAwait(false), true);
        }
        catch (AccountChangedException) { return new(status = AccountChangedStatus, false); }
        catch (OperationCanceledException) { return new("The ChatGPT request was cancelled or timed out.", false); }
        catch { return new("ChatGPT could not complete the response. Check your account or try a shorter question.", false); }
        finally { Interlocked.Exchange(ref generating, 0); }
    }

    private async Task<string> AccessAsync(string? expectedAccount, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var fileLock = await store.LockAsync(ct).ConfigureAwait(false);
            var state = store.Read();
            // A different process may change the shared selection. Never send a
            // question using an account different from the one this UI displayed.
            if (!string.Equals(state.ActiveId, expectedAccount ?? "", StringComparison.Ordinal)) throw new AccountChangedException();
            var account = state.Accounts.FirstOrDefault(value => value.Id == expectedAccount);
            if (account is null || !account.CanUsePlan || account.AccessToken.Length == 0) throw new InvalidOperationException();
            if (account.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60) return account.AccessToken;
            if (account.RefreshToken.Length == 0) throw new InvalidOperationException();
            // Keep the cross-process lock through the rotation and atomic write.
            // Fetch validation material before rotating: a later network failure
            // must not discard a replacement refresh token already received.
            await MetadataAsync(ct).ConfigureAwait(false);
            var keys = await GetAsync(JwksUrl, null, ct).ConfigureAwait(false);
            using var token = await TokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token", ["client_id"] = account.ClientId,
                ["refresh_token"] = account.RefreshToken, ["resource"] = Resource,
            }, ct).ConfigureAwait(false);
            var identityToken = ChatGptTokenValidator.Text(token.RootElement, "id_token");
            if (identityToken.Length > 0)
            {
                var identity = ChatGptTokenValidator.Validate(identityToken, keys, account.ClientId, null, DateTimeOffset.UtcNow);
                if (identity.Subject != account.Subject) throw new InvalidOperationException();
            }
            // Once the full response is received and verified, finish the local
            // replacement without another await or cancellation checkpoint.
            ApplyTokens(account, token.RootElement, requireScope: false);
            store.Save(state); Publish(state);
            if (!account.CanUsePlan) throw new InvalidOperationException();
            return account.AccessToken;
        }
        finally { gate.Release(); }
    }

    private async Task<JsonDocument> TokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(form) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The ChatGPT session could not be renewed.");
        var document = JsonDocument.Parse(await ReadBoundedAsync(response.Content, ct).ConfigureAwait(false));
        try { ChatGptTokenValidator.RejectDuplicates(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void ApplyTokens(ChatGptRegistration account, JsonElement value, bool requireScope)
    {
        var access = ChatGptTokenValidator.Text(value, "access_token");
        var refresh = ChatGptTokenValidator.Text(value, "refresh_token");
        var identity = ChatGptTokenValidator.Text(value, "id_token");
        var scope = ChatGptTokenValidator.Text(value, "scope");
        if (!ValidToken(access) || !ValidToken(refresh) || (requireScope && identity.Length == 0) ||
            !ChatGptTokenValidator.Text(value, "token_type").Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            !value.TryGetProperty("expires_in", out var expiry) || !expiry.TryGetInt32(out var seconds) || seconds is < 1 or > 86400 ||
            scope.Length > 4096 || scope.Any(char.IsControl) || identity.Length > 65536) throw new InvalidOperationException();
        if (requireScope && !value.TryGetProperty("scope", out _)) throw new InvalidOperationException();
        account.AccessToken = access; account.RefreshToken = refresh;
        if (identity.Length > 0) account.IdToken = identity;
        if (value.TryGetProperty("scope", out _)) account.Scope = scope;
        account.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds;
    }
    private async Task<Uri?> MetadataAsync(CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await GetAsync(MetadataUrl, null, ct).ConfigureAwait(false));
        var value = document.RootElement; ChatGptTokenValidator.RejectDuplicates(value);
        if (ChatGptTokenValidator.Text(value, "issuer") != ChatGptTokenValidator.Issuer ||
            ChatGptTokenValidator.Text(value, "jwks_uri") != JwksUrl ||
            ChatGptTokenValidator.Text(value, "token_endpoint") != TokenUrl ||
            ChatGptTokenValidator.Text(value, "authorization_endpoint") != AuthorizeUrl) throw new InvalidOperationException();
        var endpoint = ChatGptTokenValidator.Text(value, "revocation_endpoint");
        if (endpoint.Length == 0) return null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "auth.openai.com" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) throw new InvalidOperationException();
        return uri;
    }
    private async Task<string> GetAsync(string url, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException();
        return await ReadBoundedAsync(response.Content, ct).ConfigureAwait(false);
    }
    internal static async Task<string> ReadBoundedAsync(HttpContent content, CancellationToken ct, int limit = 262144)
    {
        if (content.Headers.ContentLength > limit) throw new InvalidOperationException();
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (count == 0) break;
            if (output.Length + count > limit) throw new InvalidOperationException();
            output.Write(buffer, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(output.ToArray());
    }
    internal static string ParseEvents(string text)
    {
        var parser = new EventAnswer();
        parser.Append(text.AsSpan());
        return parser.Finish();
    }
    internal static async Task<string> ReadEventsAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096, leaveOpen: true);
        var buffer = new char[4096];
        var parser = new EventAnswer();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (count == 0) return parser.Finish();
            parser.Append(buffer.AsSpan(0, count));
        }
    }
    // Bound individual lines/events and the retained answer, not the entire SSE
    // transcript. Thousands of small deltas need no growing transcript buffer.
    private sealed class EventAnswer
    {
        private const int EventLimit = 65536;
        private readonly StringBuilder line = new();
        private readonly StringBuilder data = new();
        private readonly StringBuilder answer = new();
        private int eventLength;
        private bool completed, shortened, skipLf, first = true;

        public void Append(ReadOnlySpan<char> characters)
        {
            foreach (var character in characters)
            {
                if (first) { first = false; if (character == '\uFEFF') continue; }
                if (skipLf && character == '\n') { skipLf = false; continue; }
                skipLf = false;
                if (character is '\r' or '\n')
                {
                    ConsumeLine(); skipLf = character == '\r';
                    continue;
                }
                if (line.Length + eventLength >= EventLimit) throw new InvalidOperationException();
                line.Append(character);
            }
        }
        private void ConsumeLine()
        {
            if (line.Length == 0) { Dispatch(); eventLength = 0; return; }
            eventLength += line.Length + 1;
            if (eventLength > EventLimit) throw new InvalidOperationException();
            var text = line.ToString(); line.Clear();
            if (!text.StartsWith("data:", StringComparison.Ordinal)) return;
            var start = text.Length > 5 && text[5] == ' ' ? 6 : 5;
            data.Append(text.AsSpan(start)); data.Append('\n');
        }
        private void Dispatch()
        {
            if (data.Length == 0) return;
            var payload = data.ToString().TrimEnd('\n'); data.Clear();
            if (payload == "[DONE]")
            {
                if (!completed) throw new InvalidOperationException();
                return;
            }
            using var item = JsonDocument.Parse(payload); ChatGptTokenValidator.RejectDuplicates(item.RootElement);
            var type = ChatGptTokenValidator.Text(item.RootElement, "type");
            if (type.Length == 0 || type is "response.failed" or "response.incomplete" or "error" || completed) throw new InvalidOperationException();
            if (type == "response.output_text.delta")
            {
                if (!item.RootElement.TryGetProperty("delta", out var value) || value.ValueKind != JsonValueKind.String) throw new InvalidOperationException();
                var delta = value.GetString()!;
                var take = Math.Min(delta.Length, 6000 - answer.Length); answer.Append(delta.AsSpan(0, take));
                shortened |= take != delta.Length;
            }
            if (type == "response.completed")
            {
                if (!item.RootElement.TryGetProperty("response", out var response) || ChatGptTokenValidator.Text(response, "status") != "completed") throw new InvalidOperationException();
                completed = true;
            }
        }
        public string Finish()
        {
            if (line.Length > 0) ConsumeLine();
            Dispatch();
            if (!completed || answer.Length == 0) throw new InvalidOperationException();
            if (answer.Length > 0 && char.IsHighSurrogate(answer[^1])) answer.Length--;
            return answer.ToString() + (shortened ? "\n[Answer shortened.]" : "");
        }
    }
    private sealed class AccountChangedException : InvalidOperationException { }
    private CancellationTokenSource RequestCancellation(CancellationToken ct)
    {
        lock (cancellationGate)
        {
            var result = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token, session.Token);
            result.CancelAfter(TimeSpan.FromSeconds(120)); return result;
        }
    }
    private void CancelSession()
    {
        lock (cancellationGate)
        {
            var previous = session; session = new CancellationTokenSource();
            previous.Cancel(); previous.Dispose();
        }
    }
    private void Publish(ChatGptAuthState state)
    {
        accounts = state.Accounts.Select(account => new ChatGptAccountInfo(account.Id, account.Label, account.Id == state.ActiveId,
            account.CanUsePlan && account.AccessToken.Length > 0 && (account.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() || account.RefreshToken.Length > 0))).ToArray();
        status = IsConnected ? "Connected to the selected ChatGPT account." : "Continue with ChatGPT to connect.";
    }
    private static bool ValidToken(string text) => text.Length is > 0 and <= 65536 && text.All(c => c is >= '!' and <= '~');
    private static bool ValidModel(string text) => text.Length is > 0 and <= 200 && text.All(c => !char.IsControl(c));
    private static string Random() => ChatGptTokenValidator.Encode(RandomNumberGenerator.GetBytes(32));
    private static string HttpStatus(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "ChatGPT needs a fresh sign-in.",
        HttpStatusCode.Forbidden => "ChatGPT plan use is not available for this account or request.",
        HttpStatusCode.TooManyRequests => "The ChatGPT usage limit was reached. Try again after it resets.",
        _ => "ChatGPT could not accept this request. No alternate billing connection was used.",
    };
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel();
        lock (cancellationGate) session.Cancel();
        client.Dispose();
        // Active operations observe cancellation and release their own gates.
    }
}
