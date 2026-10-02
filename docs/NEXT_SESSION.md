# Next session

Branch: xbox-uwp-port. Milestone: Xbox 0.12 terrain and pools.

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

Local suite: 16 checks pass, including all 256 adjacency masks, four rotations,
three zooms, deterministic saved grass, GPU pool coverage and existing architecture/
stair regressions. Full renders: artifacts/lot-render-check (48 views).
Logs: artifacts/terrain-pool-test.log and artifacts/pool-close-qa.log.
Native build/audit: artifacts/offline-terrain-final.
Handoff: artifacts/xbox-terrain-*.zip. Use XboxOfflineProbe/TESTING.md.
Xbox 0.12 validation is pending the user's proof.log and Device Portal PNGs.

After the terrain/pool hardware gate: resolve the 11 held contained objects in
House 28 using SLOT visual offsets, then lighting. Keep the lot viewer static
until these rendering gates pass; connect controlled simulation/rendering next,
then Sims, interaction/gameplay and full save support.
Remaining terrain fidelity includes saved corner heights and water animation.
Architecture openings currently cover cardinal placements in the two test lots.
