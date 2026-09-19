# FreeSims Xbox Developer Mode port

## Current status

Experimental, UWP proof source added; Windows CI is blocked before compilation
by a GitHub account restriction. Not tested on Xbox. Work is confined to
`xbox-uwp-port` in `Dushe22/FreeSimsUWP`; never push to original upstream.
Source baseline: `a7e9dba6cd4067b4efec54ae8e0443787da22992`.
No game engine implementation has been changed. No hardware milestones are tagged.

## Architecture and scope

FreeSims C# -> MonoGame 3.8.1 UWP -> DirectX -> Release x64 UWP -> Xbox Series X/S
Developer Mode. Preserve simulation, parsers, custom UI and rendering. Keep the
desktop target. First establish a separate, asset-free UWP proof application;
only integrate the engine after the toolchain and hardware proof pass.

Do not download, add, redistribute or package original Sims game data. Existing
upstream binary/content files have not been cleared for redistribution by this
audit. The proof package must include ONLY its own explicit source-generated
assets and restored framework dependencies, never the existing Content tree.
Future engine packaging requires a content provenance audit.

## Baseline and dependency map

All six existing projects target .NET Framework 4.5 with legacy MSBuild projects.

| Project | Role | References | Xbox disposition |
| --- | --- | --- | --- |
| SimsVille | x86 client, 470 compile entries | sims.common, sims.files | Share runtime source with dedicated UWP host |
| sims.common | library, 60 compile entries | sims.files | Adapt platform services; exclude WinForms controls |
| sims.files | library, 74 compile entries | MonoGame, TargaImagePCL | Keep parsers; replace GDI+ image handling |
| SimsNet | x86 server executable | all three runtime projects | Exclude standalone server |
| sims.debug | x86 WinForms utility | sims.files | Exclude |
| sims.parser | AnyCPU WinForms utility | sims.files, DiscUtils | Exclude |

The solution's Any CPU mapping still selects x86 for SimsVille. No Xbox target
exists at baseline. All binary HintPath references are present. Linux path checks
find 16 `World/Model` versus `World/model` and four `Content` versus `content`
source path mismatches, plus a `Sims.files` project reference in SimsNet. These
are case-sensitive filesystem issues, not demonstrated Windows compilation errors.

| Dependency / subsystem | Evidence | Classification / action |
| --- | --- | --- |
| MonoGame | Runtime projects reference one checked-in DLL using inconsistent 3.1.2/3.4 metadata; README says 3.6+ | Replace reference with pinned UWP 3.8.1.303; binary metadata is not proof of actual DLL version |
| OpenTK, Tao.Sdl | Desktop references; no direct OpenTK/Tao calls found in client/common C# audit | Remove from UWP target; use MonoGame's UWP backend |
| GOLDEngine | Binary dependency for UI grammar parsing | Unknown UWP/.NET Native compatibility; test before replacement |
| GonzoNet | Client network classes reference binary | Unknown UWP compatibility; determine minimum offline runtime reachability |
| TargaImagePCL | Used by ImageLoader | Candidate portable dependency; verify API surface and channel order |
| System.Drawing | ImageLoader.BitmapReader, BMP.GetBitmap; WinForms tools | Runtime decoder must be replaced; exclude editor-only APIs |
| System.Windows.Forms | Client Debug folder; common rendering/framework/winforms | Exclude and guard callers, including CoreGameScreen debug tools |
| Registry/process APIs | Utils/GameLocator/WindowsLocator.cs | Replace discovery with explicit UWP game-data root |
| Dynamic assembly selection | Program.cs AssemblyResolve/LoadFrom; MonogameLinker | Remove for UWP; use static references |
| Reflection | IffFile, UIScript, IniConfig, UI factories, VM dispatch | Keep initially; verify Release/.NET Native and preserve metadata as needed |
| Audio | HIT uses SoundEffect/instances and managed XA decoding | Retain; test UWP audio and recovery |
| Input | Custom mouse/keyboard UI; no GamePad calls found in client/common | Add controller pointer adapter later; proof tests GamePad independently |
| Other dependency-folder DLLs | SciLexer/64, ScintillaNET, DiscUtils, OpenNat, SharpDX, TargaImage | Presence is not a runtime dependency; do not bulk-copy folder to Xbox |

Native SciLexer binaries include x86 and x64 versions, but are editor-related.
PE32 on a managed DLL alone does not establish an x86-only runtime restriction;
check CLR flags before making architecture decisions.

The only packages.config found belongs to sims.debug and names
MonoGame.Framework.WindowsGL 3.4.0.459. Runtime dependencies are checked-in DLLs,
not a reproducible NuGet restore. No .gitignore existed before this branch.

