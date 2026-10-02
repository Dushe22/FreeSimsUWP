# Controlled simulation/rendering 0.16.1.0

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
loading/focus-loss intervals. X twice reloads the same lot for another trial.
A script/render-state fault stops simulation and logs actionable context.
Camera, wall modes and paused snapshots remain available. No saves are written.

Hardware gate:
1. Run Menu and return proof.log showing 36/36. Then switch to House 28.
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
   New clock rates still need this hardware check; full Xbox FPS is not measured.

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
Next after the Xbox gate: loading/rendering Sims and their saved state.
