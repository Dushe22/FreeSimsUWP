# Offline Probe 0.2.1.0 - lot placement check

Update the existing **FreeSims Offline Probe** through Xbox Device Portal with the new x64 AppX.
For the 0.2.1.0 transparency retest, keep all previously uploaded game files; no new files are needed.
If it was removed, install the included x64 dependencies too.

Keep these files under this app's LocalState (the first two were used by 0.1.0.0):

| File from the PC installation | Destination under LocalState |
| --- | --- |
| UserData/Neighborhood.iff | GameData/UserData/Neighborhood.iff |
| UserData/Houses/House01.iff | GameData/UserData/Houses/House01.iff |
| UserData/Houses/House02.iff | GameData/UserData/Houses/House02.iff |
| UserData/Houses/House28.iff | GameData/UserData/Houses/House28.iff |

PC installation: C:\Users\Joaco\Documents\Sims UWP Xbox\GameData\TheSims.
Upload House02.iff and House28.iff; no Objects.far is needed. Re-upload all four only if the app's data was removed.

Expect **15/15 PASS**. **X** switches between House 2 (388 objects) and House 28 (755 objects)
for this installation. Both thumbnails should have transparent surroundings, without the old magenta rectangle.
**A** reruns; **B** exits. Test rerun, both previews, Home/return and exit/relaunch.
Send a photo and this app's LocalState/proof.log; include proof.previous.log if present.

This validates object placement parsing and thumbnail rendering, not running Sims or restoring saved execution state.
The VM clock check still uses an empty synthetic lot. Full gameplay saves remain blocked.
Original game files are read only; test writes use isolated scratch storage. No Sims assets are in this package.