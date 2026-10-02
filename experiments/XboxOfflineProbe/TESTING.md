# Opening Frame Fix 0.12.1.0

Update the existing Offline Probe. Keep the game files uploaded for 0.11;
no additional game assets are needed. Pool/water pieces come from floors.iff.
The roof geometry, shading and sampling remain the accepted 0.11 implementation.

Expect **18/18 PASS**. A rotate, X house, Y walls, LB zoom,
RB cycles floor 1 / floor 2 / ROOF, stick pan, View center, Menu tests, B exit.

First check House 28, floor 1, angle 2, zoom 3 (the reported view):
- Wide arcade windows and double glass doors: continuous frames, with no wall
  texture crossing their trim or glass. Toggle Y to compare and rotate through
  all four views, including the opposite facade and shop windows.
- Check all three zooms, then House 2 windows/doors and both upper floors.
- Send original Device Portal close-up PNGs with walls enabled, plus proof.log.

Also check both houses at all four angles and three zooms:
- Grass: no checker grid, no tile seams; identical appearance after relaunch.
- House 2 pool: continuous authored edge pieces around the perimeter and central
  island. Check the concave corners and narrow strips for black/transparent gaps.
- House 28 pool: continuous edging, aligned with the ladders/diving board.
  Both ladders must sit on the edge at every angle; neither floats inside the pool.
  The larger water area has authored shore pieces, including its small island.
- Switch walls and floors: water stays at ground level; normal floors, door/window
  openings and balcony fences remain intact. The blue carpet, stair opening,
  stairwell banisters and inclined handrail must keep working on floor 2.
- ROOF: same shape/textures as 0.11, without covering balconies or water.

Rerun tests, Home/return, exit/relaunch. Send proof.log and original Device Portal
PNGs: close pool/island views for both houses, one whole-lot view, and floor 2.
Local GPU verification passes; Xbox verification of 0.12.1 is pending.

Still static: terrain height is flat and grass is a deterministic procedural
approximation driven by saved ARRY 6, not a reproduction of TS1's grass shader.
Water uses the original near-view pieces resampled onto geometry; there is no
water animation, new lighting or wider simulation. Eleven contained objects in
House 28 remain held. Roof shading/filtering is not pixel-identical to TS1.
Camera/test counters reset on a fresh launch.

One saved House 28 ladder has its deck part on a pool tile. The static viewer
moves the complete three-part attachment one tile onto the adjacent deck for
display only. Saved positions/files stay unchanged; valid ladders are not moved.
