# Face-only companion blinking

```powershell
dotnet run --project tests/CompanionBlinkTests/CompanionBlinkTests.csproj -c Release --no-restore
```

The harness uses the source-linked production libraries and actual local ImGui renderer. It validates all112 normal bodies and210 measured eyes, source hashes/crop identities, invalid optional metadata, unchanged body vertices/UVs, mirrored96/192px closures, full-turn Moonblob rolling, and the actual Ember/Mochi/Puff mesh deformation callback. Existing Wink callers remain covered by `CompanionVariantTests --blink-only`.

Ignored `.local/companion-blink-tests/` contains native PNG galleries and public-art fixture copies. `--render-only` skips fixture fault checks; `--avatars Puff,Orbit` limits the appearance galleries after a measured correction. These checks do not launch FFXIV or access profiles, credentials, or a network service; in-game visual validation remains separate.
