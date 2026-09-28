# Checkpoint Probe 0.7.0.0

Update the existing Offline Probe. No new game files. Expect **9/9 PASS**
(eight checkpoint checks plus GPU thumbnails). Startup hashes content and runs
replay checks, so allow more time before the first screen.

The controlled session now checkpoints to **LocalState/UserData/Checkpoints/controlled.bin**.
It saves on pause, RB step, LB save, B exit, lot reload/switch and every 300 ticks.
The screen shows SAVED TICKS N. Restores always start paused.

1. A play, then A pause at a nonzero tick. Note house, ticks and clock; SAVED TICKS
   must match. B exit/relaunch: expect the SAME house/ticks/clock, paused.
2. RB adds one tick and saves. LB explicitly pauses/saves. Go Home and return:
   a surviving process stays paused; a fresh process replays the saved checkpoint.
   Both should retain the last confirmed saved tick rather than restart at zero.
3. While playing, go Home and return. Focus loss should save/pause if the game
   thread runs before suspension. Abrupt termination can lose work after the last
   displayed SAVED TICKS (periodic saves are every 300 ticks, about 10 seconds).
4. X switches lots and saves the new paused tick-0 session. Y deliberately reloads
   the current lot from source and replaces its checkpoint with tick 0.
5. Let House 28 reach 6,000. B exit/relaunch: restore LIMIT at 6,000; Y resets it.
6. Menu reruns the checks twice, preserving/restoring the last checkpoint.
   Also test controller disconnect/reconnect: pause/save, then explicit A to run.

Send photo + proof.log (proof.log.previous if present), and whether the restore
matched after B exit and Home/return. If a checkpoint error appears, send the log
before pressing Y; the rejected file is preserved. A failed write pauses and
keeps the old file; LB retries. B waits for a successful write before exiting.

This is a bounded deterministic replay checkpoint for the two probe profiles,
not a full TS1 save serializer. It requires the SAME engine revision and content.
Restore replays at most 6,000 ticks and checks object/thread/architecture/global/
clock/RNG state before accepting the candidate. Changed or corrupt inputs reject.
Engine updates invalidate these experimental checkpoints; Y explicitly resets.
The test-run counter remains per process. Images remain thumbnails; full gameplay,
Sims, original TS1 saved stacks, live rendering and general save editing are unsupported.
