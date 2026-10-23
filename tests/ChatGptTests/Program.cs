using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmniKit.Assistant;

var directory = Path.Combine(Path.GetTempPath(), "OmniKit-OAuth-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var checks = 0;
void Check(bool value, string description) { if (!value) throw new Exception(description); checks++; }
void Reject(Action action, string description)
{
    var rejected = false; try { action(); } catch { rejected = true; }
    Check(rejected, description);
}
async Task RejectAsync(Func<Task> action, string description)
{
    var rejected = false; try { await action(); } catch { rejected = true; }
    Check(rejected, description);
}
try
{
    using var fixture = new Fixture();
    var token = fixture.Identity("test-nonce");
    var identity = ChatGptTokenValidator.Validate(token, fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow);
    Check(identity.Subject == "synthetic-subject", "Valid signed identity must validate.");
    Reject(() => ChatGptTokenValidator.Validate(token, fixture.Keys, Fixture.ClientId, "wrong", DateTimeOffset.UtcNow), "Nonce mismatch must fail.");
    Reject(() => ChatGptTokenValidator.Validate(token, fixture.Keys, "different-client", "test-nonce", DateTimeOffset.UtcNow), "Audience mismatch must fail.");
    Reject(() => ChatGptTokenValidator.Validate(fixture.Identity("test-nonce", expires: -5), fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Expired identity must fail.");
    Reject(() => ChatGptTokenValidator.Validate(fixture.Identity("test-nonce", issuer: "https://invalid.example"), fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Wrong issuer must fail.");
    Reject(() => ChatGptTokenValidator.Validate(fixture.Identity("test-nonce", critical: true), fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Unsupported critical headers must fail.");
    Reject(() => ChatGptTokenValidator.Validate(token[..^8] + "tampered", fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Tampered signature must fail.");
    Reject(() => ChatGptTokenValidator.Validate("x.y.z", fixture.Keys, Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Malformed token must fail.");
    Reject(() => ChatGptTokenValidator.Validate(token, fixture.Keys.Replace("\"fixture-key\"", "\"other-key\""), Fixture.ClientId, "test-nonce", DateTimeOffset.UtcNow), "Unknown signing key must fail.");
    var callback = new Uri("http://127.0.0.1:1455/auth/callback");
    string Request(string query, string method = "GET", string host = "127.0.0.1:1455", string path = "/auth/callback") =>
        $"{method} {path}?{query} HTTP/1.1\r\nHost: {host}\r\n\r\n";
    const string query = "code=synthetic-code&state=expected&client_id=oaiapp_fixture";
    Check(ChatGptLoopback.Parse(Request(query), callback, "expected", null).ClientId == Fixture.ClientId, "New issued client must be accepted.");
    Check(ChatGptLoopback.Parse(Request("code=synthetic&state=expected"), callback, "expected", Fixture.ClientId).ClientId == Fixture.ClientId, "Returning callback may omit client ID.");
    foreach (var bad in new[]
    {
        Request(query + "&state=expected"), Request(query.Replace("expected", "wrong")), Request(query, "POST"),
        Request(query, host: "localhost:1455"), Request(query, path: "/callback"),
        Request(query.Replace("oaiapp_fixture", "dynamic_agent_client")), Request("state=expected&error=access_denied"),
        Request(query + "&code=second"), Request(query).Replace("Host:", "Host: 127.0.0.1:1455\r\nHost:"),
    }) Reject(() => ChatGptLoopback.Parse(bad, callback, "expected", null), "Invalid callback must fail closed.");
    Reject(() => ChatGptLoopback.Parse(Request(query), callback, "expected", "oaiapp_other"), "Reauthorization cannot replace client ID.");
    Check(ChatGptConnection.ParseEvents(Fixture.Events) == "Synthetic answer.", "Only completed streams return an answer.");
    foreach (var bad in new[]
    {
        "data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\n",
        Fixture.Events.Replace("response.completed", "response.failed"),
        Fixture.Events.Replace("response.completed", "response.incomplete"),
        Fixture.Events.Replace("\"status\":\"completed\"", "\"status\":\"failed\""),
        "data: {\"type\":\"error\",\"message\":\"untrusted provider text\"}\n\n",
    })
    {
        Reject(() => ChatGptConnection.ParseEvents(bad), "Failed or incomplete streams must not return partial success.");
        await RejectAsync(() => ChatGptConnection.ReadEventsAsync(new StringContent(bad), CancellationToken.None),
            "Incremental parsing also rejects failed or incomplete streams.");
    }
    const string completed = "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n";
    const string smallDelta = "data: {\"type\":\"response.output_text.delta\",\"item_id\":\"synthetic-item\",\"output_index\":0,\"content_index\":0,\"delta\":\"x\",\"sequence_number\":1}\n\n";
    var manyEvents = string.Concat(Enumerable.Repeat(smallDelta, 5000)) + completed;
    Check(Encoding.UTF8.GetByteCount(manyEvents) > 262144, "The fixture exceeds the former transcript byte limit.");
    Check(await ChatGptConnection.ReadEventsAsync(new StringContent(manyEvents), CancellationToken.None) == new string('x', 5000),
        "Many small events may exceed 256KiB while the answer stays bounded.");
    var longDelta = "data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = new string('x', 8000) }) + "\n\n";
    Check(await ChatGptConnection.ReadEventsAsync(new StringContent(longDelta + completed), CancellationToken.None)
        == new string('x', 6000) + "\n[Answer shortened.]", "Only retained answer text is truncated; completed status remains required.");
    await RejectAsync(() => ChatGptConnection.ReadEventsAsync(new StringContent("data: " + new string('x', 65537)), CancellationToken.None),
        "A single oversized event line is rejected before unbounded buffering.");
    await RejectAsync(() => ChatGptConnection.ReadEventsAsync(new StringContent(string.Concat(Enumerable.Repeat("data: {}\n", 9000))), CancellationToken.None),
        "Multiline events have a cumulative size bound.");
    await RejectAsync(() => ChatGptConnection.ReadEventsAsync(new ByteArrayContent([0xFF]), CancellationToken.None),
        "Malformed UTF-8 cannot enter an answer.");
    var unicodeEvents = "\uFEFFdata: {\"type\":\"response.output_text.delta\",\"delta\":\"Moon 🌙\"}\r\n\r\n" + completed.Replace("\n", "\r\n");
    using (var split = new StreamContent(new SplitReadStream(Encoding.UTF8.GetBytes(unicodeEvents))))
        Check(await ChatGptConnection.ReadEventsAsync(split, CancellationToken.None) == "Moon 🌙", "UTF-8 and CRLF boundaries may split across reads.");
    var boundRejected = false;
    try { await ChatGptConnection.ReadBoundedAsync(new StringContent(new string('x', 262145)), CancellationToken.None); }
    catch { boundRejected = true; }
    Check(boundRejected, "Response byte limit must be enforced.");

    using var connection = fixture.Connection(directory);
    Check(!connection.IsConnected && connection.Accounts.Count == 0, "Construction must not create a connection.");
    await connection.SignInAsync(CancellationToken.None);
    await fixture.Callback;
    Check(connection.IsConnected && fixture.LastAuthorizeClient == "dynamic_agent_client", "Initial public-client registration must connect.");
    var store = new ChatGptAuthStore(new DirectoryInfo(directory));
    var saved = store.Read();
    Check(saved.Accounts.Count == 1 && saved.Accounts[0].ClientId == Fixture.ClientId && ChatGptAuthStore.ValidHost(saved.HostId), "Issued client, identity and stable host must persist.");
    var host = saved.HostId; var active = saved.ActiveId;
    var encrypted = File.ReadAllBytes(Path.Combine(directory, "credentials", "chatgpt.dpapi"));
    Check(!Encoding.UTF8.GetString(encrypted).Contains("synthetic-access", StringComparison.Ordinal), "Only DPAPI ciphertext may be saved.");
    Check((await connection.ListModelsAsync(CancellationToken.None)).Single().Id == "fixture-model", "Models must come from visible account-specific catalog.");
    var catalog = ChatGptConnection.ParseModels("""
        {"models":[
          {"slug":"gpt-fixture-mini","display_name":"Small fixture","visibility":"list"},
          {"slug":"gpt-fixture-pro","display_name":"Hidden fixture","visibility":"hidden"},
          {"slug":"gpt-fixture-unknown","display_name":"Unspecified visibility"},
          {"slug":"gpt-fixture-mini","display_name":"Duplicate","visibility":"list"},
          {"slug":"gpt-fixture-nano","display_name":"","visibility":"list"}]}
        """);
    Check(catalog.Select(model => model.Id).SequenceEqual(["gpt-fixture-mini", "gpt-fixture-nano"]) &&
        catalog[1].Name == "gpt-fixture-nano", "SIWC retains visible order, deduplicates and safely falls back to the returned slug as a label.");
    fixture.ModelsJson = """{"object":"list","data":[{"id":"gpt-fixture-mini","object":"model"},{"id":"gpt-fixture-nano","object":"model"},{"id":"gpt-hidden","visibility":"hidden"}]}""";
    Check((await connection.ListModelsAsync(CancellationToken.None)).Select(model => model.Id).SequenceEqual(["gpt-fixture-mini", "gpt-fixture-nano"]),
        "The documented standard data/id catalog does not require SIWC visibility fields.");
    Check(ChatGptConnection.ParseModels("""{"models":[],"data":[{"id":"gpt-fixture-mini"}]}""").Count == 0,
        "An explicit empty SIWC catalog cannot fall through to another catalog.");
    foreach (var malformed in new[]
    {
        "[]", "{}", """{"models":{}}""", """{"data":[null]}""", """{"data":[{"id":""}]}""",
        """{"models":[{"visibility":"list","slug":42}]}""", """{"data":[{"id":" bad-id "}]}""",
        """{"data":[{"id":"gpt-fixture\n"}]}""", """{"data":[],"data":[{"id":"gpt-other"}]}""",
        """{"models":"wrong","data":[{"id":"gpt-fixture"}]}""",
    }) Reject(() => ChatGptConnection.ParseModels(malformed), "Malformed catalog must fail, not claim zero available models.");
    Reject(() => ChatGptConnection.ParseModels(JsonSerializer.Serialize(new { data = Enumerable.Range(0, 1001).Select(index => new { id = "gpt-fixture-" + index }) })),
        "Catalog entry counts remain bounded.");
    var choices = new[]
    {
        new AiModelOption("gpt-fixture-pro", "Pro"), new AiModelOption("gpt-fixture", "Standard"),
        new AiModelOption("gpt-fixture-mini", "Mini"), new AiModelOption("gpt-fixture-nano", "Nano"),
        new AiModelOption("gpt-image-fixture-nano", "Image nano"), new AiModelOption("gpt-audio-fixture-nano", "Audio nano"),
        new AiModelOption("text-embedding-fixture", "Embedding"),
    };
    Check(ChatGptConnection.PreferredModel(choices, "") == "gpt-fixture-nano", "An empty choice defaults to a returned lightweight text model.");
    Check(ChatGptConnection.PreferredModel(choices.Reverse().ToArray(), "removed-model") == "gpt-fixture-nano",
        "A missing model gets the same lightweight choice regardless of catalog order.");
    Check(ChatGptConnection.PreferredModel(choices, "gpt-fixture-pro") == "gpt-fixture-pro", "A valid explicit choice is never silently replaced.");
    Check(ChatGptConnection.PreferredModel([new("gpt-z", "Standard"), new("gpt-a", "Standard")], "") == "gpt-a",
        "Unranked text models have a deterministic id-based fallback.");
    Check(ChatGptConnection.PreferredModel([new("gpt-fixture-astra", "Astra"), new("gpt-fixture-luna", "Luna")], "") == "gpt-fixture-luna",
        "Luna wins over a heavy tier when both are returned.");
    Check(ChatGptConnection.PreferredModel([new("gpt-fixture-pro-mini", "Pro mini"), new("gpt-fixture-lite", "Lite")], "") == "gpt-fixture-lite",
        "A pro or reasoning label cannot win solely because it also contains a lightweight word.");
    Check(ChatGptConnection.PreferredModel([new("gpt-image-fixture", "Image"), new("unknown-fixture", "Unknown")], "") == "",
        "No suitable returned text-chat id means no fabricated default.");
    fixture.ModelsJson = """{"models":[]}""";
    Check((await connection.ListModelsAsync(CancellationToken.None)).Count == 0 && connection.Status.Contains("no selectable", StringComparison.Ordinal),
        "A successful empty catalog has a distinct sanitized empty status.");
    fixture.ModelsJson = """{"unexpected":"synthetic-private-provider-body"}""";
    await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "Unsupported shapes must fail distinctly.");
    Check(connection.Status.Contains("unsupported", StringComparison.Ordinal) && !connection.Status.Contains("synthetic-private", StringComparison.Ordinal),
        "Unsupported schema status must not expose provider content.");
    fixture.ModelsJson = "not-json";
    await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "Invalid JSON must fail distinctly.");
    Check(connection.Status.Contains("unreadable", StringComparison.Ordinal), "Invalid JSON is not an empty catalog.");
    fixture.ModelsJson = JsonSerializer.Serialize(new { models = new[] { new { visibility = "list", slug = "gpt-fixture-mini", display_name = "Mini", metadata = new string('x', 300000) } } });
    Check((await connection.ListModelsAsync(CancellationToken.None)).Single().Id == "gpt-fixture-mini",
        "A bounded metadata-rich catalog may exceed the smaller token-response limit.");
    fixture.ModelsJson = new string('x', 2 * 1024 * 1024 + 1);
    await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "Oversized model catalogs must fail.");
    Check(connection.Status.Contains("supported size", StringComparison.Ordinal), "Size failure has its own safe status.");
    fixture.ModelsJson = "synthetic-private-provider-body";
    foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.Found, HttpStatusCode.ServiceUnavailable })
    {
        fixture.ModelsStatus = code;
        await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "HTTP model-list failure must not look like success.");
        Check(!connection.Status.Contains("no selectable", StringComparison.Ordinal) && !connection.Status.Contains("synthetic-private", StringComparison.Ordinal),
            "HTTP diagnostics are safe and distinct from empty results.");
    }
    fixture.ModelsStatus = HttpStatusCode.OK; fixture.ModelsJson = Fixture.DefaultModels;
    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        await RejectAsync(() => connection.ListModelsAsync(canceled.Token), "Canceled model requests must not become empty lists.");
        Check(connection.Status == "Model refresh canceled.", "Cancellation has a distinct status.");
    }
    Check(fixture.Inferences == 0, "Discovery and default selection must never perform inference or select another billing source.");
    Check(await connection.AskAsync("XIV only.", "Synthetic question.", "fixture-model", CancellationToken.None) == "Synthetic answer.", "Completed Responses request must succeed.");
    Check(fixture.SafePayload, "Requests must be streaming, unstored and tool-free without max_output_tokens.");
    fixture.ResponseEvents = manyEvents;
    Check(await connection.AskAsync("XIV only.", "Synthetic question.", "fixture-model", CancellationToken.None) == new string('x', 5000),
        "The real request path consumes large transcripts incrementally.");
    fixture.ResponseEvents = Fixture.Events;
    await connection.SignInAsync(CancellationToken.None); await fixture.Callback;
    Check(fixture.LastAuthorizeClient == Fixture.ClientId && store.Read().HostId == host, "Reauthorization must reuse client and host.");
    fixture.GrantPlan = false;
    await connection.SignInAsync(CancellationToken.None); await fixture.Callback;
    Check(connection.IsConnected && store.Read().Accounts[0].CanUsePlan && store.Read().ActiveId == active,
        "Scope-denied reauthorization must retain the previous active credentials.");
    fixture.GrantPlan = true;
    fixture.BadNonce = true;
    await connection.SignInAsync(CancellationToken.None); await fixture.Callback;
    Check(connection.IsConnected && store.Read().ActiveId == active, "Failed identity verification must preserve the active account.");
    fixture.BadNonce = false;
    fixture.BadState = true; var previousExchanges = fixture.Exchanges;
    await connection.SignInAsync(CancellationToken.None); await fixture.Callback;
    Check(fixture.Exchanges == previousExchanges && connection.IsConnected, "A mismatched state must never reach code exchange.");
    fixture.BadState = false;
    saved = store.Read(); saved.Accounts[0].ExpiresAt = 1; store.Save(saved);
    using var second = fixture.Connection(directory);
    await Task.WhenAll(connection.ListModelsAsync(CancellationToken.None), second.ListModelsAsync(CancellationToken.None));
    Check(fixture.Refreshes == 1 && store.Read().Accounts[0].RefreshToken == "synthetic-refresh-rotated", "Independent connections must serialize rotating refresh and reuse the replacement.");
    saved = store.Read(); saved.Accounts[0].ExpiresAt = 1; store.Save(saved);
    fixture.RejectValidationAfterRotation = true; fixture.RotationResponseIssued = false;
    fixture.RefreshValue = "synthetic-refresh-prefetched";
    await connection.ListModelsAsync(CancellationToken.None);
    Check(store.Read().Accounts[0].RefreshToken == fixture.RefreshValue && fixture.PostRotationValidationRequests == 0,
        "Validation downloads precede rotation; their former post-token failure point cannot lose replacement credentials.");
    fixture.RejectValidationAfterRotation = false;
    saved = store.Read(); saved.Accounts[0].ExpiresAt = 1; store.Save(saved);
    using (var cancellation = new CancellationTokenSource())
    {
        fixture.RefreshValue = "synthetic-refresh-cancelled-after-body";
        fixture.RefreshBodyFinished = cancellation.Cancel;
        await RejectAsync(() => connection.ListModelsAsync(cancellation.Token), "Cancellation still prevents the model request.");
        Check(cancellation.IsCancellationRequested && store.Read().Accounts[0].RefreshToken == fixture.RefreshValue,
            "Cancellation after the full verified response cannot discard the rotated refresh token.");
        fixture.RefreshBodyFinished = null;
    }
    saved = store.Read(); saved.Accounts[0].ExpiresAt = 1; store.Save(saved);
    fixture.RedirectMetadata = true;
    var priorRefreshes = fixture.Refreshes;
    await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "Failed validation preflight blocks rotation.");
    Check(fixture.Refreshes == priorRefreshes && store.Read().Accounts[0].RefreshToken == fixture.RefreshValue,
        "A preflight failure preserves the unrotated credential.");
    fixture.RedirectMetadata = false;
    saved = store.Read(); saved.Accounts[0].ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
    var other = new ChatGptRegistration
    {
        ClientId = "oaiapp_second", Subject = "synthetic-second-subject", Label = "Second synthetic account",
        AccessToken = "synthetic-access-second", RefreshToken = "synthetic-refresh-second",
        Scope = "chatgpt.tokens.use.direct", ExpiresAt = saved.Accounts[0].ExpiresAt,
    };
    saved.Accounts.Add(other); store.Save(saved);
    using (var selector = fixture.Connection(directory))
    {
        selector.SelectAccount(other.Id);
        var beforeRequests = fixture.Requests;
        var rejectedAnswer = await connection.AskAsync("XIV only.", "Synthetic question.", "fixture-model", CancellationToken.None);
        Check(rejectedAnswer.Contains("another game instance", StringComparison.Ordinal) && fixture.Requests == beforeRequests
            && connection.Accounts.Single(account => account.IsActive).Id == active,
            "A stale instance cannot silently adopt a different persisted active account or transmit its question.");
        await RejectAsync(() => connection.ListModelsAsync(CancellationToken.None), "The account boundary also applies to model discovery.");
        Check(fixture.Requests == beforeRequests && connection.Status.Contains("another game instance", StringComparison.Ordinal),
            "External selection conflicts are reported without contacting either account.");
        var beforeBrowsers = fixture.BrowserOpens;
        var protectedBefore = File.ReadAllBytes(Path.Combine(directory, "credentials", "chatgpt.dpapi"));
        var staleSignOut = await connection.SignOutAsync(CancellationToken.None);
        Check(staleSignOut.Contains("another game instance", StringComparison.Ordinal) && fixture.Requests == beforeRequests,
            "A stale instance must not revoke the other account during sign-out.");
        var staleSignIn = await connection.SignInAsync(CancellationToken.None, addAccount: false);
        Check(staleSignIn.Contains("another game instance", StringComparison.Ordinal) && fixture.Requests == beforeRequests
            && fixture.BrowserOpens == beforeBrowsers,
            "Stale reauthorization must fail before metadata requests or browser launch.");
        Check(File.ReadAllBytes(Path.Combine(directory, "credentials", "chatgpt.dpapi")).AsSpan().SequenceEqual(protectedBefore)
            && store.Read().ActiveId == other.Id && store.Read().Accounts.Single(account => account.Id == other.Id).AccessToken == other.AccessToken,
            "Rejected management operations leave the protected credential file and selected second account unchanged.");
        Check(await selector.AskAsync("XIV only.", "Synthetic question.", "fixture-model", CancellationToken.None) == "Synthetic answer."
            && fixture.LastBearer == other.AccessToken, "The instance explicitly displaying the second account may use it.");
        connection.SelectAccount(active);
        await connection.ListModelsAsync(CancellationToken.None);
        Check(fixture.LastBearer == "synthetic-access", "Explicit reselection restores requests to the displayed original account.");
        fixture.OnBrowserOpen = () => selector.SelectAccount(other.Id);
        var changedDuringSignIn = await connection.SignInAsync(CancellationToken.None);
        await fixture.Callback;
        fixture.OnBrowserOpen = null;
        Check(changedDuringSignIn.Contains("another game instance", StringComparison.Ordinal) && store.Read().ActiveId == other.Id
            && store.Read().Accounts.Single(account => account.Id == other.Id).AccessToken == other.AccessToken,
            "A selection change during browser authorization is rechecked before committing credentials.");
        connection.SelectAccount(active);
    }
    await connection.SignOutAsync(CancellationToken.None);
    saved = store.Read();
    Check(!connection.IsConnected && saved.Accounts[0].AccessToken.Length == 0 && saved.Accounts[0].RefreshToken.Length == 0 &&
        saved.Accounts[0].IdToken.Length == 0 && saved.Accounts[0].ClientId == Fixture.ClientId && saved.HostId == host, "Sign-out clears all tokens while retaining registration and host.");
    Check(fixture.Revocations == 1 && connection.Status.Contains("revoked", StringComparison.Ordinal), "Sign-out must confirm successful remote revocation honestly.");

    using var deniedFixture = new Fixture { GrantPlan = false };
    using var denied = deniedFixture.Connection(Path.Combine(directory, "denied"));
    await denied.SignInAsync(CancellationToken.None); await deniedFixture.Callback;
    Check(!denied.IsConnected && denied.Accounts.Count == 0, "Identity without plan scope must not replace credentials or activate inference.");
    await denied.AskAsync("XIV only", "Question", "fixture-model", CancellationToken.None);
    Check(deniedFixture.Inferences == 0, "Scope-denied sign-in cannot send inference.");
    using var hostileFixture = new Fixture { BadJwksLocation = true };
    using var hostile = hostileFixture.Connection(Path.Combine(directory, "hostile"));
    await hostile.SignInAsync(CancellationToken.None);
    Check(!hostile.IsConnected && hostileFixture.BrowserOpens == 0, "Unpinned JWKS metadata must be rejected before authorization.");
    using var redirectFixture = new Fixture { RedirectMetadata = true };
    using var redirectConnection = redirectFixture.Connection(Path.Combine(directory, "redirect"));
    await redirectConnection.SignInAsync(CancellationToken.None);
    Check(redirectFixture.Requests == 1 && redirectFixture.BrowserOpens == 0, "Metadata redirects must not be followed.");
    Console.WriteLine($"PASS: {checks} offline OAuth checks; synthetic RSA, loopback callback, DPAPI and fake HTTP only. No external sign-in or credential access.");
}
finally { Directory.Delete(directory, true); }

sealed class Fixture : HttpMessageHandler
{
    internal const string ClientId = "oaiapp_fixture";
    internal const string Events = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Synthetic answer.\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n";
    private readonly RSA rsa = RSA.Create(2048);
    private string nonce = "";
    internal bool BadNonce, BadState, BadJwksLocation, RedirectMetadata;
    internal bool GrantPlan = true;
    internal bool RejectValidationAfterRotation, RotationResponseIssued;
    internal int PostRotationValidationRequests;
    internal Action? RefreshBodyFinished;
    internal Action? OnBrowserOpen;
    internal string RefreshValue = "synthetic-refresh-rotated";
    internal string ResponseEvents = Events;
    internal const string DefaultModels = """{"models":[{"visibility":"list","slug":"fixture-model","display_name":"Fixture Model"},{"visibility":"hidden","slug":"hidden-model","display_name":"Hidden"}]}""";
    internal string ModelsJson = DefaultModels;
    internal HttpStatusCode ModelsStatus = HttpStatusCode.OK;
    internal string LastBearer = "";
    internal bool SafePayload;
    internal int Exchanges, Refreshes, Revocations, Inferences, BrowserOpens, Requests;
    internal string LastAuthorizeClient = "";
    internal Task Callback = Task.CompletedTask;
    internal string Keys
    {
        get
        {
            var key = rsa.ExportParameters(false);
            return JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", kid = "fixture-key", alg = "RS256", use = "sig",
                n = ChatGptTokenValidator.Encode(key.Modulus!), e = ChatGptTokenValidator.Encode(key.Exponent!) } } });
        }
    }
    internal string Identity(string expectedNonce, int expires = 3600, string issuer = "https://auth.openai.com", bool critical = false)
    {
        var header = new JsonObject { ["alg"] = "RS256", ["kid"] = "fixture-key" };
        if (critical) header["crit"] = new JsonArray("unsupported");
        var payload = JsonSerializer.Serialize(new { iss = issuer, aud = ClientId, sub = "synthetic-subject", email = "fixture@example.invalid",
            nonce = expectedNonce, exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expires });
        var input = ChatGptTokenValidator.Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + ChatGptTokenValidator.Encode(Encoding.UTF8.GetBytes(payload));
        return input + "." + ChatGptTokenValidator.Encode(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
    internal ChatGptConnection Connection(string directory) => new(new DirectoryInfo(directory), new HttpClient(this, disposeHandler: false), (uri, ct) =>
    {
        BrowserOpens++;
        OnBrowserOpen?.Invoke();
        var query = uri.Query.TrimStart('?').Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));
        nonce = query["nonce"]; LastAuthorizeClient = query["client_id"];
        if (!query.ContainsKey("code_challenge") || query["code_challenge_method"] != "S256") throw new Exception("PKCE missing.");
        var redirect = new Uri(query["redirect_uri"]);
        var state = BadState ? "wrong" : query["state"];
        Callback = Task.Run(async () =>
        {
            using var socket = new TcpClient();
            await socket.ConnectAsync(IPAddress.Loopback, redirect.Port, ct);
            await using var stream = socket.GetStream();
            var request = $"GET /auth/callback?code=synthetic-code&state={Uri.EscapeDataString(state)}&client_id={ClientId} HTTP/1.1\r\nHost: {redirect.Authority}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), ct);
            var buffer = new byte[2048];
            var received = await stream.ReadAsync(buffer, ct);
            if (received == 0) throw new IOException("Callback response was empty.");
        }, ct);
        return Task.CompletedTask;
    });
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Requests);
        var uri = request.RequestUri!.AbsoluteUri;
        if (uri is ChatGptConnection.MetadataUrl or ChatGptConnection.JwksUrl && RejectValidationAfterRotation && RotationResponseIssued)
        {
            PostRotationValidationRequests++;
            return new(HttpStatusCode.ServiceUnavailable);
        }
        if (uri == ChatGptConnection.MetadataUrl)
        {
            if (RedirectMetadata) return new(HttpStatusCode.Found) { Headers = { Location = new Uri("https://invalid.example/jwks") } };
            return Json(new { issuer = "https://auth.openai.com", authorization_endpoint = ChatGptConnection.AuthorizeUrl,
                token_endpoint = ChatGptConnection.TokenUrl, jwks_uri = BadJwksLocation ? "https://invalid.example/jwks" : ChatGptConnection.JwksUrl,
                revocation_endpoint = "https://auth.openai.com/api/accounts/oauth/revoke" });
        }
        if (uri == ChatGptConnection.JwksUrl) return new(HttpStatusCode.OK) { Content = new StringContent(Keys) };
        if (uri == ChatGptConnection.TokenUrl)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            var form = body.Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));
            if (form["client_id"] != ClientId || form["resource"] != "https://api.openai.com/v1" || form.ContainsKey("client_secret")) throw new Exception("Wrong public-client exchange.");
            var refresh = form["grant_type"] == "refresh_token";
            if (refresh) { Interlocked.Increment(ref Refreshes); await Task.Delay(40, ct); }
            else { Exchanges++; if (!form.ContainsKey("code_verifier") || !form["redirect_uri"].StartsWith("http://127.0.0.1:", StringComparison.Ordinal)) throw new Exception("PKCE callback missing."); }
            var response = Json(new { access_token = "synthetic-access", refresh_token = refresh ? RefreshValue : "synthetic-refresh",
                id_token = Identity(BadNonce ? "wrong" : nonce), token_type = "Bearer", expires_in = 3600,
                scope = GrantPlan ? "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct" : "openid profile email" });
            if (refresh)
            {
                RotationResponseIssued = true;
                if (RefreshBodyFinished is { } finished)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                    response.Content.Dispose();
                    response.Content = new StreamContent(new EofCallbackStream(bytes, finished));
                }
            }
            return response;
        }
        if (uri.EndsWith("/oauth/revoke", StringComparison.Ordinal)) { Revocations++; return new(HttpStatusCode.OK); }
        if (request.Headers.Authorization?.Scheme != "Bearer") throw new Exception("Missing authorization.");
        LastBearer = request.Headers.Authorization.Parameter ?? "";
        if (uri == "https://api.openai.com/v1/models")
            return new(ModelsStatus) { Content = new StringContent(ModelsJson, Encoding.UTF8, "application/json") };
        if (uri == "https://api.openai.com/v1/responses")
        {
            Inferences++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var value = payload.RootElement;
            SafePayload = value.GetProperty("stream").GetBoolean() && !value.GetProperty("store").GetBoolean() &&
                value.GetProperty("tools").GetArrayLength() == 0 && !value.TryGetProperty("max_output_tokens", out _);
            return new(HttpStatusCode.OK) { Content = new StringContent(ResponseEvents, Encoding.UTF8, "text/event-stream") };
        }
        throw new Exception("Unexpected endpoint.");
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
}

sealed class EofCallbackStream(byte[] bytes, Action complete) : MemoryStream(bytes)
{
    private bool fired;
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await base.ReadAsync(buffer, cancellationToken);
        if (count == 0 && !fired) { fired = true; complete(); }
        return count;
    }
}

sealed class SplitReadStream(byte[] bytes) : MemoryStream(bytes)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => base.ReadAsync(buffer[..Math.Min(7, buffer.Length)], cancellationToken);
}
