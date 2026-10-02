# Next session

Branch: xbox-uwp-port. Milestone: Xbox 0.16.1 clock correction; new cadence hardware validation pending.

User confirmed the inclined stair handrail fix works normally on Xbox.
0.11 evidence (UWP Screenshots/0.11): 13/13 PASS, both houses with roofs at all
four angles, exit/relaunch and suspend followed by a fresh launch. User rejected
subsequent roof contour polish and requested the previous roof restored.
Roof geometry/shading/sampling stay at 04cde62ee4b6; do not redo that polish.

0.12 replaces checker grass with deterministic grain driven by saved ARRY 6.
It loads authored pool and pond pieces from the existing floors.iff, rotates
adjacency with the camera and composes concave pool corners around islands.
Ground remains flat, water static; no broader simulation or new lighting.
No extra game assets are needed beyond the 0.11 upload.

Local suite: 27 checks pass, including all 256 adjacency masks, four rotations,
three zooms, deterministic saved grass, GPU pool coverage and existing architecture/
stair regressions. Dynamic architecture renders: artifacts/architecture-qa (24 views).
Logs: artifacts/architecture-tests-final.log; previous cap QA: artifacts/wall-pointer-qa.
Native build/audit: artifacts/offline-architecture-final.
Handoff: artifacts/xbox-architecture-*.zip. Use XboxOfflineProbe/TESTING.md.
0.13 adds left-stick-click pointer, right-stick pan and Y down/cutaway/up.
The selected story gets low wall stubs and brown thickness caps; persistent
railings remain. User reported that 0.13.0 retained hovered cuts indefinitely.
0.13.1 removes room history and cuts only the pointed opaque segment. Each
segment restores 0.75 s after leaving it, even with a stationary/disabled
pointer. Re-entry cancels expiry; switching targets does not retain the old wall.
UP restores immediately; DOWN remains down. Textures/geometry buffers are reused.
Previous gate: Xbox 0.13.2 floor seams, door thresholds/reveals and balcony colors,
plus unchanged timed restoration. Captures: artifacts/architecture-qa.
Lower caps/faces end below the upper floor; opening contours cut caps and add
inner depth. Fences use their style instead of reverse wallpaper.
Follow docs/RENDERING_CONTRACT.md for ownership, bounds and rendering validation.
Tests: artifacts/architecture-tests-final.log; native: artifacts/offline-architecture-final.
Handoff: artifacts/xbox-architecture-*.zip; optional Menu suite is 27/27.
The user's 0.12 PNG shows 17/17 PASS and clipped House 28 window/door trim.
0.12.1 keeps a separate authored opening mask for each wall face and applies a
bounded eight-sample SPR2 depth bias only to the camera-facing architectural
half. Other faces and unrelated sprites retain their depth. Real three-section
window/double-door GPU fixtures cover four angles/three zooms, frame coverage
and foreground occlusion. No mask enlargement or asset/save edits.
Xbox 0.12.1 proof.log confirms OutOfMemoryException at 2026-10-02 06:05:42 UTC
while changing House 28 zoom, despite 18/18 PASS. User clarified they did not
observe a separate visual defect. Root causes: VM.OnBHAVChange held every
imported VM after Dispose, and the static assembled-routine cache retained
VM/content through VMRoutine.VM. Routines now belong to their VM, and session
Dispose unregisters the event and clears its cache. Shared frame pixels and
GPU textures remove per-instance duplicates. The old scene/lot is released
before allocating its replacement. Alpha ordering is cached at upload and
passes without matching alpha texels are skipped. No geometry/shader changes.
The complete test suite runs only on Menu, not startup; the initial label is
MENU: TESTS. Memory usage/budget and view costs are logged. OOM has one zoom-1
recovery attempt, and failed views no longer display stale imagery as current.
Evidence: artifacts/memory-before.log and memory-after.log (retained managed
memory after four unloaded lots: 234 MiB before, below 1 MiB after; weak VM
references now dead). artifacts/stability-stress.log: 144 view rebuilds,
288 GPU draws, no retained scene/pixel arrays after each lot; shared uploads
reduce total pixel bytes by 34-40%. This is desktop evidence, not Xbox FPS.
48 regenerated whole-lot PNGs are byte-identical to the previous render set.
Offline compatibility suite also passes. Latest 0.12.2 hardware proof confirms
20/20 PASS, two launches and 46 view changes without logged failures/OOM;
peak logged usage 425.48 MiB against a 1024 MiB budget.

