# Preview acceptance in FFXIV

These checks require the actual game. A compiler or isolated state test cannot
establish them. None is marked passed until observed.

- Install from the custom feed; confirm load/unload and first-run setup.
- At minimum window size and UI scale 1.0 / 1.5 / 2.0, inspect every settings
  section, expanded control, import dialog, welcome page, search and long answer.
  Block release acceptance on overlap, clipping or controls outside scroll regions.
- Test Alt+Space and the settings shortcut, rebinding, held keys, chat typing,
  another app focused, and conflicting desktop shortcuts.
- Search community/guild/free company; open game windows and loaded plugin UIs.
  Equip a saved job set and confirm an ambiguous job search asks for a choice.
- Enable soft interaction and its HUD. Face two nearby NPCs, rotate/zoom the
  camera, check stable selection, range and blocked line of sight. Toggle HUD off.
  One press must interact once, no candidate must leave the key alone, and chat,
  combat, cutscenes, occupied states and alt-tab must suppress interaction.
- Test a vendor, quest NPC, usable quest object and treasure. Game restrictions
  may still reject a visible candidate. Do not count a prompt as successful use.
- With saved Miner/Botanist sets, check one node interaction switches and waits
  before opening gathering. Finish gathering and verify restoration. Manually
  change gear/job and confirm restoration cancels. Test missing items, a hidden
  node, timeout and disabled auto-switch/restore. No harvesting should occur.
- Enable voice, activate Speak, and verify one-utterance microphone capture stops
  on completion/cancel/disable/unload. Practice a phrase, save a binding, test it,
  and verify ambiguous/unrecognized speech does not trigger an arbitrary action.
- Confirm No AI produces no provider requests. With explicit provider setup,
  submit an XIV question, cancel, test failure status and disable AI. Provider
  replies must never execute game actions.
- Export/import JSON, reject malformed files, preserve settings after rejection,
  reload the plugin, and check preferences persist. Exports must exclude secrets.
- Confirm Umbra Plugins entry and optional native widget. Verify Browsingway
  navigation only when enabled and its named overlay exists; unknown overlay
  must report failure rather than claim rendering.
