using OmniKit.Browser;

var checks = 0;
void Check(bool value, string reason) { ++checks; if (!value) throw new InvalidOperationException(reason); }
Check(ContentProtection.RuleCount >= 50, "Bundled protection rules must load.");
foreach (var url in new[] { "https://doubleclick.net/ad", "https://ads.doubleclick.net/ad", "https://A.B.DOUBLECLICK.NET./ad", "http://google-analytics.com/collect", "https://x.adsrvr.org/script.js", "https://aax.amazon-adsystem.com/" })
    Check(ContentProtection.ShouldBlock(url), "Known advertising and analytics hosts include exact suffixes and subdomains.");
foreach (var url in new string?[] { null, "", "not a url", "file:///doubleclick.net/a", "https://notdoubleclick.net/", "https://doubleclick.net.example.invalid/", "https://safe.invalid/doubleclick.net", "https://safe.invalid/?url=https://doubleclick.net/", "https://doubleclick.net@safe.invalid/", "https://accounts.google.com/", "https://github.com/", "https://openai.com/", "http://127.0.0.1/ads" })
    Check(!ContentProtection.ShouldBlock(url), "Host matching must not block lookalikes, paths, queries or unrelated services.");
Console.WriteLine($"PASS: {checks} content protection checks; local rules only, no network requests.");
