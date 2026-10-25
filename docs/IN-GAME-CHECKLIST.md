# Preview acceptance in FFXIV

These checks require the actual game. A compiler or isolated state test cannot
establish them. None is marked passed until observed.

- Open each of the four navigation folders in narrow and wide layouts. Exercise
  every inner tab, tree shortcut and search jump, including Appearance → AI →
  Appearance, Soft Interact HUD Text/Effects/Position, NomNom and expanded More
  options. Hidden binding capture must cancel when changing panes. Check gutters,
  text wrapping, revealed controls and footer at 480×330 and scales 1/1.5/2.
- In Companion → Sayings, enable Comments, browse Universal and each creature,
  and search/filter contexts. Add/edit/disable/remove/restore a line; reload and
  confirm all changes persist. Check visible-companion filtering during rotation
  and a draft's captured scope. Verify matching activity bubbles, repeat avoidance,
  sleep/fitting suppression, dismissal and long Unicode text at the screen edges.
  Preview/merge a synthetic JSON export; invalid input must preserve the library.
  Settings-only JSON must not contain sayings or grant provider consent. No
  saying or idle bubble may invoke AI, voice recording or a game command.
- In Unlocks → Aetheryte, compare main crystals and physical shards with the
  game's unlocked list. Confirm Current zone follows the actual area while manual
  browsing stays selected. Test zone tabs, area search and All areas. Virtual
  exits must show no attunement box and remain outside the attuned count.
- Start a small current-zone route with vnavmesh idle. Confirm nearest-target
  selection, a verified hard target, one interaction and a tick only after the
  game sets attunement. A path ending out of range must not count. Test a supported
  walking exit and expected loading/mesh warmup; unsupported travel must stop.
- Stop during pathfinding, movement and attunement. Test Esc, manual movement,
  focus loss, combat, logout, wrong-zone travel and another navigation caller.
  Late queries must not start movement after cancellation. No unrelated navigator
  may be stopped. Verify NomNom, job changes and soft-interaction actions remain
  excluded while a route is active. Boats/paid travel must not be attempted.

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
- Confirm Character window job switching starts disabled. Disable only the
  corresponding Simple Tweaks/HaselTweaks tweak, enable OmniKit's version and
  acknowledge the conflict notice. Other tweaks must continue to work.
  Click a current unsaved class, an existing target set, a missing base class,
  a missing advanced job and a crafter/gatherer. Verify class-named snapshots,
  main-hand and exact soul crystal, no overwritten sets, and controller release
  versus held confirm. Shift-clicking crafters must retain desynthesis.
  Try missing weapon/crystal, a locked job, a broken existing set and full gearset
  slots: reject before changing gear. Test HQ main hands, dirty current sets and
  linked glamour plates. During a pending change, manual gear/selection changes,
  zoning, logout, combat or disabling the feature must stop without later retries.
  Check local success/error feedback and any manual return-set instruction.
  Change gear after saving the current class, then switch: no duplicate current
  set may be created. For a missing target, observe weapon/crystal first, then
  new set creation, Recommended Gear and reassignment of that same new row.
  Check capitalization and actual class name, reload the Gearset List and plugin,
  and verify the new set retains the recommended outfit. Existing custom-named
  sets must retain their names and contents. Blocked recommendation/finalization
  must stop once and leave the new minimal target set identified for review.
- In Automations, confirm NomNom starts off with Automatic selected. Check food
  detection and NQ/HQ counts using bags, then remove/re-add the selected food.
  Choose an eligible +EXP food and enable both switches: with no Well Fed, one
  usable request should consume the selected quality and produce the buff.
  Leave an existing food buff active and change the selection: it must not eat
  again or replace that buff. After expiry, it may consume one selected food.
  Compare Automatic's highest EXP/item-level/quality ranking with actual bags;
  test an unusable top food and verify it skips to the best usable one. Pick a
  manual food, reload and confirm it remains selected. Exhaust one kind without
  confirmed Well Fed and verify the next kind is not repeatedly consumed.
  Test each switch off, empty bags, wrong/nonfood IDs in a local
  import, combat, crafting, gathering, dialogue, cutscenes, zoning, text input,
  alt-tab and a blocked/rejected item use. No rapid retry loop or item drain is
  acceptable. Verify recovery follows the displayed instruction, and job
  switching/soft interaction cannot issue competing actions while food is pending.