## Startup, graphics, storage and existing bugs

- Program.InitWithArguments sets `useDX = false`. The Xbox host must consistently
  select DX in FSOEnvironment, GameFacade and World and use direct UWP references.
- MonogameLinker selects `Monogame/WindowsGL` etc. and deletes a local framework
  DLL; those runtime directories are absent from the checkout. Desktop runtime
  deployment is not proven by successful compilation.
- Shader sources and precompiled DX/OGL effects/fonts exist; no .mgcb was found.
  Rebuild effects for the chosen MonoGame version; do not assume old XNB ABI
  compatibility. Shader DX branches use SM4.
- BitmapReader calls GDI+ and swaps BGRA to RGBA. Preserve dimensions, orientation,
  mask colors and existing premultiplication behavior in the replacement.
- GlobalSettings/IniConfig use relative configuration paths and reflection.
- Content systems use relative paths and create Downloads/Houses; ChangeManager
  also writes content. Inventory all writes before sharing code with UWP.
- TS1NeighborhoodProvider reads UserData/Neighborhood.iff but saves Neighborhood.iff
  at UserPath's root. Its comment about copying user data does not implement the
  copy. Validate against desktop before changing save semantics.
- WindowsLocator has a SteamInstallPath field that is never assigned but is used
  in the Steam discovery branch. This is a pre-existing finding, not an Xbox bug.
- CursorManager's user32 cursor implementation and InputManager's MapVirtualKey
  declaration are not evidence of active calls; distinguish dead code from real
  portability blockers.

## Toolchain

Chosen build host: GitHub Actions **windows-2022**, Visual Studio 2022 MSBuild,
UWP managed tooling, .NET Native and a supported Windows SDK. Do not use
windows-latest, whose installed Visual Studio version can change.

Pin `MonoGame.Framework.WindowsUniversal` to **3.8.1.303**. UWP was removed in
3.8.2. Start from MonoGame v3.8.1's CoreApp template/API, not a desktop executable
wrapped in an AppX. Use Release/x64 and .NET Native from the first proof build.
Build-time and runtime framework versions must remain consistent.

Local host: Linux x86_64, Git 2.43.0; no dotnet, Mono or MSBuild found.
Desktop baseline attempt:

```text
msbuild SimsVille/SimsVille.csproj /p:Configuration=Release /p:Platform=x86
Result: command not found (exit 127); compiler never ran.
```

This is a host-toolchain blocker, not a FreeSims source compilation failure.
Windows CI will record the actual desktop baseline separately from UWP results.

## Build and deployment instructions

The `Xbox UWP proof and desktop baseline` workflow runs on windows-2022 for proof,
script or workflow changes pushed to xbox-uwp-port; it can also be dispatched
manually. Desktop baseline failures are reported separately and do not prevent
the independent proof job. A green workflow alone is not evidence that the
desktop baseline passed: inspect its explicit compiler outcome and logs.

On Windows with VS2022 managed UWP/.NET Native tooling and SDK 10.0.19041.0:

```powershell
./scripts/Build-XboxProof.ps1 -Sign
# Separate baseline diagnostic (requires nuget.exe on PATH):
./scripts/Build-DesktopBaseline.ps1
```

The proof script generates its own geometric PNG logos and source commit identity,
restores pinned packages, compiles Release/x64 with .NET Native and produces AppX.
Its optional signing step creates an ephemeral development key in the Windows
certificate store, signs the application, exports only the public .cer, and deletes
the private key. No PFX or signing password is created. Build the project through
this script first; generated assets/BuildInfo are intentionally not committed.

Artifacts are named `xbox-uwp-proof-<full commit>` and
`xbox-uwp-proof-logs-<full commit>`. The package artifact includes AppPackages with
framework dependencies, a public certificate and BUILD.txt with commit and SHA256.
Failed compilations upload diagnostic logs only. The desktop job uploads logs,
not the original engine binaries or content. The first run was blocked before
either runner started; consequently no build logs or package artifacts exist yet.

### Recorded CI attempt

- Source commit: `da8369358a8042d584ef8629301c63ec7fdc0e88`.
- Run: https://github.com/Dushe22/FreeSimsUWP/actions/runs/35454183333
- Both `desktop-baseline` and `uwp-proof` failed with **zero executed steps**.
- GitHub check annotation: "The job was not started because your account is locked
  due to a billing issue."
- This is an account/runner provisioning failure, not a compiler or package error.
  No Windows compiler result can be claimed for either configuration.
