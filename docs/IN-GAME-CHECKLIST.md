# Preview acceptance in FFXIV

These checks require the actual game. A compiler or isolated state test cannot
establish them. None is marked passed until observed.

- Install or update from the custom feed; confirm load/unload, first-run setup,
  author Zero and the compiled version/hash shown in the footer.
- At minimum window size and UI scale 1.0 / 1.5 / 2.0, inspect every settings
  section, expanded control, import dialog, welcome page, search and long answer.
  Block release acceptance on overlap, clipping or controls outside scroll regions.
  Check compact normal/warning/error status lines, full hover details and expanded
  help. The complete version must remain readable in the footer and About.
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
  compare 50%, 100% and 200% with outline on/off and all 11 text-effect choices.
  Test intensity 0/1/2, animation off/on and speed 0.1/1/3, including revealed
  controls and long effect names at minimum size. Repeat
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
- With player bags open, compare Inventory/Large/Expansion/Grid focus against
  bag search/text input, chat, shop and unknown menus. Passive bags must retain
  the key label and direct/gathering interaction; true paused states hide normal
  HUD text. Explicit HUD Modify/menu/drag remains visible, with dialogue and
  cutscene suppression still taking precedence.
- Enable Use verified quest items for Rocks in a Hard Place, sequence 2. Check
  all three cumbersome rocks with the tracked/owned Tiller's Shovel: one fresh
  press selects/confirms then attempts one item action. Debris must remain an
  ordinary interaction. Test missing/untracked shovel, wrong sequence/territory,
  ambiguous handlers, casting, out-of-use range, disabled feature and unknown
  targets. A blocked matched rule must not send a second ordinary interaction.
  Detection range must not collapse to the item's native use range.
- Switch all five companions, both outfits and six aura/shape colors. Check
  walking, ear movement, blinking and explicit mouth preview, including both facing directions.
  Draw a roaming area and confirm the complete companion and magic stay inside.
  Verify click-to-talk, resize, animation off, magic off and companion off.
- Inspect a complete walk loop and its wrap: feet alternate, the support foot
  stays planted, arms oppose the legs, and the head/body do not change size.
  Compare Bimble's settled idle with walk phases 0/0.25/0.5/0.75 in both outfits
  at the same 96/192 size and baseline. Its body and limbs must retain their size
  and attachments through start/stop; both paws must settle within 180ms. Hover
  and disable animation to check a grounded pose. Compare Moonbear's separate
  idle artwork and walking rig too; the fitted height must not shrink.
  Stop, turn, pause, resize and move a supporting frame; those movements must
  not spin the gait. Check the limbless blob's continuous roll and stable
  floating Wink/Nova bodies. Verify full silhouettes and picking bounds at
  minimum and maximum companion sizes.
  Include diagonal and nearly vertical paths, sharp turns and high-refresh
  displays. Tiny horizontal drift must not repeatedly flip facing. Compare
  frame pacing at 60/144 Hz where available; no alternating frozen/moving frames.
- Enable Frame interaction. Drag each avatar/outfit onto a native game window,
  a compatible plugin window and an Umbra bar. Test top-edge perching, walking
  off or closing the support, falling to the screen floor, landing and walking
  continuously home. Test contained walking and edge
  pushing. Check carry/fall/land/push previews. Move/resize/close the supporting
  window; the companion must follow or recover safely. Disable each frame option
  and the master switch. It must not move, resize or edit any foreign frame.
- Expand Home & personality. Show/hide the house and drag it near each screen
  edge; the roaming area must follow and the entrance must face inward. Pick up
  the companion and house with the grab hand on/off; disable animations and verify
  manual pickup still works without animated hand effects. Test local comments
  on/off and 30/120/600-second intervals with AI disabled. For Wink, test blinking
  on/off and 1/3/10-second intervals while idle and floating; animation off freezes
  the blink. Comments must not trigger AI requests, microphone capture or speech.
- In Companion settings click AI → Appearance → Voice → AI repeatedly. Open
  settings directly into each subtab too. No AI option or status may trap the
  user on its tab. Inspect every connection's revealed controls at minimum size.
- Enable voice, activate Speak, and verify one-utterance microphone capture stops
  on completion/cancel/disable/unload. Practice a phrase, save a binding, test it,
  and verify ambiguous/unrecognized speech does not trigger an arbitrary action.
- Confirm No AI produces no provider requests. With explicit provider setup,
  submit an XIV question, cancel, test failure status and disable AI. Provider
  replies must never execute game actions. Each accepted Ask must cause at most
  one inference request: rapid duplicate clicks must not queue another request,
  and cancellation, failure or a missing model must not trigger an automatic retry
  or provider/model fallback. Explicit subsequent Ask is a separate request.
- Test ChatGPT browser sign-in, cancellation, rejected consent, model listing,
  account switching, expired-token refresh and sign-out. Verify partial/failed
  streams are not shown as completed answers. CLI generation must remain paused
  with an explanatory status, while connection checks and supported sign-in
  remain available. Test existing logins, missing/incompatible versions,
  cancellation and unload. No login secret, raw CLI diagnostic or credential file may appear in
  the UI, logs, database, JSON exports or release artifacts.
- Export/import JSON, reject malformed files, preserve settings after rejection,
  reload the plugin, and check schema-6 preferences persist. Check legacy migration
  preserves chords, appearance and existing consent while selecting in-game web
  navigation. Imports must not grant new AI consent. Exports must exclude secrets.
- Open wiki and Community Finder actions in OmniKit's browser; no system-browser
  fallback should appear on failure. Check website-only mode, the controls icon,
  back/forward/reload/address/home, hide/reopen, move/resize and same-view links.
  Test the 760 × 500 surface and 360 × 240 minimum, parent resizing, mixed DPI,
  minimize/restore and game focus in windowed/borderless mode. Treat exclusive
  fullscreen as unverified. Typing in the browser must suppress plugin shortcuts.
- In browser Extensions, explicitly select a trusted unpacked fixture and test
  listing, enable/disable and removal. Verify the dedicated profile persists
  without importing another browser's cookies or logins. Unsupported schemes,
  downloads and external-window requests must not launch outside applications.
  Closing/unloading the plugin or game must close its helper process.
- Confirm optional link capture starts off for new and migrated configurations.
  Enable it and test a standard HTTP(S) ShellExecute link from the same game
  process. Recognized authentication links, non-web schemes, custom process launches and
  links opened by other processes must retain their normal behavior. Disable
  capture and verify ordinary game-process links are no longer intercepted.
- Confirm Umbra Plugins entry and optional native widget, including opening the
  launcher/settings without a Browsingway dependency.
