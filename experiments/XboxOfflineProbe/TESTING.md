# Wall cutaway neighborhood 0.13.3.0

Update the existing Offline Probe; keep the already uploaded game files.
Menu runs the optional suite: expect 27/27 PASS.

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
3. CUTAWAY: point at an opaque wall to cut it and walls within two tiles of its center on the selected story. The radius is independent of zoom and rotation.
   Move off it and wait: it must restore after 0.75 seconds, even with the
   pointer still. No room history is retained. Pointing at another wall must
   let walls outside the new neighborhood restore independently. Re-enter before expiry: the current
   wall stays cut. Authored openings allow picking through their holes.
4. Repeat on floor 2: the cuts must stay on floor 2. ROOF keeps the complete
   building, regardless of the wall mode; use RB to return to a room view.
5. Pan with the right stick while pointing, then rotate/zoom. The cursor
   and wall hit must stay aligned. Leave the pointer still: the lot should
   not rebuild or upload textures except for a pending cut restoration.
   Toggle the pointer off: previous cuts restore after 0.75 s. Pan normally.
6. Alternate houses, run Menu twice, Home/return and relaunch. Return proof.log
   plus screenshots of any wrong cut, cap, opening or stair occlusion.

Local evidence: 27 render checks pass, including timed restoration with a
stationary pointer, re-entry, switching walls and mode changes on both lots,
four angles and two floors. Expired walls/caps/attachments restore identical
GPU pixels without texture reallocations. Visual captures are under
artifacts/architecture-qa. Xbox controller behavior still needs hardware validation.

0.12.2 hardware proof.log has two launches, 46 view changes and 20/20 PASS,
no logged exceptions or OOM, with peak logged app usage 425.48 MiB / 1024 MiB.
This milestone preserves that texture sharing and release-before-load workflow.

Static limits: flat terrain, static water, no live simulation; 11 contained
House 28 objects await SLOT offsets. Accepted roof geometry remains unchanged.

Architecture regression checks:
- House 2, angle 2, floor 2, DOWN: lower-story wall faces/caps must not rise
  through the carpet. Low stubs belonging to floor 2 intentionally remain.
- House 2, angle 2, roof, zoom 3: balcony railings must use the same iron
  material on their inside and outside; no white/green wallpaper patches.
- Both houses, all angles, UP and DOWN: openings have shaded jambs/headers/
  sills; door thresholds are open and low top caps do not cross their holes.
- House 28, angle 2, floor 1: wide windows/double doors retain their trim and
  transparent holes. Foreground geometry must still occlude the frames.
- Alternate floors, wall modes and houses; leave the cursor stationary after
  moving off a wall. Restore delay remains 0.75 s. Return proof.log with captures.

Memory/rendering rules are documented in docs/RENDERING_CONTRACT.md.
The new colored batch is preallocated once per view and hard-limited to 4 MiB.
CPU contours are cached per lot, coalesced along straight mask boundaries and
released with it. Visibility scratch sets/buffers are reused. No new textures,
shaders, assets, simulation callbacks or accepted roof mesh changes.

0.13.3 changes only the shared cutaway radius. Neighboring solid wall centers
within two tiles cut together, with independent restoration deadlines.
House 2 upper tile (24,24) is saved with brown floor pattern 5; its authored
material is preserved at the user's request, rather than overriding a lot tile.
The 0.13.2 hardware session in the supplied 0.13/proof.log has no logged
errors/OOM and a peak sampled usage of 289.13 MiB / 1024 MiB.

Generality: geometry, masks, material mapping and cutaway belong to the shared
renderer. The viewer currently admits House 2 and 28 for validation. Existing
stair visual and pool attachment compatibility rules are keyed to object types
and saved geometry, not house IDs or fixed coordinates. Future lots still
require asset/footprint coverage checks; this is not a promise of support for
all untested object types or stories.
