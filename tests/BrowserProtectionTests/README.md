# Local protection rules

```powershell
dotnet run --project tests/BrowserProtectionTests/BrowserProtectionTests.csproj -c Release
```

Twenty checks link the production hostname matcher and embed the real rule file. They cover exact hosts, nested subdomains, case, trailing DNS dots, lookalike suffixes, unrelated account services, URL paths/queries, user-info parsing and non-web schemes. No browser or network is used. Native interception and popup behavior are covered by `BrowserBehaviorSmoke`.
