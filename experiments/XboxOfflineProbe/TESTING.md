# Contained SLOT objects 0.14.0.0

Update the existing Offline Probe; keep the already uploaded game files.
Menu runs the optional suite: expect 29/29 PASS. No extra assets are needed.

Controls: left-stick click toggles pointer/camera; left stick moves pointer or
pans, right stick pans, Y cycles UP/DOWN/CUTAWAY, A rotates, X switches lots,
LB zooms, RB selects floor/roof, View centers, Menu tests, B exits.

House 28 checks, all four angles and three zooms:
- Floor 1: five red tabletop slot machines on the arcade counter, registers
  and gift displays on the shop counters, and the lamp on the dressing-room
  counter. Objects must sit on their surface rather than float or sink.
- Expected: SLOTTED 11, HELD 0, UNSUPPORTED 0; DRAWN 398 on floor 1 and
  508 on floor 2/roof at zooms 1 and 3. Zoom 2 has different authored frames.
- Objects behind counters, walls or upstairs floors remain depth-occluded.
  Cutting/restoring walls must preserve contained objects and their hosts.
- House 2 remains SLOTTED 0. Its original brown stair landing tile is preserved.
- Alternate lots/floors/zooms, run Menu twice, Home/return, B exit and relaunch.
  Return proof.log and any screenshots with placement or occlusion defects.

Shared implementation: SLOT standard/custom heights, parent rotation and
nested container offsets. Rendering does not move VM objects, rewrite saves,
run placement callbacks or enable simulation. Invalid slot chains produce
controlled object diagnostics; avatar bone slots await the Sims milestone.

Local evidence: 29 render checks, including real contained children at all
angles/zooms, independent nested layout, malformed/cyclic slots, counter depth,
draw order, shared frames, resource disposal and previous rendering regressions.
Captures: artifacts/architecture-qa. Native/hardware evidence is separate.

Memory/rendering contract: docs/RENDERING_CONTRACT.md. Slot placement is computed
only during view construction, with no new long-lived VM cache. Existing frame
pixel/depth sharing and GPU texture ownership remain unchanged.

Accepted roof, two-tile cutaway radius and 0.75 s restoration remain unchanged.
Terrain is flat, water static, viewer admits two test lots and simulation is off.
Next after hardware validation: lighting, then controlled simulation/rendering.