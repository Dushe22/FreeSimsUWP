# Shared stair, lamp and roof corrections 0.17.1.0

Update the Offline Probe and retain the existing uploaded game files.
Keep the seven-file PRIVATE artifacts/sim-probe-upload.zip upload from 0.17.
Additionally upload artifacts/shared-lot-assets-0171.zip, preserving its GameData
subfolders. It supplies the missing original GameData/Roofs/r3_.bmp separately
from APPX; do not distribute these originals. Menu expects 41/41 PASS and
requires House05, its three Characters and the referenced avatar archives.

X cycles House 2 -> 28 -> 5 -> 2. House 5 renders the saved Mortimer, Bella and
Cassandra with their original appearance and pose. These are stationary visual
snapshots; Sim behavior, movement and full saved execution remain later work.
Shared import logic applies to compatible future lots. Houses 2/28 contain no
saved people. House5 must show UNSUPPORTED 0 at both floors and roof, SIMS 3.
Its zero-graphic multipart stair stubs are nonvisual, not missing artwork.
The accepted roof geometry/shading/sampling and original floor materials remain.

Stair rails use the common stair resource callbacks and visible upper-wall state,
not model GUIDs. Test floors 1/2, all angles/zooms, Down/Cutaway/Up and timed
restoration; upper rail segments must appear with their host walls absent/cut.
Up restores the original wall-side selection, including House5's enclosed stair.

Lamps.iff now runs its original Main. Only kind/power arguments are read from
its original initializer; Init/placement are not executed. Room light, emissive
graphics and all states reuse existing GPU resources. Empty residential rooms
remain off according to the original routine. Saved Sims are still detached
visuals, so room occupancy/sleep-driven lighting requires the later executable
Sim milestone; this build does not pretend those residents are VM avatars.

This milestone connects selected object Main routines to the displayed lot.
House 2 starts 134 flower/shrub objects; House 28 starts 80 chair/table/sink/
hanging-plant/light/aquarium objects. Other saved threads remain held.
The resource compatibility gate is shared with the headless controller and
applies to future lots using the same object families. Sim behavior, interaction,
saved stack resumption, object creation/movement and full saves remain later work.
Automatic turbo was cancelled by the user and has not been implemented.

Controls:
- D-pad right/left: PAUSED / NORMAL / FAST / ULTRA (1x / 2x / 4x).
- Right-stick click: pause/resume the selected running speed.
- Initial state: paused at the imported lot's saved clock time.
- LS click pointer/camera; LS pointer/pan, RS pan; Y wall modes; A rotate;
  X switches to a fresh lot; LB zoom; RB floor/roof; View centers; Menu tests.

The live VM uses 30 ticks/sim minute at 30 base ticks/real second. Normal:
one sim minute per real second; one sim hour per real minute; a full day in
24 real minutes. Fast is 2x (12 real minutes/day); Ultra is 4x (6/day).
Pause freezes both clock and behavior. These rates implement the user's
requested timing specification; no original-executable calibration is claimed.
Raw lot imports retain their 150-tick representation. Starting a live session
rescales only its in-memory fractional minute, preserving hours/minutes;
odd saved seconds round down by one sim second to the new two-second resolution.
Normal animation cadence remains 30 ticks/s; no additional simulation work,
texture uploads or clock advancement outside the VM is introduced.

Each fresh lot trial stops at 6000 ticks (200 sim minutes):
about 200 / 100 / 50 active real seconds at Normal / Fast / Ultra, excluding
loading/focus-loss intervals. X three times reloads the same lot for another trial.
A script/render-state fault stops simulation and logs actionable context.
Camera, wall modes and paused snapshots remain available. No saves are written.

Hardware gate:
1. Run Menu and return proof.log showing 41/41. Then switch to House 28.
2. Use Normal to observe the aquarium from angle 2, floor 2, walls down;
   compare lamps before/after behaviors initialize. Other idle objects may
   execute without visibly changing. Confirm counters still show 11 SLOT children.
3. Pause/resume and change speeds. Check that one sim hour takes 60 / 30 / 15
   active real seconds at Normal / Fast / Ultra. Pause freezes behavior and clock while
   pointer cutaway restoration (~0.75 s) and camera remain responsive.
4. Change rotations, zooms and floors during a run. Home/return, disconnect/
   reconnect and Menu must not produce a clock catch-up jump.
5. Complete trials on both houses, including Ultra; switch/reload repeatedly.
   Check openings, stairs, railings, accepted roofs and pools remain intact.
   After 6000 ticks the trial must stay stopped until a fresh lot is loaded.
6. Return screenshots/video plus proof.log, including SIM/MEMORY records.
   Report responsiveness, crashes or visual defects. 0.16 hardware passed
   36/36 with no reported visual defects and working aquarium animation.
   Its scoped log peaked at 523.03 MiB of a 1024 MiB limit; one view rebuild sample was Medium, otherwise Low.
   The user has since confirmed nominal clock operation; full Xbox FPS is not measured.
7. Switch to House 5, floor 1. Check all three Sims at each angle/zoom, with walls
   Down/Cutaway/Up: walls, fences and furniture must occlude bodies correctly.
   Change clock/lighting and camera; body textures must remain complete and stable.
   Sims remain stationary even while selected object routines and clock advance.
   Cycle all three lots repeatedly and return MEMORY/LOT VIEW logs and captures.

SIM logs include active count, completed ticks, peak update/redraw milliseconds,
sprite/light revisions and texture bytes/count. Tick updates must retain
identical texture totals within a view. Preloaded frames use immutable shared
arrays, bounded sprite/pixel budgets and the existing SPR2 depth shader.
Room contributions refresh without GPU uploads. Camera/zoom changes still
rebuild at the existing release-before-upload boundary.

0.16 evidence: artifacts/live-render-tests-final.log (36 render checks),
artifacts/simulation-qa (12 images at 0/300/6000 ticks), and
artifacts/live-offline-tests-final.log. Both lots pass 6000 ticks with fixed
texture totals. Complete SPR2 rows may omit their terminal marker; the decoder
now stops at the authored height and still rejects truncated row payloads.
0.16.1 local evidence: artifacts/clock-render-tests.log and artifacts/clock-offline-tests.log.
These cover complete days at four frame rates, exact speeds and preserved import
phase, plus actual VM trials and GPU resource stability.
0.17.1 local evidence: artifacts/shared-render-final.log (41 checks),
artifacts/shared-offline-final.log and artifacts/shared-sims-qa.log, with 24
captures of House5 floors/roof, four angles and Down/Up in artifacts/sims-qa.
All three Sims use
13 mesh parts; GPU resources remain constant on relight, and disposed Sim views
and pixel/vertex arrays are released. Invalid/truncated BCF input fails explicitly.
Next after this Xbox visual/memory gate: controlled Sim animation/movement,
then interaction and full saved execution. No automatic turbo.
