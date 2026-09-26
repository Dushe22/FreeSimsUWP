# FreeSims Offline Probe — Series S
Install XboxOfflineProbe_0.1.0.0_x64.appx plus the three supplied x64 dependencies.
Launch once, then B to exit. Initial missing-file failures are expected.

In Device Portal, select **Dushe22.FreeSimsXboxOfflineProbe**, then LocalState.
Upload these two files from your PC's installed The Sims folder:

| PC relative path | Destination under this app's LocalState |
| --- | --- |
| UserData/Neighborhood.iff | GameData/UserData/Neighborhood.iff |
| UserData/Houses/House01.iff | GameData/UserData/Houses/House01.iff |

No Objects.far is needed for this probe. Previous probes use separate storage; either upload
from the PC again or copy these files from the Content Probe's matching locations.

Launch: expect **9/9 PASS** and a visible House 1 thumbnail.
A reruns. B exits. Repeat twice, Home/return and exit/relaunch; verify 9/9 and consistent sizing.
Send a photo and this app's LocalState/proof.log (proof.previous.log too if present).

Coverage: local command verification/order/deferral/shutdown, rejection of network-origin
commands, 150 real VM ticks on a synthetic EMPTY 8x8 headless lot, actual neighborhood/house
provider reads, saved-overlay selection, blocked incomplete serializers, and GPU thumbnail readback.
The clock test is not a populated Sims lot. This does not validate full gameplay or neighborhood
save serialization. The provider's new preview constructor explicitly blocks those saves.
Existing save overlays are read; tests write only a unique scratch copy under UserData/OfflineProbe.
No game data is packaged or redistributed.
