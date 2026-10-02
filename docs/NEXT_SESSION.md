# Resume after quota reset

Branch: xbox-uwp-port. Latest build: Xbox 0.10.3 stair handrail fix.

The 0.10.2 Xbox screenshots confirmed persistent stairwell banisters, but exposed
missing upper pieces of the separate inclined stair handrail. Saved OBJM loading
had not reconstructed its dynamic sprite flags. The static renderer now runs the
two known upper-stub visual callbacks in its isolated session, restoring only the
exposed-side layers. No Init/Main or wider simulation is enabled.

Local renderer suite: 8/8, both houses, all rotations and levels; stair layers
checked at all three zooms. Existing floor and persistent-railing checks pass.
Xbox 0.10.3: user confirmed the stair handrail now works normally (2026-10-01). The reported handrail bug is resolved. Existing game files suffice.

Logs: artifacts/stair-fix-test.log and artifacts/stair-fix-native.log.
Package audit: artifacts/offline-stair-fix-final/AUDIT.txt.
Handoff: artifacts/xbox-stair-fix-*.zip. No game data or private keys included.

Next: door/window opening masks, thin inter-floor seams, then roofs.
Terrain/water remain diagnostic; contained objects remain held; host is static.
