# Offline Probe 0.3.0.0 - real TS1 VM objects

Update the existing FreeSims Offline Probe using the new x64 AppX.
Keep the existing Neighborhood.iff and House01/02/28.iff uploads.
Add these two files using Xbox Device Portal, under this app's LocalState:

| Source inside your PC game installation | Destination under LocalState |
| --- | --- |
| GameData/Objects/Objects.far | GameData/GameData/Objects/Objects.far |
| GameData/Global/Global.far | GameData/GameData/Global/Global.far |

PC game root: `C:\Users\Joaco\Documents\Sims UWP Xbox\GameData\TheSims`.
The repeated GameData in the destination is intentional: the first is the app's game root.
Previous uploads remain in `GameData/UserData/Neighborhood.iff` and `GameData/UserData/Houses/House01.iff`, `House02.iff`, `House28.iff`.
If reinstalling after removal, restore those uploads and the included x64 dependencies.

Expect **9/9 PASS**. **A** reruns; **X** switches house previews; **B** exits.
Test two reruns, Home/return, then exit/relaunch. Send a photo and LocalState/proof.log
(plus proof.previous.log if present).

The VM creates a real chair and a three-tile sofa, runs their initialization/main scripts,
places/rotates them, tests script-created objects, ticks 150 times and deletes/recreates them.
The house image remains a thumbnail. Full saved-lot restoration, Sims, expansion/download
objects and gameplay saves are not enabled. Source archives remain read-only; no game assets
are included in the AppX or handoff ZIP.