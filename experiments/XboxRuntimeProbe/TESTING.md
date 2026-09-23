# FreeSims Runtime Probe — Xbox Series S / X

This separate app runs the shared SimsVille UILayer, UIScreen, UIButton, text
renderer, input routing and GlobalSettings under .NET Native. It uses original
generated glyphs and button pixels. No Sims installation, downloaded assets,
legacy Content folder, network connection, neighborhood or game save is used.

## Package and installation

Read BUILD.txt in this bundle for the exact source commit and AppX SHA256.
The source commit displayed by the app must match BUILD.txt's first 12 characters.

- Identity: Dushe22.FreeSimsXboxRuntimeProbe, version 0.1.0.0, x64.
- Install XboxRuntimeProbe_0.1.0.0_x64.appx using Xbox Developer Mode Device Portal:
  My games & apps / Add (or the Apps deployment page).
- Add the three supplied Dependencies/x64 packages: Microsoft.NET.Native.Framework.2.2,
  Microsoft.NET.Native.Runtime.2.2 and Microsoft.VCLibs.x64.14.00.
- The public FreeSimsXboxDevelopment.cer is included for certificate inspection or
  trust prompts. No private key is included. Use the same sideload procedure as
  the previous probes; do not replace the original Common Probe installation.
- Launch **FreeSims Runtime Probe** on the console. Allow the initial checks to finish.

## Expected screen

- FREESIMS RUNTIME PROBE and the correct source commit.
- **10/10 PASS** with four actual client-settings cases, two controller-pointer
  cases, UI click routing, canceled press routing, UI GPU readback and offline UI boot.
- A blue **TOGGLE HINTS** button and white crosshair pointer.
- **HINTS ON - LOADED** on a fresh install, or the previously saved ON/OFF value.
- This is a client UI integration test. It is not the Sims main menu or a playable lot.

## Controls and procedure

1. Leave the left stick centered for 10 seconds: the pointer should not drift.
   Move it in all directions and to every edge: it must remain inside the UI.
2. Move onto TOGGLE HINTS and press/release **A** once. The existing UIButton
   should highlight, depress, and toggle HINTS ON/OFF once; the status ends in
   **SAVED**, and CLICKS increases by one. Each successful click writes and reads
   the actual GlobalSettings.ShowHints property from config.ini.
3. Hold A over the button for a second: no repeat clicks. Release: exactly one click.
   Press A away from the button: no change.
4. Hold A over the button, press **B**, then release both. B cancels the pending
   press: there must be no toggle or extra click, and the button must not stay down.
5. Note the current HINTS value. Press **Menu** to exit and relaunch. Require the
   same value with LOADED. A fresh launch starts CLICKS at zero; this is intentional.
6. Go to Xbox Home and reopen the app. Require consistent UI size, pointer/button
   alignment, responsive input and the same saved value. Repeat while A is held
   over the button before going Home; release A at Home. Return must not generate
   an unintended click or leave the button depressed.
7. Disconnect/reconnect the controller, including while A is down if practical.
   Reconnection must not toggle settings; release A before the next deliberate click.
8. Press **Y** to rerun the ten checks after returning. Require 10/10 PASS. Y resets
   the display click count but must not change the saved HINTS setting.
9. Press Menu to exit. Report any black screen, exception, incorrect scaling,
   stuck press, missing caption, failed test, unexpected toggle or persistence loss.

Only left-stick pointer, A select, B cancel, Y rerun and Menu exit are implemented
here. Camera, simulation controls, neighborhood loading and game saves are later work.
The pointer defaults to 600 logical pixels/second with a radial 0.2 dead zone;
speed and dead zone are adjustable in the shared ControllerPointer API.

## Return evidence

Return a photo of the result/button, console model and exact OS build if available,
and **LocalState/proof.log** (plus proof.log.previous if it exists).

Device Portal File explorer:
User Folders / LocalAppData / Dushe22.FreeSimsXboxRuntimeProbe_<publisher-id> /
LocalState. This app has a different folder from Common Probe.

For a settings failure, also return **LocalState/UserData/config.ini**. It contains
only this probe's client settings; inspect before sharing. The separate
UserData/ClientSettingsTests directory contains synthetic regression fixtures.

Success requires ten passes, responsive shared UI rendering/clicks, correct focus
cancellation and size after Home/return, and retained HINTS across relaunch.
No Xbox behavior is considered verified until you return the hardware results.
