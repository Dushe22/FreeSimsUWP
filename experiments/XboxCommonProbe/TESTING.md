# Xbox common/storage probe

This app tests shared sims.common code and the UWP storage boundary using
original synthetic data. It is not SimsVille gameplay and is separate from
the earlier Files Probe and standalone proof. Keep those installed if desired.

## Package and install

- App: FreeSims Common Probe.
- Identity: Dushe22.FreeSimsXboxCommonProbe, 0.1.0.0, x64.
- Main package: XboxCommonProbe_0.1.0.0_x64.appx.
- BUILD.txt in the handoff identifies the exact source commit and AppX SHA256.
  The screen displays the first 12 characters of that commit.

Extract the ZIP. In Xbox Dev Mode, open Device Portal at the address shown in
Dev Home. Use My games & apps / Apps manager -> Add to deploy the main AppX.
Supply the three packages under Dependencies/x64 when requested:
Microsoft.NET.Native.Framework.2.2.appx, Microsoft.NET.Native.Runtime.2.2.appx,
and Microsoft.VCLibs.x64.14.00.appx. Retain already installed newer framework
versions. The .cer is the public signing certificate, not a dependency package.
The game-data-probe.txt file is uploaded separately in the test below.

Launch **FreeSims Common Probe**. Do not launch Files Probe by mistake.

## Automatic checks and persistence

1. Within 30 seconds expect **12/12 PASS**, twelve green rows, a moving bar,
   increasing ENGINE LAYER FRAMES, a viewport readout, and PERSISTED VALUE 0 on a
   fresh install. WAITING FOR GAME DATA MARKER is expected initially.
2. Press A to rerun. Require 12/12 again. A must not change the persisted value.
3. Note the current value N, then press X three times, releasing between presses.
   Expect PERSISTED VALUE N+3. Each X writes and verifies a synthetic INI value.
4. Press B to exit, then relaunch. Require 12/12 and the same N+3 value.
5. Go Home and return three times, including 30 seconds at Home once. Require
   unchanged apparent layout and persisted value, and 12/12 after A.
6. Wait 60 seconds with the bar moving. Reconnect the controller and press A.
   Require 12/12 and unchanged persisted value.
7. Keep this app installed between checks; the test compares its retained data.

## Provisioning test without Sims assets

1. In Device Portal File explorer, navigate to User Folders -> LocalAppData ->
   Dushe22.FreeSimsXboxCommonProbe_<publisher-id> -> LocalState -> GameData.
   The app creates this folder on first launch. Select the Common Probe family.
2. Upload the supplied **game-data-probe.txt** into that GameData folder, keeping
   its filename. Its original synthetic contents are:
   FreeSims external game-data fixture v1
3. Return to the app and press A. Require **GAME DATA MARKER PASS**, 12/12 PASS,
   and the same persisted value. If an upload control is unavailable, return the
   exact Device Portal behavior; do not substitute uploading real game data.
4. Press B and relaunch. Require GAME DATA MARKER PASS and the retained value.

This checks external provisioning into the designated game-data root. It does
not detect or validate a Sims installation. The app only reads the marker; it
does not generate it there automatically.

## Controls, logs and success criteria

A = rerun checks/read marker. X = increment/write/read back synthetic INI state.
B = exit. Other inputs have no assigned probe action.

Return a photo of the final screen, the displayed commit, persisted values before
and after relaunch, console model/OS if known, and these files from this app:

- LocalState/proof.log and proof.log.previous if present.
- LocalState/UserData/Logs/events.log if the event fallback check fails.
- LocalState/UserData/common-probe.ini if the persistence check fails.

Expected log records include START COMMON PROBE with the full commit, PATHS,
RESULT 12/12 PASS, PERSISTENT CREATED/LOADED/STORED, GAME DATA MARKER PASS,
FIRST FRAME and DISPLAY viewport/back-buffer/scale diagnostics. No credentials
are needed in the report.

Pass requires all twelve automatic checks, retained synthetic state after exit
and Home/return, marker PASS after upload/relaunch, stable layout, responsive
controls and no runtime errors. Report any FAIL row, storage error, crash,
clipping or changed value rather than treating the run as a pass.

## Coverage and limits

The twelve checks cover content IDs, curves, shared text input, path mappings/
boundaries, INI creation/reflection/write/reload/blank lines, file logging,
packaged content reads, UWP event-log fallback, shared render-target clear and
GameScreen layer callbacks. The visible app itself updates/draws through
GameScreen. Persistence and external provisioning require the manual steps.

No Sims assets are packaged. This is not validation of real game saves, all
image/chunk types, full shaders, SimsVille UI, neighborhood/lot loading or
simulation. Those remain later milestones.

## Rebuild

From the exact clean source commit with Git on PATH:

~~~powershell
./scripts/Build-XboxProof.ps1 -Target CommonProbe -Sign
~~~

Output is artifacts/common-probe. Private signing keys are never exported and
are removed after signing.

References:
[UWP file locations](https://learn.microsoft.com/en-us/windows/apps/develop/files/file-access-permissions),
[Device Portal App File Explorer](https://blogs.windows.com/windowsdeveloper/2016/06/08/using-the-app-file-explorer-to-see-your-app-data/).
