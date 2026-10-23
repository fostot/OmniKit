# Preview acceptance in FFXIV

These checks require the actual game. A compiler or isolated state test cannot
establish them. None is marked passed until observed.

- Install or update from the custom feed; confirm load/unload, first-run setup,
  author Zero and the compiled version/hash shown in the footer.
- At minimum window size and UI scale 1.0 / 1.5 / 2.0, inspect every settings
  section, expanded control, import dialog, welcome page, search and long answer.
  Block release acceptance on overlap, clipping or controls outside scroll regions.
- Test Alt+Space and the settings shortcut, rebinding, held keys, chat typing,
  another app focused, and conflicting desktop shortcuts.
- Search community/guild/free company; open game windows and loaded plugin UIs.
  Equip a saved job set and confirm an ambiguous job search asks for a choice.
- Enable soft interaction and its HUD. Face two nearby NPCs, rotate/zoom the
  camera, check stable selection, range and blocked line of sight. Toggle HUD off.
  Start NPC dialogue, a vendor, quest/choice menu, retainer interaction and a
  gathering session: the text and editor sample must hide. Close the interaction
  and confirm the candidate returns without a
  Preview suffix. Other menus must not resume interaction actions.
  One press must interact once, no candidate must leave the key alone, and chat,
  cutscenes, occupied states and alt-tab must suppress interaction. Combat must
  suppress world-object actions; explicitly enabled enemy actions remain usable.
- Shift-right-click the HUD and choose Modify. Font size must appear at the top;
  compare 50%, 100% and 200% with outline on/off and each text effect. Repeat
  without a candidate and with the gameplay HUD disabled. Only editing may show
  a labelled sample. Check readability, small-window scrolling and persistence.
- Test a vendor, quest NPC, usable quest object and treasure. Game restrictions
  may still reject a visible candidate. Do not count a prompt as successful use.
- Toggle each target type separately and turn them all off. Hostile and neutral
  enemies must start off. With no enemy action choice, enemies may preview but
  must not act. Compare a known aggressive enemy with a passive one and inspect
  the current-hostility classification before and during combat. Target only
  must select without starting auto-attack; Target + start auto-attack must
  select then enable it once per press. Holding the key must not repeat or turn
  an existing auto-attack off. Check range/visibility and paused input guards.
- With saved Miner/Botanist sets, check one node interaction switches and waits
  before opening gathering. Finish gathering and verify restoration. Manually
  change gear/job and confirm restoration cancels. Test missing items, a hidden
  node, timeout and disabled auto-switch/restore. No harvesting should occur.
- Switch all five companions, both outfits and six aura/shape colors. Check
  walking, ear movement, blinking and explicit mouth preview, including both facing directions.
  Draw a roaming area and confirm the complete companion and magic stay inside.
  Verify click-to-talk, resize, animation off, magic off and companion off.
- Inspect a complete walk loop and its wrap: feet alternate, the support foot
  stays planted, arms oppose the legs, and the head/body do not change size.
  Stop, turn, pause, resize and move a supporting frame; those movements must
  not spin the gait. Check the limbless blob's continuous roll and stable
  floating Wink/Nova bodies. Verify full silhouettes and picking bounds at
  minimum and maximum companion sizes.
  Include diagonal and nearly vertical paths, sharp turns and high-refresh
  displays. Tiny horizontal drift must not repeatedly flip facing. Compare
  frame pacing at 60/144 Hz where available; no alternating frozen/moving frames.
- Enable Frame interaction. Drag each avatar/outfit onto a native game window,
  a compatible plugin window and an Umbra bar. Test top-edge perching, walking
  off and falling onto lower surfaces, landing, contained walking and edge
  pushing. Check carry/fall/land/push previews. Move/resize/close the supporting
  window; the companion must follow or recover safely. Disable each frame option
  and the master switch. It must not move, resize or edit any foreign frame.
- In Companion settings click AI → Appearance → Voice → AI repeatedly. Open
  settings directly into each subtab too. No AI option or status may trap the
  user on its tab. Inspect every connection's revealed controls at minimum size.
- Enable voice, activate Speak, and verify one-utterance microphone capture stops
  on completion/cancel/disable/unload. Practice a phrase, save a binding, test it,
  and verify ambiguous/unrecognized speech does not trigger an arbitrary action.
- Confirm No AI produces no provider requests. With explicit provider setup,
  submit an XIV question, cancel, test failure status and disable AI. Provider
  replies must never execute game actions.
- Test ChatGPT browser sign-in, cancellation, rejected consent, model listing,
  account switching, expired-token refresh and sign-out. Verify partial/failed
  streams are not shown as completed answers. Test CLI connection checks and
  supported existing logins, missing/incompatible versions, cancellation and
  unload. No login secret, raw CLI diagnostic or credential file may appear in
  the UI, logs, database, JSON exports or release artifacts.
- Export/import JSON, reject malformed files, preserve settings after rejection,
  reload the plugin, and check preferences persist. Exports must exclude secrets.
- Confirm Umbra Plugins entry and optional native widget. Verify Browsingway
  navigation only when enabled and its named overlay exists; unknown overlay
  must report failure rather than claim rendering.
