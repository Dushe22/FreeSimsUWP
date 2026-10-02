# Architecture Render Probe 0.11.0.0

Update the existing Offline Probe. Keep previously uploaded game files.
Add these two BMPs from your own game to LocalState/GameData/GameData/Roofs/
using Device Portal:
- r4_.bmp (House 2)
- tar1.bmp (House 28)
Local copies are staged in artifacts/lot-roof-upload. No game data is in the AppX.
Missing roof files produce a clear log error; existing floor views remain usable.

Expect **13/13 PASS**. A rotate, X house, Y walls, LB zoom,
RB cycles floor 1 / floor 2 / ROOF, stick pan, View center, Menu tests, B exit.

Check both houses at all angles and zooms:
- Doors/windows: openings must reveal the room or outdoors behind the frame.
  Check House 2's arched windows and House 28's large shop windows.
- Between floors: no orange/white gaps through adjoining walls; check House 2
  angle 1, level 2, zoom 3. Texture changes/baseboards can remain visible.
- ROOF: pitched textured surfaces have straight ridges and half-tile eaves, avoiding balconies,
  open courtyards, pools and the island in House 2's pool. No triangular holes.
- Stair regression: the real staircase opening stays open; the repaired blue
  carpet stays complete. Stairwell banisters and inclined handrails still render.

Rerun tests, Home/return, exit/relaunch. Send proof.log and original Device Portal
PNGs of floor 2 and ROOF for both houses. Xbox validation of 0.11 is pending.

Still static: flat checker terrain/blue water, no lighting or simulation;
contained objects remain held. Roof shape uses the existing half-tile rectangular hip model at the saved pitch.
Roof tiles use the existing renderer's face-relative texture scale. Pixel-identical
TS1 shading/filtering is not yet reproduced.
Authored near-view materials/masks are sampled onto geometry. Architectural
opening mapping currently covers cardinal placements in these two test lots.
Camera/test counters reset on a fresh launch.
