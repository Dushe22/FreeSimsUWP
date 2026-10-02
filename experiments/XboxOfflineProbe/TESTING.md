# Expiring pointer wall cuts 0.13.1.0

Update the existing Offline Probe; keep the already uploaded game files.
Menu runs the optional suite: expect 24/24 PASS.

Controls:
- Click the left stick to toggle POINTER / CAMERA.
- Left stick moves the pointer in POINTER; pans in CAMERA.
- Right stick pans the camera in either mode.
- Y cycles walls UP -> DOWN -> CUTAWAY -> UP. Current mode appears below the lot.
- A rotates; X switches houses; LB zooms; RB selects floor 1 / floor 2 / roof.
- View centers; Menu tests; B exits. Resume/disconnect consumes held button edges.

Check both lots, four angles and three zooms:
1. UP: solid walls have narrow brown top caps and exposed ends. Existing
   openings, roofs and stair railings should remain intact.
2. DOWN: the selected story becomes low textured wall stubs. Lower stories
   and persistent fences/banisters remain full. Hosted doors/windows and
   wall objects marked HideForCutaway disappear with their host walls.
3. CUTAWAY: enable the pointer and point at an opaque wall segment to cut it.
   Move off it and wait: it must restore after 0.75 seconds, even with the
   pointer still. No room history is retained. Pointing at another wall must
   let the first restore independently. Re-enter before expiry: the current
   wall stays cut. Authored openings allow picking through their holes.
4. Repeat on floor 2: the cuts must stay on floor 2. ROOF keeps the complete
   building, regardless of the wall mode; use RB to return to a room view.
5. Pan with the right stick while pointing, then rotate/zoom. The cursor
   and wall hit must stay aligned. Leave the pointer still: the lot should
   not rebuild or upload textures except for a pending cut restoration.
   Toggle the pointer off: previous cuts restore after 0.75 s. Pan normally.
6. Alternate houses, run Menu twice, Home/return and relaunch. Return proof.log
   plus screenshots of any wrong cut, cap, opening or stair occlusion.

Local evidence: 24 render checks pass, including timed restoration with a
stationary pointer, re-entry, switching walls and mode changes on both lots,
four angles and two floors. Expired walls/caps/attachments restore identical
GPU pixels without texture reallocations. Visual captures are under
artifacts/wall-pointer-qa. Xbox controller behavior still needs hardware validation.

0.12.2 hardware proof.log has two launches, 46 view changes and 20/20 PASS,
no logged exceptions or OOM, with peak logged app usage 425.48 MiB / 1024 MiB.
This milestone preserves that texture sharing and release-before-load workflow.

Static limits: flat terrain, static water, no live simulation; 11 contained
House 28 objects await SLOT offsets. Accepted roof geometry remains unchanged.
