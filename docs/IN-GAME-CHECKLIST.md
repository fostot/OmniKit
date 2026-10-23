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
- In Launcher settings test width 320/640/1200, text size 0.75/1/1.5, result count
  3/7/12 and both position limits. Check empty, typed, cleared and no-match queries
  at narrow viewports and UI scale 1/1.5/2. The input and gear must stay visible;
  keyboard/scroll navigation must still reach results beyond the row limit.
  Reset must restore the five defaults and survive reload.
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
  Test outline thickness 0.5/1/4 and verify it follows the moving primary glyphs,
  including chromatic glitch, hologram and light sweep, without clipping.
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
- For Forbidden Island, sequence 1, test Fetid Dodo Flesh on each of the three
  suspicious mounds. One fresh press must confirm the target before one item
  request. Test missing/untracked items, a same-named item from another quest,
  wrong sequence/territory, combat, blocked usability and disabled feature.
  A blocked mapped attempt must not talk or retry. The game owns the resulting
  encounter; OmniKit must not fight creatures or advance later objectives.
- Enable Use verified quest emotes for A Bridge Too Full, sequence 2. Check both
  irate coachmen in the verified territory: a fresh press must select/confirm the
  target before one `/soothe` request. Test wrong sequence/territory, paused input,
  blocked action, disabled feature and unknown targets. Holding the key must not
  repeat; a blocked matched rule must not open dialogue instead. See
  [mapping and limits](QUEST-EMOTES.md).
- In Quest Items, inspect locked Next Action as one icon and unlocked mode with
  its heading/drag area. Compare the preview to the actual Interact resolver for
  ordinary, mapped item/emote and blocked candidates. Clicking submits once;
  holding, locking, dragging and tooltips must never cause repeated/extra actions.
  Check empty/single/eight/more-than-eight item pages, button sizes 24/40/56,
  saved positions and viewport edges at 320/480px widths and UI scale 1/1.5/2.
  Headings, corner locks, panels and tooltips must not overlap or clip.
- Check the manual bar with usable KeyItems and native-tracked ordinary quest
  items. Untracked potions and ground-targeted actions must not appear. With no
  selected target, clicking must stay blocked. Change target, stack identity,
  territory or login state before a queued click; stale requests must fail.
  A routine identical snapshot refresh must not invalidate a valid click. Each
  accepted click permits one native attempt without auto-targeting or retries.
  Text entry must still block actions while clicking a bar must not block itself.
- Switch all eight companions: Moonblob, Wink, Nova, Mochi, Puff, Orbit, Ember and
  Prism. Check Moonblob as the default, six aura colors, rolling/floating,
  blinking and explicit mouth preview, including both facing directions.
  Draw a roaming area and confirm the complete companion and magic stay inside.
  Verify click-to-talk, resize, animation off, magic off and companion off.
- Preview all nine seasonal themes on the eight companions. Check Auto against
  supported native festival state, no festival, unknown IDs and conflicting
  events; it must not infer an event from the PC month. Official date fallback
  applies only when native data is unavailable, and manual selection remains
  available. Test seasonal off and optional companion rotation at 30/300/3600
  seconds, including pause/carry/nap states. Rotation must retain the saved
  manual companion choice and make no model request.
- Inspect complete rolling/floating cycles and their wrap at 96/192 sizes.
  Bodies must retain their identity, source aspect and stable scale; no separate
  body pieces should jiggle or detach. Compare idle with movement and reactions.
  Stop, turn, pause, resize and move a supporting frame; those movements must
  not introduce sudden rotation. Check Moonblob's continuous roll and the other
  seven stable floating bodies. Verify full silhouettes and picking bounds at
  minimum and maximum companion sizes.
  Include diagonal and nearly vertical paths, sharp turns and high-refresh
  displays. Tiny horizontal drift must not repeatedly flip facing. Compare
  frame pacing at 60/144 Hz where available; no alternating frozen/moving frames.
