# Pointer and wall modes 0.13.0.0

Update the existing Offline Probe; keep the already uploaded game files.
Menu runs the optional suite: expect 23/23 PASS.

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
3. CUTAWAY: enable the pointer and move over rooms and directly over walls.
   The game room/rectangle cut logic retains up to three recently hovered
   indoor rooms. Direct wall hover also cuts the hit wall; authored openings
   allow picking through. Move outside and select UP to restore every wall.
4. Repeat on floor 2: the cuts must stay on floor 2. ROOF keeps the complete
   building, regardless of the wall mode; use RB to return to a room view.
5. Pan with the right stick while pointing, then rotate/zoom. The cursor
   and wall hit must stay aligned. Leave the pointer still: the lot should
   not rebuild or upload textures. Toggle the pointer off and pan normally.
6. Alternate houses, run Menu twice, Home/return and relaunch. Return proof.log
   plus screenshots of any wrong cut, cap, opening or stair occlusion.

Local evidence: 23 render checks pass, including projection round trips,
story/roof boundaries, GPU restoration after mode cycles and 160 moving
hover draws without texture reallocations. Visual captures are under
artifacts/wall-pointer-qa. Xbox controller behavior still needs hardware validation.

0.12.2 hardware proof.log has two launches, 46 view changes and 20/20 PASS,
no logged exceptions or OOM, with peak logged app usage 425.48 MiB / 1024 MiB.
This milestone preserves that texture sharing and release-before-load workflow.

Static limits: flat terrain, static water, no live simulation; 11 contained
House 28 objects await SLOT offsets. Accepted roof geometry remains unchanged.
