# Room lighting and day/night 0.15.0.0

Update the Offline Probe and keep the already uploaded game files.
Menu runs the optional suite: expect 31/31 PASS. No extra assets are needed.

New controls:
- D-pad left/right: change time by three hours and pause the preview.
- Right-stick click: toggle automatic cycle (four real minutes per day).
- Initial time: 12:00, paused. The viewer clock does not change the saved lot.

Existing controls: LS click pointer/camera; LS moves pointer or pans; RS pans;
Y UP/DOWN/CUTAWAY, A rotate, X lots, LB zoom, RB floor/roof, View center,
Menu tests, B exit.

Hardware checks on both lots, all four angles and three zooms:
1. At 12:00, inspect floor 1, floor 2 and roof. Window/door openings, stair
   landing materials, fences and SLOT objects retain geometry/depth.
2. Step through 15:00, 18:00, 21:00, 00:00, 03:00, 06:00, 09:00 and noon.
   Ground, water and roof change exterior tint; each room follows its saved
   daylight/electric contributions. Saved lights do not run new behavior.
3. Lit interiors stay brighter than unlit rooms at night; luminous objects
   with saved GeneratesLight flags remain visible. Frames and translucent
   edges must retain openings, without black rectangles or new occlusion.
4. Cut/restore walls at night, including a stationary pointer after leaving
   a wall (~0.75 s). Relighting must preserve caps/reveals and SLOT placement.
5. Enable automatic time for two full cycles. No accumulating tint or memory
   growth; pause with RS click and confirm the scene stops changing.
6. Alternate lots/levels/zooms, run Menu twice, Home/return, exit and relaunch.
   Return proof.log and day/night screenshots, reporting any visual defect.

Expect House 28 SLOTTED 11, HELD 0, UNSUPPORTED 0. House 2 has no contained
children and preserves its original brown stair landing tile.
LIGHT logs include hour/cycle/room count, texture count and upload bytes.
Changing time/cutaway must not allocate or upload replacement textures.
Memory usage must remain below the app budget; desktop tests do not certify
Xbox performance or memory.

Local evidence: 31 render checks, shared engine offline compatibility, and
96 dynamic captures in artifacts/lighting-qa. Native/hardware checks separate.
Generic room rules apply to every loaded footprint; the current viewer admits
two test lots. Per-room ambient/electric lighting is implemented; point-light
falloff/cast shadows and behavior-driven lamp switching remain later work.
Terrain remains flat and water static. Simulation remains off.
Next after the hardware gate: controlled simulation/rendering.