- Enable Frame interaction. Drag each avatar onto a native game window,
  a compatible plugin window and an Umbra bar. Test top-edge perching, walking
  off or closing the support, landing on supported frames below or the screen
  floor, then returning continuously to the roaming area. Include compatible
  windows that ignore mouse input; do not assume every full-screen canvas is a
  supported surface. Test contained walking and edge
  pushing. Check carry/fall/land/push previews. Move/resize/close the supporting
  window; the companion must follow or recover safely. Disable each frame option
  and the master switch. It must not move, resize or edit any foreign frame.
  Include a thin HUD strip: walking past its edge must fall, not remain attached
  to an invisible full-screen canvas.
- Expand Home & personality. Show/hide the house and drag it near each screen
  edge and outside the drawn area; the roaming area must remain unchanged and
  the entrance must face inward. Normal free roaming must remain in the drawn
  area and avoid the house footprint. Pick up
  the companion and house with the grab hand on/off and at scale 0.2/0.4/1; disable animations and verify
  manual pickup still works without animated hand effects. Test local comments
  on/off and 30/120/600-second intervals with AI disabled. For Wink, test blinking
  on/off and 1/3/10-second intervals while idle and floating; animation off freezes
  the blink. Compare Fourier-smoothed blink off/on without changing Wink's body
  shape or movement. Pick up near different object edges and confirm the object
  eases under the centered hand; hand scaling must not shift it. Comments must
  not trigger AI requests, microphone capture or speech.
- Test naps for all eight companions: about 45 seconds awake and 20 asleep by default,
  plus awake interval 15/300 and sleep duration 5/120. A nap trip can leave the
  drawn area at about twice normal speed, enter through the front doorway rather
  than the roof/side, curl up, breathe,
  wake and return. Check sleep symbols on/off and all screen edges. Pause during
  approach and sleep; position must freeze. Disable naps, hide or move the house
  during approach/entry/sleep; the companion must exit and return without a
  teleport. Approved walking/rolling/floating cadence must remain intact.
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
  reload the plugin, and check all 19 new schema-8 fields persist. Check v1–v7
  atomic migration preserves chords, appearance and existing consent while
  selecting in-game web navigation. Imports must not grant new AI consent.
  Exports must exclude secrets.
  Invalid new field ranges or a failed database write must leave the prior
  snapshot intact. New house/hand styles, grounded hops and rings are not source-13
  acceptance items; they remain pending.
- Open wiki and Community Finder actions in OmniKit's browser; no system-browser
  fallback should appear on failure. Check website-only mode, the controls icon,
  back/forward/reload/address/home, the always-visible close X, hide/reopen,
  move/resize and same-view links. Close/hide must stop the helper and engine;
  leaving it closed must not restart them in the background.
  Test the 760 × 500 surface and 360 × 240 minimum, parent resizing, mixed DPI,
  minimize/restore and game focus in windowed/borderless mode. Treat exclusive
  fullscreen as unverified. Typing in the browser must suppress plugin shortcuts.
  Hover native game UI overlapping the browser and test the cutout mitigation;
  do not mark the reported flicker fixed until observed in FFXIV.
- Browser protection must start on in a new session. Test a script popup and
  requests to known blocked ad/tracking hosts, then the session protection switch
  for compatibility. The embedded 60-host list does not promise to block all ads.
- In browser Extensions, explicitly select a trusted unpacked fixture and test
  listing, enable/disable and removal. Verify the dedicated profile persists
  without importing another browser's cookies or logins. Unsupported schemes,
  downloads and external-window requests must not launch outside applications.
  Closing/unloading the plugin or game must close its helper process.
- Confirm optional link capture starts off for new and migrated configurations.
  Enable it while the browser is closed and confirm no prewarm. Test a standard
  HTTP(S) ShellExecute link from the same game
  process. Recognized authentication links, non-web schemes, custom process launches and
  links opened by other processes must retain their normal behavior. Disable
  capture and verify ordinary game-process links are no longer intercepted.
  A captured ordinary web link must start the closed browser on demand.
- Confirm Umbra Plugins entry and optional native widget, including opening the
  launcher/settings without a Browsingway dependency.
