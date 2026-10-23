# Browser routing checks

Build the core, then run `dotnet run --project tests/WebRoutingTests -c Release`
from the repository root. These checks load the compiled core with local Dalamud
references and exercise URL validation and injected Community Finder routing.
They never start a browser, open a profile, connect to the network or attach to
the game. Actual WebView2 child-window and game-focus checks are separate.
