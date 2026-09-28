# Static Lot Render Probe 0.9.0.0

Update the existing Offline Probe. No new game files. Expect **4/4 PASS** after
startup checks. The image is rendered from saved lot data, not a thumbnail.

- A rotates; X switches House 2/28; Y toggles walls.
- LB cycles zoom; RB selects floor level 1/2.
- Left stick pans; View recenters; Menu reruns tests; B exits.

Check both lots, all four angles, both levels and zooms. With walls enabled,
objects behind solid walls should be hidden. With walls disabled, inspect the
interiors. Pan at higher zoom, then recenter. Rerun twice, try Home/return, and
B exit/relaunch. Send proof.log plus photos with walls on and off.

Expected limitations: flat checkerboard terrain; diagnostic floor/wall colors
(including an artificial road color); flat blue pool/water surfaces; solid wall
geometry without window/door openings; no roofs, lighting or simulation. Objects
in container slots are held and counted. Empty/hidden/out-of-world objects are
not drawn; unsupported drawable records are reported in the log/UI. Current
fixtures have zero unsupported records; House 28 has 11 contained objects held.
Intersections between translucent surfaces still use sorted blending.

Camera/test counters reset on a new launch. The older simulation checkpoint is
left untouched. The package contains no game files or private key.
