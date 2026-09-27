# TS1 Behavior Probe 0.5.0.0

Update the existing Offline Probe with the new x64 AppX. Keep all previous uploads.

Add one file using Xbox Device Portal:
- PC: C:\Users\Joaco\Documents\Sims UWP Xbox\FreeSimsUWP\artifacts\behavior-upload\GameData\UserData\LotZoning.iff
- Xbox: this app's **LocalState/GameData/UserData/LotZoning.iff**

The prepared file is an unchanged copy from your installed game's UserData folder.
No game files are included in the AppX or handoff ZIP.

Expect **11/11 PASS**. A reruns; X switches previews; B exits.
Test two reruns, Home/return, and B exit/relaunch. Send a photo and proof.log
(plus proof.previous.log if present). The longer behavior checks may delay startup/reruns.

Expected log:
- 93 flowers and 41 shrubs run 600 ticks, including real TS1 zoning queries.
- 14 ceiling lights and 27 wall lights run 7,200 ticks each.
- Community daytime: 14 ceiling/25 indoor wall lights turn on.
- Empty residential daytime: lights turn off. Its 2 outdoor wall lights turn on
  at night and off again during the day.
- 80 selected objects run together for 6,000 ticks; 675 other objects remain held.
- Deliberately unsupported generic-call fixtures stop their VMs without resets,
  deletions, further ticks or queued commands. EXPECTED FAULT CONTAINED is a pass.

Zoning transitions use controlled VM scenarios with the existing saved geometry.
Only selected mains restart; saved execution stacks, Sim interactions, full gameplay,
live lot rendering and saves remain unsupported. Images are still thumbnails.