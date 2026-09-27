# Controlled Simulation Probe 0.6.0.0

Update the existing Offline Probe with the new x64 AppX. No new game files:
keep all previous uploads, including UserData/LotZoning.iff.

Expect **10/10 PASS**, then House 28 **PAUSED**, 80 active / 675 held, ticks 0.
The nine controller checks plus GPU thumbnails run automatically at startup.

- **RB:** pause and step exactly one tick. Confirm ticks and clock advance.
- **A:** play/pause. Leave running for 10 seconds; pause and confirm ticks stop.
- **X:** switch lots; each loads paused at tick 0. House 2: 134 active / 254 held.
- **Y:** reload the current lot from source, paused at tick 0.
- **Menu:** rerun the automated checks (twice); expect 10/10 each time.
- **B:** exit. Relaunch starts a fresh paused session; check proof.log for EXIT REQUESTED BY B.

While running, go Home and return. A surviving process must retain its tick count
and return paused without catching up; a new process starts at tick 0, paused.
Disconnect/reconnect the controller while running: it must pause and require A.
Let House 28 reach 6,000 ticks (about 3 minutes 20 seconds): LIMIT - RELOAD appears.
It must stop there; Y reloads. Please send a photo and proof.log (and
proof.log.previous if present), plus whether the manual controls passed.

The test run counter is per process. Completed results remain in proof.log.
Live tick/clock counters reflect selected object mains; the image is a static
thumbnail. Sessions are NOT saved. Sims, full lot rendering, original saved
execution stacks, arbitrary behaviors and gameplay saves remain unsupported.
The bounded 6,000-tick exercise keeps this milestone within tested coverage.