- In Launcher settings test width 320/640/1200, height 24/64/120, text size 0.75/1/1.5, result count
  3/7/12 and both position limits. Check empty, typed, cleared and no-match queries
  at narrow viewports and UI scale 1/1.5/2. The input and gear must stay visible;
  keyboard/scroll navigation must still reach results beyond the row limit.
  Reducing height must remove vertical whitespace. Reset restores all six defaults and survives reload.
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
  mapped item/emote and blocked matched candidates. Ordinary targets and enemies
  must show no extra button, even unlocked. Pausing hides it. Clicking submits once;
  holding, locking, dragging and tooltips must never cause repeated/extra actions.
  Check empty/single/eight/more-than-eight item pages, button sizes 24/40/56,
  saved positions and viewport edges at 320/480px widths and UI scale 1/1.5/2.
  Headings, corner locks, panels and tooltips must not overlap or clip. Hover Ripe
  La Noscean Orange: its full name and blocked reason must fit without scrolling.
- Check the manual bar with usable KeyItems and native-tracked ordinary quest
  items. Untracked potions and ground-targeted actions must not appear. With no
  selected target, clicking must stay blocked. Change target, stack identity,
  territory or login state before a queued click; stale requests must fail.
  A routine identical snapshot refresh must not invalidate a valid click. Each
  accepted click permits one native attempt without auto-targeting or retries.
  Text entry must still block actions while clicking a bar must not block itself.
- Switch all thirteen companions: Moonblob, Wink, Nova, Mochi, Puff, Orbit, Ember,
  Prism, Nib, Echo, Fizz, Glim and Rune. Check Moonblob as the default, six aura colors, rolling/floating,
  blinking and explicit mouth preview, including both facing directions.
  Draw a roaming area and confirm the complete companion and magic stay inside.
  Verify click-to-talk, resize, animation off, magic off and companion off.
- Preview all nine seasonal body themes on the eight companions, with accessories
  on/off independently. Inspect all 72 looks and fitted hats at 96/192 and both
  directions, including Ember's broad crown rather than its flame tip. Check Auto against
  supported native festival state, no festival, unknown IDs and conflicting
  events; it must not infer an event from the PC month. Official date fallback
  applies only when native data is unavailable, and manual selection remains
  available. Test seasonal off and optional companion rotation at 30/300/3600
  seconds, including pause/carry/nap states. Rotation must retain the saved
  manual companion choice and make no model request.
- Pick each of the four everyday looks on every companion, then Random and its
  look timer. Keep identity through pickup and naps; check theme changes defer
  until the companion is free. Inspect connected Ember flicker, Mochi squash and
  Puff billow, plus Orbit's layered rotating/tilting rings with movement off/on
  and speed 0.2/1/3. Disabling animation must freeze them.
- Inspect complete rolling/floating cycles and their wrap at 96/192 sizes.
  Compare Ocean Opal with Lavender Geode, Tide Glass with Twilight Velvet,
  and Puff's cream with bubble looks: soft bodies deform while rigid ones rock.
  Test Body motion at 0/1/1.5 and Movement reactions off/on. Start, stop and turn
  in both directions; reactions must settle without reversing or snapping.
  Check each creature's connected body motion, especially Nib/Echo/Fizz/Glim/Rune
  in tall painted hats. The hat must not cancel the deformation. Eyes and fitted
  bows follow the same warp; animation off freezes the movement. Stop/start and
  direction changes must settle smoothly without a detached body or limbs.
  Orbit's whole body, hat and both ring passes must turn and sway together,
  retaining their separate ring pitch/precession and front/back depth order.
  Check eye-only blinks on all eight original companions, all everyday looks and
  all holiday bodies while resting and moving. Orbit's rings must follow the body
  theme independently of the accessory. Verify Magic off, animation off and the
  still fitting preview. The normal body texture and shape
  must remain stable. Verify blink off, animation off, Fourier Wink and fitting
  freeze, plus rolling/mirrored/deformed eyes without displaced lids.
  Bodies must retain their identity, source aspect and stable scale; no separate
  body pieces should jiggle or detach. Compare idle with movement and reactions.
  Stop, turn, pause, resize and move a supporting frame; those movements must
  not introduce sudden rotation. Check Moonblob's continuous roll and the other
  seven stable floating bodies. Verify full silhouettes and picking bounds at
  minimum and maximum companion sizes.
- Put a reachable ledge below an unreachable higher destination and watch for
  hop/fall loops in both directions. Three failed excursions must stop further
  retries in an unchanged scene, including wide ledges and jittering waypoints.
  Moving the ledge, resizing, changing the destination or manual pickup can
  release it. Actual contact with a different higher support counts as progress;
  passing through the same point in midair does not. A safe alternate route must
  remain usable.
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
  With flat-surface walking on, verify no free-space ascent. Nearby reachable
  ledges permit bounded hops; open edges permit falls. Toggle hopping off and
  verify unreachable routes stop instead of teleporting.
