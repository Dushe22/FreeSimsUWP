# Time modes and room lighting 0.15.1.0

Update the Offline Probe and keep the already uploaded game files.
Menu runs the optional suite: expect 32/32 PASS. No extra assets are needed.

Time controls:
- D-pad right: PAUSED -> NORMAL -> FAST -> ULTRA (stops at ULTRA).
- D-pad left: lower speed, ending at PAUSED.
- Right-stick click: pause/resume the last running speed.
- Initial time: 12:00, paused. The viewer advances its own VMClock.

Normal advances one sim minute per real second; Fast currently 3x, Ultra 10x.
These are implementation rates pending original-executable calibration.
The original Deluxe manual confirms Pause/Normal/High/Ultra modes:
https://www.manuallib.com/download/2023-10-18/The%20Sims%E2%84%A2%20Deluxe%20Edition%20Manual.pdf
Behavior-based automatic acceleration for sleeping/away Sims is later work.

Existing controls: LS click pointer/camera; LS moves pointer or pans; RS pans;
Y UP/DOWN/CUTAWAY, A rotate, X lots, LB zoom, RB floor/roof, View center,
Menu tests, B exit.

Hardware checks:
1. At noon, confirm all architecture, stair handrails, openings and SLOT objects.
2. In each speed, measure ten real seconds: about 10 / 30 / 100 sim minutes,
   allowing input/measurement error. Pause freezes the clock; camera and timed
   cutaway restore (~0.75 s) remain usable. RS resumes the selected speed.
3. Change lot/floor/zoom and run Menu while Ultra is selected. Time must not
   jump by the time spent loading/testing. Home/return and disconnect/reconnect
   the controller: the paused interval is not caught up on return.
4. On Ultra, run two full days (~144 s/day). Confirm continuous midnight,
   day/night lighting, no compounded tint and no steadily growing memory.
   Night/dawn/noon/dusk checks on both lots, all four angles and three zooms.
5. Exit/relaunch starts paused at noon. Return proof.log, time measurements
   and screenshots; report any visual defect, crash or poor responsiveness.

Expect House 28 SLOTTED 11, HELD 0, UNSUPPORTED 0. House 2 preserves its
original brown stair landing tile. Saved lamp behavior remains unchanged.
LIGHT logs include hour/speed/multiplier/ticks/room count and resource totals.
Changing time/cutaway must not allocate replacement textures; relight existing
vertices and redraw only on palette/visibility changes. Peak memory remains
below budget. Desktop checks do not certify Xbox performance.

Local evidence: 32 render checks, shared-engine offline compatibility and
existing lighting GPU coverage/depth/resource tests. Native/hardware separate.
All timing/rendering rules apply generically to future lots. Terrain remains
flat, water static. Live simulation/Sims/behavior-triggered lighting remain
the next milestone; this viewer changes no saved VM entities or files.
