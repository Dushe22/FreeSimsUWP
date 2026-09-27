# Saved Lot Probe 0.4.0.0

Update the existing FreeSims Offline Probe with the included x64 AppX.

Upload/merge the prepared PC folder
C:\Users\Joaco\Documents\Sims UWP Xbox\FreeSimsUWP\artifacts\saved-lot-upload\GameData
into the app's **LocalState/GameData**, preserving subfolders. Do not add another
GameData wrapper. The folder contains 18 files (about 358 MiB), including the six
previous uploads. Only these 12 archives are new, relative to LocalState/GameData:

- ExpansionShared/ExpansionShared.far
- ExpansionPack3/ExpansionPack3.far
- ExpansionPack5/ExpansionPack5.far
- Downloads/CCPlugin/CCPlugin.far
- Downloads/Christmas2000/Christmas2000.far
- Downloads/FenceParty/FenceParty.far
- Downloads/HPPottyPack/HPPottyPack.far
- Downloads/Jukebox/Jukebox.far
- Downloads/LampHulaUke/LampHulaUke.far
- Downloads/SlotMach/SlotMach.far
- Downloads/UnSnacker/UnSnacker.far
- Downloads/WallLite/WallLite.far

These are local copies of your installation, never included in the handoff ZIP.
On another PC, copy these relative paths from the installed game; retain the
existing GameData/{Objects,Global} archives and UserData neighborhood/house files.

Expect **11/11 PASS**. **A** reruns; **X** switches thumbnail previews; **B** exits.
Run twice, test Home/return and exit/relaunch, then send a photo and
LocalState/proof.log (plus proof.previous.log if present).

Expected log: 1,893 definitions from 14 archives; House 2: 388 entities/247 groups;
House 28: 755 entities/540 groups/11 container links. Twenty saved chairs restart
their main routines and run 150 ticks with zero script errors. Additional installed
archives may increase the definition count.

This imports headless object graphs and selected behavior only. Other threads remain
paused; saved stacks, relationships, Sims/person state and full gameplay saves are
unsupported. House images remain thumbnails, not live rendered lots.