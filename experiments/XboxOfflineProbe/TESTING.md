# Textured Lot Render Probe 0.10.1.0

Update the existing Offline Probe. Add these two files from your own game to
LocalState/GameData/GameData/ using Device Portal:
- floors.iff
- walls.iff
Local copies are staged in artifacts/lot-material-upload. Keep the previously
uploaded files, including the expansion archives. No game files are in the AppX.

Expect **7/7 PASS**. Controls are unchanged: A rotate, X house, Y walls,
LB zoom, RB floor, stick pan, View center, Menu tests, B exit.

First check House 2, level 2, angle 1: both small blue-carpet gaps must be gone;
the staircase opening must remain. Then check both houses and wall toggles. Look at
roads, paving, wallpaper and low fences/railings. Rerun, Home/return, exit/relaunch.
Send proof.log and original Device Portal PNGs.

Still static: flat checker terrain, flat blue water, no roofs/lighting/simulation,
no window/door cutout masks. Contained objects remain held. Materials are sampled
from authored near-view sprites onto geometry; exact original filtering/shading
is not yet reproduced. Camera/test counters reset on a fresh launch.
