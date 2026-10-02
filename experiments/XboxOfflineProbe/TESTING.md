# Stability fix 0.12.2.0

Update the existing Offline Probe; keep the already uploaded game files.
Startup opens House 2 directly. MENU: TESTS means the optional suite has not run,
not a failure. Press Menu when wanted; expect 20/20 PASS after it finishes.

A rotate, X house, Y walls, LB zoom, RB floor/roof, stick pan, View center, B exit.

Hardware check (the failure reported was House 28 while changing zoom):
1. Switch to House 28 and cycle all three zooms several times, including zoom 3.
2. Rotate through four angles; change floors/roof and walls, then pan at zoom 3.
3. Alternate houses several times and repeat. Frames/doors, stairs, pools and
   roofs should retain their accepted appearance, with no stale or blank views.
4. Run Menu tests twice, then repeat zoom/house changes. Home/return and relaunch.
5. Return proof.log. MEMORY reports app usage and actual Xbox limit; VIEW COST
   reports build time, texture count and uploaded bytes. Memory should return
   toward the same range when revisiting the same lot, not grow after each test.

If the OS budget is still exhausted, the viewer makes one recovery attempt at
zoom 1 and reports MEMORY LIMIT. That is diagnostic recovery, not a passing
hardware result. A failed view displays an error instead of the previous image.

Local evidence: 20 render checks, the offline compatibility suite, and a stress
run of 144 view rebuilds / 288 draws pass. All 48 regenerated whole-lot PNGs
match the previous build byte-for-byte. Disposed VM/content collection is now a
regression test. Xbox confirmation is still needed; PC timing is not Xbox timing.

Static viewer limits remain: flat terrain, static water, no live simulation,
11 contained House 28 objects awaiting SLOT offsets. Roof appearance unchanged.
