# Controlled simulation/rendering 0.16.0.0

Update the Offline Probe and retain the existing uploaded game files.
Menu runs the optional suite: expect 36/36 PASS. No new assets are needed.

This milestone connects selected object Main routines to the displayed lot.
House 2 starts 134 flower/shrub objects; House 28 starts 80 chair/table/sink/
hanging-plant/light/aquarium objects. Other saved threads remain held.
The resource compatibility gate is shared with the headless controller and
applies to future lots using the same object families. Sims, interaction,
saved stack resumption, object creation/movement and full saves remain later work.
Automatic turbo was cancelled by the user and has not been implemented.

Controls:
- D-pad right/left: PAUSED / NORMAL / FAST / ULTRA (current 1x / 3x / 10x).
- Right-stick click: pause/resume the selected running speed.
- Initial state: paused at the imported lot's saved clock time.
- LS click pointer/camera; LS pointer/pan, RS pan; Y wall modes; A rotate;
  X switches to a fresh lot; LB zoom; RB floor/roof; View centers; Menu tests.

The live VM preserves its imported 150 ticks/minute at 30 base ticks/second.
This controlled compatibility trial therefore takes about five real seconds
per sim minute at Normal, rather than the separate 30-ticks/minute preview
in 0.15.1. Exact original-executable timing calibration is still pending;
do not alter the imported rate or double-advance a preview clock to hide it.

Each fresh lot trial stops at 6000 ticks (40 imported clock minutes):
about 200 / 67 / 20 active real seconds at Normal / Fast / Ultra, excluding
loading/focus-loss intervals. X twice reloads the same lot for another trial.
A script/render-state fault stops simulation and logs actionable context.
Camera, wall modes and paused snapshots remain available. No saves are written.

Hardware gate:
1. Run Menu and return proof.log showing 36/36. Then switch to House 28.
2. Use Normal to observe the aquarium from angle 2, floor 2, walls down;
   compare lamps before/after behaviors initialize. Other idle objects may
   execute without visibly changing. Confirm counters still show 11 SLOT children.
3. Pause/resume and change speeds. Pause freezes behavior and clock while
   pointer cutaway restoration (~0.75 s) and camera remain responsive.
4. Change rotations, zooms and floors during a run. Home/return, disconnect/
   reconnect and Menu must not produce a clock catch-up jump.
5. Complete trials on both houses, including Ultra; switch/reload repeatedly.
   Check openings, stairs, railings, accepted roofs and pools remain intact.
   After 6000 ticks the trial must stay stopped until a fresh lot is loaded.
6. Return screenshots/video plus proof.log, including SIM/MEMORY records.
   Report responsiveness, crashes or visual defects. Xbox FPS/memory remain
   unverified until these hardware results are reviewed.

SIM logs include active count, completed ticks, peak update/redraw milliseconds,
sprite/light revisions and texture bytes/count. Tick updates must retain
identical texture totals within a view. Preloaded frames use immutable shared
arrays, bounded sprite/pixel budgets and the existing SPR2 depth shader.
Room contributions refresh without GPU uploads. Camera/zoom changes still
rebuild at the existing release-before-upload boundary.

Local evidence: artifacts/live-render-tests-final.log (36 render checks),
artifacts/simulation-qa (12 images at 0/300/6000 ticks), and
artifacts/live-offline-tests-final.log. Both lots pass 6000 ticks with fixed
texture totals. Complete SPR2 rows may omit their terminal marker; the decoder
now stops at the authored height and still rejects truncated row payloads.
Next after the Xbox gate: loading/rendering Sims and their saved state.