Historical next step after terrain/pools: resolve the 11 held contained objects in
House 28 using SLOT visual offsets, then lighting. Keep the lot viewer static
until these rendering gates pass; connect controlled simulation/rendering next,
then Sims, interaction/gameplay and full save support.
Remaining terrain fidelity includes saved corner heights and water animation.
Architecture openings currently cover cardinal placements in the two test lots.

Pool ladder follow-up: user confirmed the ladder floating inside House 28 pool.
Saved deck object 201 is on pool tile (31,11), facing west. The static viewer
reconciles all three parts one tile east to the adjacent paved deck, without
moving VM entities or writing the save. Other ladders remain in place.
An additional regression covers attachment directions and both houses at every
angle/zoom. LOT VIEW logs poolAttachments=1 for House 28, 0 for House 2.


Current 0.14.0: all 11 saved House 28 children now render through generic SLOT
surface offsets, rotated by their parent and accumulated through nested containers.
Counters use height 4/5 = 0.8 tile; custom heights use Offset.Z/5. No house/GUID
placement patches, saved position edits, new simulation callbacks or caches.
Parent hidden/cutaway host state follows the container root. Malformed chains
are bounded and diagnosed. Expected slotted=11, held=0, unsupported=0; floor 1
at zoom 1/3 renders 398 objects, upper/roof 508. House 2 has no contained children.
Two new CPU/GPU checks bring Menu to 29/29; frame sharing and disposal still pass.
Local log: artifacts/slot-tests-final.log; captures: artifacts/architecture-qa.
Native handoff: artifacts/offline-slots-0140-final and artifacts/xbox-slots-*.zip.
Install/testing: experiments/XboxOfflineProbe/TESTING.md. Next is lighting only
after hardware placement/depth and memory validation.

0.13.3 hardware proof.log: commit 62886dbee225, 27/27 PASS, no logged failures
or OOM in its session, peak sampled app usage 339.48 MiB / 1024 MiB. Cutaway uses
a two-tile radius and independent 0.75 s restoration. Preserve original House 2
brown stair tile (24,24), as explicitly requested after saved-material diagnosis.
0.14 hardware confirmed 2026-10-02: commit 9a3112a31d29, 29/29 PASS,
17 logged view builds with no failure/OOM. Four House 28 angles show slotted=11,
held=0, unsupported=0 and correct counter placement. Peak app usage 459.48 MiB
against 1024 MiB, pressure Low. SLOT rendering gate closed.

0.15.0 lighting baseline connects saved VM room lighting to the static renderer. Geometry
triangles and sprite roots use the architecture room map; camera-facing wall
faces/caps/reveals sample the visible side. Exterior ground/water/roof uses
the existing engine palette. Saved electric/window contributions and emissive
sprite flags follow engine rules. Room snapshots retain no VM entities.
The palette is shared with simulation, uses twelve two-hour keys and wraps
continuously through midnight. No lamp behaviors or VM clock/save edits.

