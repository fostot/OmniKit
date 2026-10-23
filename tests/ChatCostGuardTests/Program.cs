using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using OmniKit;
using OmniKit.Assistant;

var project = new DirectoryInfo(AppContext.BaseDirectory);
while (!File.Exists(Path.Combine(project.FullName, "src/OmniKit/Assistant/ChatService.cs")))
    project = project.Parent ?? throw new Exception("Project root not found.");
var scratch = Directory.CreateDirectory(Path.Combine(project.FullName, ".local", "chat-cost-tests-" + Guid.NewGuid().ToString("N")));
var assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}

foreach (var kind in new[] { AiConnectionKind.ApiKey, AiConnectionKind.ChatGpt })
{
    using var fixture = new ServiceFixture(scratch, kind);
    var handler = fixture.Selected;
    handler.Hold = true;
    var first = fixture.Service.AskAsync("one explicit synthetic question", default);
    await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Check(fixture.Service.IsBusy, "The service reports the active request.");
    var duplicates = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
        Task.Run(() => fixture.Service.AskAsync("duplicate synthetic question", default))));
    Check(duplicates.All(value => value.Contains("still answering", StringComparison.Ordinal)), "All concurrent submissions are rejected immediately.");
    Check(handler.Count == 1, "Only one inference POST starts under concurrent submission.");
    fixture.Config.AiConnection = kind == AiConnectionKind.ApiKey ? AiConnectionKind.ChatGpt : AiConnectionKind.ApiKey;
    Check((await fixture.Service.AskAsync("provider switch duplicate", default)).Contains("still answering", StringComparison.Ordinal), "A changed provider does not bypass the service gate.");
    handler.Release.TrySetResult();
    Check(await first == "Synthetic answer.", "The original provider completes the original question.");
    Check(!fixture.Service.IsBusy && fixture.Api.Count + fixture.ChatGpt.Count == 1, "Completion releases the gate without queued requests or fallback.");
    Check(await fixture.Service.AskAsync("a new explicit question", default) == "Synthetic answer.", "A new explicit submission can use the new selection.");
    Check(fixture.Api.Count == 1 && fixture.ChatGpt.Count == 1, "Each explicitly selected provider received exactly one POST.");

    foreach (var (captured, isChatGpt) in new[] { (fixture.Api.Requests.Single(), false), (fixture.ChatGpt.Requests.Single(), true) })
    {
        using var payload = JsonDocument.Parse(captured);
        var body = payload.RootElement;
        Check(body.GetProperty("model").GetString() == (isChatGpt ? "synthetic-chatgpt-model" : "synthetic-api-model"), "Provider-specific models stay attached to the selected request.");
        Check(body.GetProperty("tools").GetArrayLength() == 0 && !body.GetProperty("store").GetBoolean(), "No tools or stored conversation are requested.");
        Check(body.GetProperty("input").GetArrayLength() == 1, "Only the explicit question is sent as input.");
        Check(body.GetProperty("stream").GetBoolean() == isChatGpt, "Each provider uses its supported response format.");
        Check(isChatGpt ? !body.TryGetProperty("max_output_tokens", out _) : body.GetProperty("max_output_tokens").GetInt32() == 1000,
            "API output is bounded; unsupported SIWC max_output_tokens is absent.");
    }
}

foreach (var kind in new[] { AiConnectionKind.ApiKey, AiConnectionKind.ChatGpt })
{
    foreach (var failure in new[] { "401", "429", "500", "redirect", "malformed", "transport", "tool-call" })
    {
        using var fixture = new ServiceFixture(scratch, kind);
        fixture.Selected.Failure = failure;
        var response = await fixture.Service.AskAsync("synthetic failure case", default);
        Check(fixture.Selected.Count == 1 && fixture.Api.Count + fixture.ChatGpt.Count == 1, "Failure or a requested tool never causes retry, fallback or a continuation: " + failure);
        Check(!fixture.Service.IsBusy && !response.Contains("PRIVATE_FIXTURE_DETAIL", StringComparison.Ordinal), "Failure releases the gate and hides raw provider details.");
    }

    using (var fixture = new ServiceFixture(scratch, kind))
    using (var cancel = new CancellationTokenSource())
    {
        fixture.Selected.Hold = true;
        var first = fixture.Service.AskAsync("cancel synthetic question", cancel.Token);
        await fixture.Selected.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Check(fixture.Selected.Count == 1 && !fixture.Service.IsBusy, "Cancellation never retries and releases the service gate.");
        fixture.Selected.Hold = false;
        Check(await fixture.Service.AskAsync("explicit question after cancel", default) == "Synthetic answer." && fixture.Selected.Count == 2,
            "Only a new explicit submission after cancellation creates another request.");
    }
    using (var fixture = new ServiceFixture(scratch, kind, TimeSpan.FromMilliseconds(80)))
    {
        fixture.Selected.Hold = true;
        await fixture.Service.AskAsync("timeout synthetic question", default).WaitAsync(TimeSpan.FromSeconds(5));
        Check(fixture.Selected.Count == 1 && !fixture.Service.IsBusy, "Timeout never retries and releases the service gate.");
    }
    using (var fixture = new ServiceFixture(scratch, kind))
    {
        fixture.Selected.Hold = true;
        var first = fixture.Service.AskAsync("unload synthetic question", default);
        await fixture.Selected.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Service.Dispose();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Service.AskAsync("question after unload", default);
        Check(fixture.Selected.Count == 1, "Unload cancels once and prevents later inference.");
    }
}