- Open Companion → Appearance → Home. Show/hide the house and drag it near each screen
  edge and outside the drawn area; the roaming area must remain unchanged and
  the entrance must face inward. Normal free roaming must remain in the drawn
  area and avoid the house footprint. Pick up
  the companion and house with the grab hand on/off and at hand size 8/38/192; disable animations and verify
  manual pickup still works without animated hand effects. Test local comments
  on/off and 30/120/600-second intervals with AI disabled. For Wink, test blinking
  on/off and 1/3/10-second intervals while idle and floating; animation off freezes
  the blink. Compare Fourier-smoothed blink off/on without changing Wink's body
  shape or movement. Pick up near different object edges and confirm the object
  eases under the centered hand; hand scaling must not shift it. Comments must
  not trigger AI requests, microphone capture or speech.
- Open Fit accessories. The real companion, nap phase, physics and animation
  clocks must freeze; the editor must show one still body in the center with
  optional eye-only blinks. At
  minimum size and scales 1/1.5/2, open all three selectors and test accessory
  drag, horizontal/vertical corner resize, Size and Angle. The body must not rescale
  during dragging. Save multiple fits, change selectors, close/reopen, resize
  the game and mirror/roll the companion; placement must follow its head rather
  than screen coordinates. Cancel preserves saved fits; Reset is draft-only
  until Save. Fit view changes only preview zoom. Check extreme saved scales
  and offsets, angles at -180/0/180 degrees, failed-save handling and JSON export/import.
  Verify existing fits migrate without movement. Only the five whole bows are
  fitted; no separate hats or rear pieces may appear. With blinking enabled the
  eyes blink while the body and saved fit remain still. Disable blink/animation
  to freeze the eye preview too.
- Select all four Painted costumes for every companion at 96/192 and both
  facings. Inspect complete front brims, Santa cuffs and pom-poms in walking,
  carry and nap poses. Tall hats must not shrink the body. Entry/sleep/waking
  use the same character's bare artwork; the painted hat returns outside only.
  Check None, Auto and named themes, event changes, and a manual costume change
  while sleeping. Original everyday/holiday choices stay saved. Test all thirteen
  in the avatar menu, including their portraits and rotation.
- Pick an everyday look while a holiday body is active: it must leave holiday
  mode and show that look. Enable a named Body theme and verify the everyday
  selection remains saved. Change body/companion while sleeping: the appearance
  must update immediately without moving or restarting the nap. Automatic
  rotation still waits. Inspect all 72 distinct holiday transformations.
- Enable automatic companion rotation and Companion poof. The outgoing body
  must remain until the opaque peak; the new body appears once and fades into
  view without teleporting or resetting gait. Test poof off, magic off, animation
  off, manual appearance changes, pickup/nap deferral and interrupted transitions.
  Select Default, Crystal Pod, Cloud Cushion, Lantern Hut and each holiday home;
  disable Use holiday homes when testing a manual style. Check Random home/hand
  choice, all five hand designs, continuous grip contact and a retained hand from
  pickup through release. Clamp actual house artwork flush to each screen edge.
- Test naps for all thirteen companions: about 45 seconds awake and 20 asleep by
  default, plus awake interval 15/300 and sleep duration 5/120.
  Check all thirteen homes at both facings: the companion first shrinks fully
  in front of the doorstep, turns away, then climbs through the actual opening.
  Check all everyday/holiday rear looks keep their original color and markings;
  the four rear poses must share one support point and show one body at a time.
  No wall may
  cut across the full-size foreground body. Inspect Default's curved arch,
  cancellation during foreground shrink, hats off/on, and the existing exit path.
  A nap trip can leave the
  drawn area at about twice normal speed, enter through the front doorway rather
  than the roof/side, curl up, breathe,
  wake and return. Stay small while leaving the doorway and crossing the front
  path; step outward before growing in front of the house, with no roof or
  wall intersection. Check sleep symbols on/off and all screen edges. Pause during
  approach and sleep; position must freeze. Disable naps, hide or move the house
  during approach/entry/sleep; the companion must exit and return without a
  teleport. Approved walking/rolling/floating cadence must remain intact.
- In Appearance → Scene, change companion/home/hand sizes separately. Each
  control must leave the other two sizes unchanged. Scene scale 0.5/1/2 changes
  them together; reset controls keep the other size group intact. Test a small
  home with a large companion, both screen edges, resizing during a nap, and a
  small roaming box. The pet must shrink to the actual opening and exit safely.
  Check all five Appearance sections and their conditional controls at 480×330.
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