Original 0.15.0 controls: D-pad left/right changed time by three hours; right-stick
click toggles a four-real-minute day. Starts paused at noon. Update light colors
in existing vertex arrays at five-minute steps; redraw only on changed palette
colors/visibility. No new textures/shader changes or per-frame light decoding.
LIGHT logs include hour, cycle, room count and unchanged resource totals.
31 render checks cover room contributions, luminous sprites, SLOT root rooms,
all angles/zooms, day/night coverage/depth, midnight, exact restoration and
resource reuse; existing ownership/cutaway/opening/roof/pool gates remain.
Tests: artifacts/lighting-tests-final.log; 96 captures: artifacts/lighting-qa.
Offline compatibility: artifacts/lighting-offline-tests.log.
Native handoff: artifacts/offline-lighting-0150-final, artifacts/xbox-lighting-*.zip.
Use experiments/XboxOfflineProbe/TESTING.md. Confirm lighting and memory on Xbox
before controlled simulation/rendering, then Sims, interaction and full saves.
Lighting is per-room ambient/electric; point-light falloff/cast shadows and
behavior-driven lamp switching are outside this static rendering milestone.

0.15 hardware results reviewed 2026-10-02 (UWP Screenshots/0.15):
only the a80b41b0f756 session beginning 19:53:59 UTC belongs to this build;
the appended log also includes older versions. 31/31 PASS, four view builds,
no logged failure/OOM, peak sampled app usage 348.85 MiB / 1024 MiB, Low.
Eight House 2 captures show night/dawn/noon/dusk without a new visible
geometry defect. Texture totals stay fixed while time changes. Coverage
does not yet establish two full automatic days, House 28 night hardware or FPS.

0.15.1 replaces the four-minute preview day with shared VMTimeController:
PAUSED / NORMAL / FAST / ULTRA, D-pad left/right lowers/raises the selected
speed, RS click pauses/resumes the last running speed. Starts paused at noon.
Normal uses 30 ticks/s and 30 ticks/sim minute (one sim minute/real second);
fast/ultra use 3x/10x as current implementation values. The original Deluxe
manual confirms the four modes, but supplies no precise multipliers:
https://www.manuallib.com/download/2023-10-18/The%20Sims%E2%84%A2%20Deluxe%20Edition%20Manual.pdf
Do not claim measured original-executable timing parity until calibrated.
Automatic turbo was cancelled by the user on 2026-10-02; no implementation was made.

VMClock honors its configured/saved TicksPerMinute instead of overwriting
it at every tick; both the unconfigured fallback and legacy VMContext remain 150, matching their previous effective rate. The viewer advances
a separate VMClock, retains no entity callbacks and writes no saves. Fixed
tick pacing is shared runtime code, ready for the offline simulation caller.
Inactive/disconnected/load/test intervals are discarded on resume; slow
frames accept at most 250 ms (75 Ultra ticks), without catch-up backlog.
Lighting still updates existing vertices only at five-minute buckets and
on actual palette changes. Camera/wall restoration uses real time regardless
of pause/speed. Menu expects 32/32 including timing/frame-rate/resume tests.
Tests: artifacts/time-render-tests.log, artifacts/time-offline-tests.log.
Native handoff: artifacts/offline-time-0151-final, artifacts/xbox-time-*.zip.
Next after hardware clock/light checks: controlled live simulation/rendering,
then Sims, interaction and full saves.
Final local checks: 32/32 render checks pass; OfflineCompatibility 8/8 and live object tests pass (150 ticks, zero script errors); all 91 source game-file SHA256 hashes unchanged. Shared timing changes introduce no texture/geometry/shader edits.

0.16.0 implements roadmap step 3: controlled simulation/rendering.
The generic resource compatibility gate is shared with TS1SimulationController:
flowers/shrubs, HD chairs/tables/sinks/hanging plants, ceiling/wall lights and
large aquariums. House 2 activates 134 objects; House 28 activates 80.
Fresh Main routines only, other saved threads held; no Init/placement callbacks,
saved-stack resume, Sim import or save writes. Trials stop at 6000 ticks.
Topology/placement changes or script faults require a fresh lot; never silently
reset/delete entities or retry a failed script.

