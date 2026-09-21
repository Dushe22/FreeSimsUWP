# Xbox files probe: hardware procedure

This is the first engine-library probe, not the SimsVille game. It uses the
existing sims.files parsers and original synthetic fixtures. No Sims data needed.
The earlier FreeSims Xbox Proof remains a separate installed app.

## Exact source and package

Use the source commit and SHA256 recorded in BUILD.txt beside the handoff package.
The app displays the first 12 characters of that source commit.
Package: XboxFilesProbe_0.1.0.0_x64.appx.
Identity: Dushe22.FreeSimsXboxFilesProbe, x64, version 0.1.0.0.
Display name: FreeSims Files Probe.

Rebuild from a clean checkout of that commit, with Git on PATH:

~~~powershell
./scripts/Build-XboxProof.ps1 -Target FilesProbe -Sign
~~~

Default output: artifacts/files-probe. Signing refuses uncommitted source changes.
No private signing key is exported; it is removed after signing.

## Install on Xbox Series S/X Dev Mode

1. Start Developer Mode and open Xbox Device Portal using the address shown in
   Dev Home. Sign in locally; do not share its credentials.
2. In My games & apps / Apps manager, choose Add, select
   XboxFilesProbe_0.1.0.0_x64.appx, then Next.
3. Supply the three x64 dependency packages from Dependencies/x64:
   Microsoft.NET.Native.Framework.2.2.appx,
   Microsoft.NET.Native.Runtime.2.2.appx, Microsoft.VCLibs.x64.14.00.appx.
   If already installed at the required/newer versions, retain those versions.
4. Complete deployment. The .cer is the public test-signing certificate only;
   it is not an application/dependency package. Follow Device Portal's developer
   package prompt if it requests certificate approval.
5. Launch **FreeSims Files Probe**, not the earlier proof.

Reference: [Microsoft Xbox Device Portal deployment](https://learn.microsoft.com/en-us/previous-versions/windows/uwp/xbox-apps/device-portal-xbox).

## Test

1. Within 30 seconds, expect **10/10 PASS**, ten green PASS rows, RUN 1, and a
   moving blue bar. Check the displayed commit against BUILD.txt.
2. Wait 60 seconds. The blue bar should keep moving without errors.
3. Press A three times, releasing between presses. Each press reruns all ten
   tests. Expect 10/10 PASS and RUN 4.
4. Go to Xbox Home, return to the app, then press A once. Expect 10/10 PASS again.
   Record whether the app resumed or restarted; either must remain usable.
5. Disconnect/reconnect the controller and press A. The tests should rerun.
6. Press B to exit, relaunch, and confirm 10/10 PASS again.
7. Return the console model, exact OS build if available, displayed commit,
   result, any failed row, and the log below. A photo of the results is helpful.

A = rerun. B = exit. Other buttons/sticks have no function in this probe.

## What is tested

- CPU: BMP row padding/orientation/RGBA, PNG straight alpha and dimensions,
  caller stream ownership, malformed image rejection, reflective IFF chunk
  creation/lazy BCON decoding/byte order, malformed IFF rejection.
- GPU: all three existing BMP color keys, PNG straight alpha, premultiplication,
  and unpremultiplication through the shared ImageLoader plus texture readback.

These synthetic cases do not validate every image format, real Sims assets,
other IFF chunk payloads, gameplay, save/reload, or engine suspend safety.

## Logs and success criteria

Device Portal File explorer -> User Folders -> LocalAppData ->
Dushe22.FreeSimsXboxFilesProbe_<publisher-id> -> LocalState -> proof.log.
The package family may be displayed as FreeSims Files Probe. Select this app,
not Dushe22.FreeSimsXboxProof. Include proof.log.previous if present.

Expect START FILES PROBE with the full commit, TEST RUN n BEGIN,
ten PASS lines, RESULT 10/10 PASS, TEST RUN n END and FIRST FRAME.
Lifecycle/controller events appear as exercised; their presence does not prove
game save safety. If startup fails, also return Device Portal's exact error.

PASS requires 10/10 on every run, a moving bar, working rerun/exit/relaunch and
no runtime exceptions. A red FAIL row, stalled launch/bar, crash, or wrong commit
is not a pass. Return the log and stop; do not provision game data yet.