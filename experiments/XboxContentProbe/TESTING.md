# FreeSims Content Probe — Series S
Install XboxContentProbe_0.1.0.0_x64.appx and the three supplied x64 dependencies using Device Portal.
Launch once to create its LocalState folders, then exit with B. Missing-data failures are expected until upload.

In Device Portal File explorer, select **Dushe22.FreeSimsXboxContentProbe** (not the earlier probes), then LocalState.
Upload ONLY these three files from your PC installation, creating the exact destination folders:

| PC path under GameData/TheSims | Destination under this app's LocalState |
| --- | --- |
| GameData/Objects/Objects.far | GameData/GameData/Objects/Objects.far |
| UserData/Neighborhood.iff | GameData/UserData/Neighborhood.iff |
| UserData/Houses/House01.iff | GameData/UserData/Houses/House01.iff |

The repeated GameData is intentional: the outer directory is the app's installation-data root.
The app package and handoff ZIP contain no Sims data; upload your own files separately.

Relaunch: expect **9/9 PASS**. This installation logged 49 neighbors, 24 recognized house chunks,
126 object IFFs and 743 object definitions in the desktop test.
A reruns; B exits. Rerun twice, Home/return, and exit/relaunch: expect 9/9 each time and stable layout.
If any check fails, send the photo and this app's LocalState/proof.log (also proof.previous.log if present).

The tests parse real data and validate copy-on-write byte preservation and failure recovery.
Scratch copies go to LocalState/UserData/ContentProbe/<unique id>/Neighborhoods/UserData.
They do not overwrite uploaded source files or existing saves.
This does not start the simulation, render a lot, decode every resource, or validate full gameplay saves.
