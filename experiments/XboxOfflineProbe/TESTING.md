# Textured Lot Render Probe 0.10.3.0

Update the existing Offline Probe. Add these two files from your own game to
LocalState/GameData/GameData/ using Device Portal:
- floors.iff
- walls.iff
Local copies are staged in artifacts/lot-material-upload. Keep the previously
uploaded files, including the expansion archives. No game files are in the AppX.

Expect **8/8 PASS**. Controls are unchanged: A rotate, X house, Y walls,
LB zoom, RB floor, stick pan, View center, Menu tests, B exit.

First check House 2, level 2, angle 1: both small blue-carpet gaps must be gone;
the staircase opening must remain. Toggle Y: stair and balcony railings must
remain visible with walls hidden. Check rotations/zooms, then both houses. Look at
roads, paving, wallpaper and low fences/railings. Rerun, Home/return, exit/relaunch.
Send proof.log and original Device Portal PNGs.

Still static: flat checker terrain, flat blue water, no roofs/lighting/simulation,
no window/door cutout masks. Contained objects remain held. Materials are sampled
from authored near-view sprites onto geometry; exact original filtering/shading
is not yet reproduced. Camera/test counters reset on a fresh launch.

Handrail regression: House 2, angle 0, zoom 3, levels 1 and 2. The inclined
stair handrail must continue through the upper steps. Check all four angles.
STAIR UPPER HANDRAIL SPRITES verifies the original dynamic layers at every zoom.