- User action: resolve the GitHub Actions account restriction. Then rerun all jobs
  on this run (it tests the exact commit above), or dispatch `xbox-proof.yml` with
  ref `xbox-uwp-port` to test the latest branch. Do not change account billing or
  spend limits automatically.

Future deployment: enable Xbox Developer Mode/Device Portal, deploy the exact
Release/x64 test package and its x64 framework dependencies, then test on hardware.
Record source commit, Actions run, package identity, expected scene, controls and
log retrieval instructions when a package actually exists.

### Proof hardware procedure (use only after a successful package build)

1. Download the package artifact for the specified source commit in GitHub Actions.
   Extract it on the deployment computer. Read BUILD.txt and verify the SHA256.
2. On the console enable Developer Mode and Xbox Device Portal/remote access.
   Open the console's displayed Device Portal URL on the same local network.
3. In Device Portal's application deployment page select the main `.appx` from
   AppPackages (not a framework dependency). Add the x64 framework `.appx` files
   from its Dependencies/x64 directory if included/required; install the package.
   Do not upload the .cer as an application or any .pfx. The .cer is the public
   development certificate, useful for trust setup when testing on Windows PCs.
4. Launch **FreeSims Xbox Proof**. If Dev Home offers an App/Game classification,
   select Game. Report the actual console OS and classification with results.
5. Expect a dark background, title, commit prefix, controller/audio status and a
   moving blue rectangle. This is a toolchain proof, not a Sims menu.
6. On controller 1 test left stick (crosshair), A (toggle rectangle color/count),
   B (recenter), X (quarter-second generated tone), Menu (pause/resume animation).
   Disconnect/reconnect and optionally leave/reopen the app; note any failures.
7. In Device Portal File Explorer, select this application's local data and
   download LocalState/proof.log (and proof.log.previous if present). Portal labels
   vary; LocalState is the app-local storage, not its installation directory.
   Log events report submission only, not proof that pixels/sound reached hardware.
8. Return full commit/BUILD.txt, Series X or S, console OS, visible/audio/input
   results, any deployment HRESULT, and proof.log. If it fails before writing a
   log, return the Device Portal deployment/launch error and available crash dump.

Do not load any Sims data for this test. Stop here and wait for user results before
claiming hardware success or tagging xbox-poc-uwp/first-launch/first-render.

## Game-data layout for the later engine target

- Application content: explicitly selected, redistributable engine resources in
  the read-only installed package.
- Game data: user-provided Sims installation, provisioned separately; initially
  use LocalState/GameData to avoid relying on unrestricted desktop/USB paths.
- User data: writable LocalState/UserData for copied neighborhoods and saves,
  separate settings and diagnostics. Never modify the user's source installation.

The proof does not load Sims data and must not request it.

## Completed milestones

- Source dependency/platform inventory recorded.
- GitHub write access verified; xbox-uwp-port pushed and tracking origin.
- Ignore rules added for build output, local game data, saves and private keys.
- Known secret-format scan found no matches (not a comprehensive security audit).
- Minimal CoreApplication/MonoGame proof source added, pinned to 3.8.1.303.
- Separate Windows desktop baseline and signed UWP package CI paths added.

## Current blockers / next milestones

1. Wait for the user to unlock GitHub Actions, then establish the Windows desktop
   compiler baseline and capture failures separately.
2. Compile/package asset-free MonoGame 3.8.1 UWP proof with rendering, input,
   diagnostic logging and generated tone audio.
3. Stop for user Xbox test results; no hardware success tags before confirmation.
4. Only then introduce the shared-source FreeSims UWP target and platform services.

## Xbox-specific modifications

The proof is isolated in experiments/XboxUwpProof and shares no engine source yet.
It exercises DirectX rendering, GamePad input, generated PCM audio, local log
storage and lifecycle event logging. Lifecycle events are diagnostic only; full
engine save safety/device recovery remains later work. All desktop sources remain
unchanged.

## Hardware test results

None. Neither Xbox Series X nor Series S has been tested. Never infer hardware
success from compilation or a GitHub Actions green check.

## Rejected approaches

Win32 Runner, Wine and BoxedWine are outside project scope. A C++ engine rewrite,
latest-MonoGame upgrade and porting standalone desktop utilities are also excluded.

## References

- https://github.com/francot514/FreeSims/tree/a7e9dba6cd4067b4efec54ae8e0443787da22992
- https://github.com/MonoGame/MonoGame/tree/v3.8.1/Templates/MonoGame.Templates.CSharp/content/MonoGame.Application.UWP.CoreApp.CSharp
- https://www.nuget.org/packages/MonoGame.Framework.WindowsUniversal/3.8.1.303
- https://monogame.net/blog/2024-08-16-monogame-382/
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md
