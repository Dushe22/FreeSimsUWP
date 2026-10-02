# Next session

Branch: xbox-uwp-port. Milestone: Xbox 0.13.2 masked wall thickness and balcony materials.

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

Local suite: 27 checks pass, including all 256 adjacency masks, four rotations,
three zooms, deterministic saved grass, GPU pool coverage and existing architecture/
stair regressions. Dynamic architecture renders: artifacts/architecture-qa (24 views).
Logs: artifacts/architecture-tests-final.log; previous cap QA: artifacts/wall-pointer-qa.
Native build/audit: artifacts/offline-architecture-final.
Handoff: artifacts/xbox-architecture-*.zip. Use XboxOfflineProbe/TESTING.md.
0.13 adds left-stick-click pointer, right-stick pan and Y down/cutaway/up.
The selected story gets low wall stubs and brown thickness caps; persistent
railings remain. User reported that 0.13.0 retained hovered cuts indefinitely.
0.13.1 removes room history and cuts only the pointed opaque segment. Each
segment restores 0.75 s after leaving it, even with a stationary/disabled
pointer. Re-entry cancels expiry; switching targets does not retain the old wall.
UP restores immediately; DOWN remains down. Textures/geometry buffers are reused.
Next gate: Xbox 0.13.2 floor seams, door thresholds/reveals and balcony colors,
plus unchanged timed restoration. Captures: artifacts/architecture-qa.
Lower caps/faces end below the upper floor; opening contours cut caps and add
inner depth. Fences use their style instead of reverse wallpaper.
Follow docs/RENDERING_CONTRACT.md for ownership, bounds and rendering validation.
Tests: artifacts/architecture-tests-final.log; native: artifacts/offline-architecture-final.
Handoff: artifacts/xbox-architecture-*.zip; optional Menu suite is 27/27.
The user's 0.12 PNG shows 17/17 PASS and clipped House 28 window/door trim.
0.12.1 keeps a separate authored opening mask for each wall face and applies a
bounded eight-sample SPR2 depth bias only to the camera-facing architectural
half. Other faces and unrelated sprites retain their depth. Real three-section
window/double-door GPU fixtures cover four angles/three zooms, frame coverage
and foreground occlusion. No mask enlargement or asset/save edits.
Xbox 0.12.1 proof.log confirms OutOfMemoryException at 2026-10-02 06:05:42 UTC
while changing House 28 zoom, despite 18/18 PASS. User clarified they did not
observe a separate visual defect. Root causes: VM.OnBHAVChange held every
imported VM after Dispose, and the static assembled-routine cache retained
VM/content through VMRoutine.VM. Routines now belong to their VM, and session
Dispose unregisters the event and clears its cache. Shared frame pixels and
GPU textures remove per-instance duplicates. The old scene/lot is released
before allocating its replacement. Alpha ordering is cached at upload and
passes without matching alpha texels are skipped. No geometry/shader changes.
The complete test suite runs only on Menu, not startup; the initial label is
MENU: TESTS. Memory usage/budget and view costs are logged. OOM has one zoom-1
recovery attempt, and failed views no longer display stale imagery as current.
Evidence: artifacts/memory-before.log and memory-after.log (retained managed
memory after four unloaded lots: 234 MiB before, below 1 MiB after; weak VM
references now dead). artifacts/stability-stress.log: 144 view rebuilds,
288 GPU draws, no retained scene/pixel arrays after each lot; shared uploads
reduce total pixel bytes by 34-40%. This is desktop evidence, not Xbox FPS.
48 regenerated whole-lot PNGs are byte-identical to the previous render set.
Offline compatibility suite also passes. Latest 0.12.2 hardware proof confirms
20/20 PASS, two launches and 46 view changes without logged failures/OOM;
peak logged usage 425.48 MiB against a 1024 MiB budget.

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