The renderer preloads bounded graphic/dynamic-sprite states, shares original
color/depth arrays and uploads them once per view. Primitive snapshots select
the visible states and refresh room contributions. No VM references in views,
per-tick view rebuilds, pixel decoding or texture uploads. Source placement,
SLOT children, cutaway, openings, roofs and pool attachment rules are preserved.
Complete SPR2 row data without an explicit end marker is accepted at Height;
incomplete row payloads still fail. FlowersOutdoor sprite 203/frame 1, graphic
3 at zoom 3 exposed this decoder assumption during precache validation.

A single VMTimeController drives the imported clock and behavior together.
Starts paused at saved time; preserve the imported 150 ticks/minute. At 30 base
ticks/s this live compatibility gate takes five real seconds/sim minute, unlike
the independent 30-ticks/minute 0.15.1 preview. Original timing calibration
remains pending. Normal/Fast/Ultra stay 1x/3x/10x; turbo request cancelled.
Inactive/loading/test intervals still discard elapsed time and catch-up debt.
SIM/MEMORY telemetry records resource totals and peak update/redraw cost.

Local render gate: 36 checks, both lots at 6000 ticks, all angles/zooms,
exact static initial pixel equivalence, frame/texture reuse and live disposal.
Twelve generated before/after captures: artifacts/simulation-qa; affected samples inspected.
Logs: artifacts/live-render-tests-final.log, artifacts/live-offline-tests-final.log.
Native handoff: artifacts/offline-live-0160-final, artifacts/xbox-live-*.zip.
Hardware instructions: experiments/XboxOfflineProbe/TESTING.md.
Next after controlled simulation's Xbox memory/performance/visual gate:
loading/rendering Sims and their saved state, then interaction and full saves.

Final offline gate: 8/8, existing behavior/controller/checkpoint tests pass, both controlled object trials reach 6000 ticks with no script errors, all 91 original game-file hashes unchanged.

0.16 hardware review (2026-10-02): only the b3c265df8567 session beginning
21:25:19 UTC in Desktop/UWP Screenshots/0.16/proof.log belongs to this build.
36/36 PASS; 31 views; no FAIL/FATAL/UNHANDLED/exception or script-stop records.
Peak app memory 523.03 MiB / 1024 MiB, one Medium sample at view rebuild; all other samples Low. View texture totals remain
constant during tick updates. User confirms no visual defects and correct
moving aquarium; two screenshots show different fish frames. Full FPS is not measured.

0.16.1 supersedes the previous live clock cadence and speed multipliers:
one sim minute/real second at Normal, a full day in 24 real minutes; Fast 2x,
Ultra 4x (12/6 minutes/day). These are the user's requested rates. No independent
original-executable calibration or auto-turbo is claimed. Auto-turbo remains cancelled.
Keep simulation at 30 base ticks/s so aquarium behavior is not accelerated at Normal.
ConfigureLiveClock is common to renderer and headless controller; raw imports
retain 150 ticks/minute. VMClock rescales fractional phase without changing hours,
minutes or tick count; odd seconds round down by one sim second at live resolution.
No source files/saves are rewritten. Pauses/loading/disconnects still discard
elapsed time; slow-frame work tops out at 30 Ultra ticks without catch-up debt.
Trial limit remains 6000 ticks: 200 sim minutes, 200/100/50 real seconds.
Timing tests cover exact multipliers, full days at multiple frame rates,
midnight/pause/resume and phase conversion; live tests cover both lots at 6000 ticks.
Logs: artifacts/clock-render-tests.log, artifacts/clock-offline-tests.log.
Native handoff: artifacts/offline-clock-0161-final, artifacts/xbox-clock-*.zip.
Next after clock hardware validation: Sims import/render and saved state,
then interaction and full saves. Preserve all accepted rendering and memory rules.

0.16.1 final local gate: 36/36 render checks, OfflineCompatibility 8/8 plus behavior/controller/checkpoint checks; both live lots complete 6000 ticks, frame/texture totals fixed, all 91 original game-file hashes unchanged. New cadence hardware validation remains pending.
