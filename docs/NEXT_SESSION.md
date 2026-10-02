# Next session

Branch: xbox-uwp-port. Milestone: Xbox 0.12.1 opening frames.

User confirmed the inclined stair handrail fix works normally on Xbox.
0.11 evidence (UWP Screenshots/0.11): 13/13 PASS, both houses with roofs at all
four angles, exit/relaunch and suspend followed by a fresh launch. User rejected
subsequent roof contour polish and requested the previous roof restored.
Roof geometry/shading/sampling stay at 04cde62ee4b6; do not redo that polish.

0.12 replaces checker grass with deterministic grain driven by saved ARRY 6.
It loads authored pool and pond pieces from the existing floors.iff, rotates
adjacency with the camera and composes concave pool corners around islands.
Ground remains flat, water static; no broader simulation or new lighting.
No extra game assets are needed beyond the 0.11 upload.

Local suite: 18 checks pass, including all 256 adjacency masks, four rotations,
three zooms, deterministic saved grass, GPU pool coverage and existing architecture/
stair regressions. Full renders: artifacts/lot-render-check (48 views).
Logs: artifacts/openings-release-tests.log; close-ups: artifacts/architecture-inspect.
Native build/audit: artifacts/offline-openings-final.
Handoff: artifacts/xbox-openings-*.zip. Use XboxOfflineProbe/TESTING.md.
The user's 0.12 PNG shows 17/17 PASS and clipped House 28 window/door trim.
0.12.1 keeps a separate authored opening mask for each wall face and applies a
bounded eight-sample SPR2 depth bias only to the camera-facing architectural
half. Other faces and unrelated sprites retain their depth. Real three-section
window/double-door GPU fixtures cover four angles/three zooms, frame coverage
and foreground occlusion. No mask enlargement or asset/save edits.
Xbox 0.12.1 validation is pending per XboxOfflineProbe/TESTING.md.

After the terrain/pool hardware gate: resolve the 11 held contained objects in
House 28 using SLOT visual offsets, then lighting. Keep the lot viewer static
until these rendering gates pass; connect controlled simulation/rendering next,
then Sims, interaction/gameplay and full save support.
Remaining terrain fidelity includes saved corner heights and water animation.
Architecture openings currently cover cardinal placements in the two test lots.

Pool ladder follow-up: user confirmed the ladder floating inside House 28 pool.
Saved deck object 201 is on pool tile (31,11), facing west. The static viewer
reconciles all three parts one tile east to the adjacent paved deck, without
moving VM entities or writing the save. Other ladders remain in place.
An additional regression covers attachment directions and both houses at every
angle/zoom. LOT VIEW logs poolAttachments=1 for House 28, 0 for House 2.
