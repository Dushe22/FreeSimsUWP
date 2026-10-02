# Next session

Branch: xbox-uwp-port. New milestone: Xbox 0.11 architecture renderer.

User confirmed 0.10.3's inclined stair handrail works normally on Xbox.
The static renderer retains the narrow upper-stub visual callbacks; no Init/Main
or wider simulation is enabled.

0.11 adds authored door/window masks, shared story elevations with adjoining
wall overlap, and pitched roofs selected by HOUS metadata. Full-wall reference
masks remove transparent sprite padding without stretching low fences/openings.
Roof footprints use wall-only room maps so water cannot create false indoor
islands. Diagonal halves and flood-fill seed tiles are covered correctly.
Existing blue-carpet, stair-hole and all-zoom handrail regressions remain.

Local suite: 13 checks, both houses, four rotations and three levels, GPU depth
readback plus roof bitmap bounds and geometry. Log: artifacts/architecture-test.log.
Xbox 0.11 is pending the user's test. Use experiments/XboxOfflineProbe/TESTING.md.
New owned assets: r4_.bmp and tar1.bmp, staged in artifacts/lot-roof-upload;
upload into LocalState/GameData/GameData/Roofs/. No game data/private keys in ZIP.
Build/audit: artifacts/offline-architecture-final. Handoff: xbox-architecture-*.zip.

Remaining rendering: exact roof ridges/eaves, terrain/water and lighting,
contained-object visual offsets. Roofs currently approximate hips/valleys over
closed wall footprints at the saved pitch; cardinal openings cover the test lots.
Keep the viewer static until this build is validated; wider simulation is separate.