using (var fixture = new ServiceFixture(scratch, AiConnectionKind.ChatGpt))
{
    fixture.ChatGpt.Hold = true;
    var first = fixture.Service.ChatGpt.AskAsync("Synthetic instructions", "Direct synthetic question", "synthetic-model", default);
    await fixture.ChatGpt.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    var duplicate = await fixture.Service.ChatGpt.AskAsync("Synthetic instructions", "Direct duplicate", "synthetic-model", default);
    Check(duplicate.Contains("still answering", StringComparison.Ordinal) && fixture.ChatGpt.Count == 1, "Direct ChatGPT callers cannot queue duplicate generation.");
    fixture.ChatGpt.Release.TrySetResult();
    await first;
}

using (var fixture = new ServiceFixture(scratch, AiConnectionKind.ApiKey))
{
    fixture.Config.AiEnabled = false;
    await fixture.Service.AskAsync("disabled", default);
    fixture.Config.AiEnabled = true;
    await fixture.Service.AskAsync("", default);
    await fixture.Service.AskAsync(new string('x', 4001), default);
    await fixture.Service.AskAsync("already canceled", new CancellationToken(true));
    fixture.Config.AiModel = "";
    await fixture.Service.AskAsync("missing model", default);
    fixture.Config.AiModel = "synthetic-model";
    fixture.Config.AiConnection = (AiConnectionKind)99;
    await fixture.Service.AskAsync("unsupported provider", default);
    foreach (var kind in new[] { AiConnectionKind.CodexCli, AiConnectionKind.ClaudeCli })
    {
        fixture.Config.AiConnection = kind;
        Check(await fixture.Service.AskAsync("paused CLI", default) == CliChatBackend.SingleRequestLimitNotice, "CLI generation is unavailable at the service boundary.");
    }
    Check(fixture.Api.Count + fixture.ChatGpt.Count == 0, "Disabled, invalid, canceled and paused submissions create no inference request.");
    Check(!Directory.Exists(Path.Combine(fixture.Directory.FullName, "cli-chat")), "Paused CLI submissions create no CLI working directory.");
}

Console.WriteLine($"PASS: {assertions} synthetic cost-guard assertions; one inference POST per explicit supported submission, immediate duplicate rejection, no retries/fallback/tool continuations, cancellation/timeout/unload, provider snapshots, bounded API output and SIWC-compatible payload. CLI inference remains paused. No real providers, CLI inference or real credentials used.");

sealed class ServiceFixture : IDisposable
{
    public Configuration Config { get; } = new();
    public CountingHandler Api { get; } = new(false);
    public CountingHandler ChatGpt { get; } = new(true);
    public CountingHandler Selected { get; }
    public DirectoryInfo Directory { get; }
    public ChatService Service { get; }

    public ServiceFixture(DirectoryInfo scratch, AiConnectionKind kind, TimeSpan? timeout = null)
    {
        Directory = System.IO.Directory.CreateDirectory(Path.Combine(scratch.FullName, Guid.NewGuid().ToString("N")));
        var account = new ChatGptRegistration
        {
            ClientId = "oaiapp_fixture", Subject = "synthetic-subject", Label = "Synthetic account",
            AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh", Scope = "chatgpt.tokens.use.direct",
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
        };
        new ChatGptAuthStore(Directory).Save(new ChatGptAuthState
        {
            HostId = "urn:uuid:" + Guid.NewGuid().ToString("D"), ActiveId = account.Id, Accounts = [account],
        });
        Services.PluginInterface.ConfigDirectory = Directory;
        Config.AiConnection = kind;
        Service = new ChatService(Config);
        ReplaceClient(Service, Api, timeout);
        ReplaceClient(Service.ChatGpt, ChatGpt, timeout);
        Selected = kind == AiConnectionKind.ChatGpt ? ChatGpt : Api;
    }

    // Test-only injection; every HTTP operation terminates in CountingHandler.
    // The product keeps its hardened, no-redirect HttpClient construction.
    private static void ReplaceClient(object target, HttpMessageHandler handler, TimeSpan? timeout)
    {
        var field = target.GetType().GetField("client", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((HttpClient)field.GetValue(target)!).Dispose();
        field.SetValue(target, new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(5) });
    }
    public void Dispose() => Service.Dispose();
}

sealed class CountingHandler(bool chatGpt) : HttpMessageHandler
{
    private int count;
    public int Count => Volatile.Read(ref count);
    public bool Hold { get; set; }
    public string Failure { get; set; } = "";
    public ConcurrentQueue<string> Requests { get; } = new();
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/v1/responses")
            throw new Exception("Unexpected metadata or non-inference route in synthetic handler.");
        Interlocked.Increment(ref count);
        Requests.Enqueue(await request.Content!.ReadAsStringAsync(ct));
        Entered.TrySetResult();
        if (Hold) await Release.Task.WaitAsync(ct);
        if (Failure == "transport") throw new HttpRequestException("PRIVATE_FIXTURE_DETAIL");
        var status = Failure switch
        {
            "401" => HttpStatusCode.Unauthorized, "429" => HttpStatusCode.TooManyRequests,
            "500" => HttpStatusCode.InternalServerError, "redirect" => HttpStatusCode.TemporaryRedirect,
            _ => HttpStatusCode.OK,
        };
        var content = Failure switch
        {
            "malformed" => "PRIVATE_FIXTURE_DETAIL invalid JSON",
            "tool-call" => chatGpt
                ? "data: {\"type\":\"response.output_item.added\",\"item\":{\"type\":\"function_call\",\"name\":\"must_not_run\"}}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n"
                : "{\"output\":[{\"type\":\"function_call\",\"name\":\"must_not_run\"}]}",
            _ => chatGpt
                ? "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Synthetic answer.\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n"
                : "{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"Synthetic answer.\"}]}]}",
        };
        var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, chatGpt ? "text/event-stream" : "application/json") };
        if (Failure == "redirect") response.Headers.Location = new Uri("https://example.invalid/do-not-follow");
        return response;
    }
}
