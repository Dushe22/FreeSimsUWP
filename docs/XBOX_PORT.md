# FreeSims Xbox Developer Mode port

## Current status

The original MonoGame proof passed the user's complete hardware procedure on
**Xbox Series S** (OS reported as "latest", exact OS build not supplied).
Tested source: 3aaafa134f31b7fdb4216e8a1b12c44447323a22, tag xbox-poc-uwp.

The user confirmed all ten files-probe checks passed on Series S for source
173ddaa0855939406413d4acea15c6798e03528a, with photos showing 10/10 PASS.
The subsequent 0.1.1.0 resize retest passed according to the user.
The 0.1.1.0 retest adapts the logical layout to the live viewport and adds display
diagnostics. The user confirmed all requested tests passed; see the dated result below.
The Common/storage probe passed on Series S for source 27f318c55deb2dadff92ba827f63ba9b572fd1d5. The supplied log confirms 20 successful runs, persistence across three launches and external marker reads. SimsVille integration is next; see the dated hardware result below.

Work is on xbox-uwp-port in Dushe22/FreeSimsUWP. GitHub Actions remains blocked by
account billing; local builds provide validation. The engine changes so far are
shared sims.files and sims.common libraries, image/logging adapters and separate
storage roots. SimsVille now compiles as a shared UWP runtime library. The native client UI probe awaits hardware validation; full game startup and content loading remain unfinished.

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

Historical (previous workspace) host: Linux x86_64, Git 2.43.0; no dotnet, Mono
or MSBuild found. See Recovered Project State below for the new Windows host.
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

### General proof hardware procedure (see exact signed handoff below)

1. Use the signed local ZIP specified in the final hardware handoff below. GitHub
   Actions artifacts are unavailable while account billing blocks the runners.
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

1. Proof hardware procedure passed according to the user. Preserve xbox-poc-uwp.
2. Shared sims.files and UWP decoder compile; desktop CPU fixtures pass.
3. Files-probe checks passed on Series S; verify the Home/return layout fix next.
4. Integrate the remaining engine incrementally; saves and simulation are unverified.
5. GitHub Actions billing remains a separate account blocker; use local builds.

## Xbox-specific modifications

The proof is isolated in experiments/XboxUwpProof and shares no engine source yet.
It exercises DirectX rendering, GamePad input, generated PCM audio, local log
storage and lifecycle event logging. Lifecycle events are diagnostic only; full
engine save safety/device recovery remains later work. The new XboxFilesProbe
shares sims.files and keeps its desktop decoder/API in desktop-only files.

## Hardware test results

User-reported PASS received 2026-09-21 for proof commit
3aaafa134f31b7fdb4216e8a1b12c44447323a22. See the dated hardware result
at the end of this document for scope and limitations.

## Rejected approaches

Win32 Runner, Wine and BoxedWine are outside project scope. A C++ engine rewrite,
latest-MonoGame upgrade and porting standalone desktop utilities are also excluded.

## References

- https://github.com/francot514/FreeSims/tree/a7e9dba6cd4067b4efec54ae8e0443787da22992
- https://github.com/MonoGame/MonoGame/tree/v3.8.1/Templates/MonoGame.Templates.CSharp/content/MonoGame.Application.UWP.CoreApp.CSharp
- https://www.nuget.org/packages/MonoGame.Framework.WindowsUniversal/3.8.1.303
- https://monogame.net/blog/2024-08-16-monogame-382/
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md

# Recovered Project State

Recovery date: 2026-09-20. This section records the recovered state before new
implementation changes. Later validation entries supersede its build status.

## Repository state

- Working repository: https://github.com/Dushe22/FreeSimsUWP
- Recovered branch: `xbox-uwp-port`, tracking `origin/xbox-uwp-port`.
- Recovered latest commit: `0d47e33c5610d3806de08f3e58b7cecbebcd4bfa`,
  `docs: record GitHub Actions account blocker and unverified build status`.
- Default branch: `master`, at `a7e9dba6cd4067b4efec54ae8e0443787da22992`.
- Origin fetch/push: `https://github.com/Dushe22/FreeSimsUWP.git`.
- Comparison-only upstream: `https://github.com/francot514/FreeSims.git`.
- Fresh full clone; fetched all branches and tags, inspected all-history graph
  and commit subjects, each port commit and its complete source changes.
  No shallow history, reset, upstream merge or force push was used.
- Origin has only master and xbox-uwp-port; no origin tags. Port is three commits
  ahead of master. Origin master and upstream master have the same commit/tree.
- Upstream also has `fixes` at `4a882a65c46f652c605302d4b1e15a062f355d2a`
  and `upgrades` at `4a248431741656598ffba697d61fafd75cbef878`.
  These unmerged reference branches contain engine/desktop changes, not surviving
  Xbox work (no UWP/Xbox/WindowsUniversal project or source matches).
  They are not substitutes for the recovered port branch.
- 323 reachable commits across fetched references; 51 upstream SimsVille release
  tags. These are upstream history, not Xbox milestones. No tags were created.

### Previous development commits

1. `a4a0ee84693561d2c9fbb6db3cf729b100fde1ab`: original dependency/port audit,
   documentation and ignore rules.
2. `da8369358a8042d584ef8629301c63ec7fdc0e88`: isolated UWP proof, build scripts
   and Windows CI.
3. `0d47e33c5610d3806de08f3e58b7cecbebcd4bfa`: recorded CI account failure and
   explicitly unverified compilation/hardware status.

## Existing Xbox/UWP work

All 13 files changed from upstream are listed below. No engine source changed.
"Unverified" means no successful compatible compiler run exists at recovery,
not that the implementation is known to be broken. Retain all recovered work.

| File | Purpose and recovered implementation | Compilation/completeness |
| --- | --- | --- |
| `docs/XBOX_PORT.md` | Architecture, dependency audit, blockers and future hardware procedure | Documentation exists; local Linux environment description is historical |
| `.gitignore` | Build output, root game data/saves and private-key exclusions | Retain; nested data-directory coverage needs expansion before provisioning |
| `.github/workflows/xbox-proof.yml` | Independent desktop and UWP jobs on windows-2022 | Present; only recorded run failed before either job executed |
| `scripts/Build-DesktopBaseline.ps1` | Restore .NET 4.5 references and invoke Release/x86 build | Partial: missing-tool diagnostics and x86 propagation to AnyCPU libraries need correction |
| `scripts/Build-XboxProof.ps1` | Generate original logos/build identity, restore/build/package and optionally sign with temporary key | Present; not yet executed past toolchain discovery |
| `experiments/XboxUwpProof/XboxUwpProof.csproj` | Explicit-source x64 UWP project, .NET Native Release, MonoGame 3.8.1.303 and UWP 6.2.14 | Unverified; retain pins and DirectX target |
| `experiments/XboxUwpProof/Package.appxmanifest` | App identity, universal device family, original generated logo references | Unverified packaging/activation |
| `experiments/XboxUwpProof/Program.cs` | CoreApplication + MonoGame framework view, exception/lifecycle logging | Unverified; no engine startup integration |
| `experiments/XboxUwpProof/ProofGame.cs` | SpriteBatch geometry, generated PCM tone, controller pointer and buttons | Unverified; fixed 500 px/s proof pointer, not full requested engine controls |
| `experiments/XboxUwpProof/PixelText.cs` | Original grid font without content pipeline assets | Unverified; complete enough for proof source |
| `experiments/XboxUwpProof/ProofLog.cs` | LocalFolder log, rotation, diagnostic fallback | Unverified runtime; save/lifecycle safety not implemented |
| `experiments/XboxUwpProof/Properties/AssemblyInfo.cs` | Proof assembly metadata | Unverified build |
| `experiments/XboxUwpProof/Properties/Default.rd.xml` | Application metadata preservation for .NET Native | Unverified native compilation |

### Engine audit rechecked

- SimsVille, sims.common and sims.files remain legacy .NET Framework 4.5 projects.
  Client is x86; the two libraries define AnyCPU configurations.
- `SimsVille/Program.cs` still chooses OpenGL (`useDX = false`), calls
  MonogameLinker and uses Assembly.LoadFrom. The UWP proof bypasses this startup.
- WinForms and System.Drawing still appear in runtime project references.
  `sims.files/ImageLoader.cs` still decodes through GDI+ Bitmap/LockBits.
- `SimsVille/Utils/GameLocator/WindowsLocator.cs` still uses Registry APIs and
  reads an unassigned SteamInstallPath field. These are upstream code.
- GamePad/LocalFolder use exists in the proof, not an engine platform abstraction.
- Existing DirectX/OpenGL content and shader sources are inherited unchanged.
  No new shader pipeline, engine decoder, storage or controller adapter survived.
- Debug/parser/server tools remain excluded from planned UWP integration.
  Building only the SimsVille solution target can use existing library mappings
  without building those separate executables.

## Missing work

Compatible local build validation; successful proof packaging; Xbox launch,
render/audio/controller/lifecycle hardware confirmation; shared-source engine UWP
host; UWP image decoding; explicit game/user/content paths; removal of desktop-only
dependencies from that host; reflection/native validation; engine controller
adapter; shader rebuild/provenance audit; neighborhood/lot/simulation/save/reload.
These are planned milestones, not recovered implementations.

## Suspected lost work

**LOST OR UNCONFIRMED WORK**: Any claimed compiled UWP package, console deployment,
hardware test, integrated engine UWP runtime, working decoder/platform services,
controller-driven engine UI or save/resume implementation beyond this proof.
No such implementation is present on origin branches/tags or in port commits.
The recovered documentation does not claim these existed. A lost local workspace
cannot establish their existence; do not recreate an imaginary later state.

## Windows development environment at recovery

- Windows 10 Enterprise LTSC, 64-bit, version 10.0.19044.
- Bundled Git 2.53.0.windows.3 found outside PATH:
  `C:\Users\Joaco\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`.
- No Visual Studio Installer/vswhere, VS MSBuild, Windows Kits SDK, managed UWP
  workload, .NET Native tooling, .NET SDK or NuGet CLI found initially.
- .NET Framework runtime 4.8.04084 and legacy framework MSBuild file version
  4.8.4084.0 are present. They do not replace VS2022 UWP tooling or net45 references.
- No separate MonoGame tooling/package cache found. The proof needs no MGCB;
  it generates geometry/audio and restores the pinned framework package.
- Microsoft-signed VS2022 Build Tools installation was started with managed desktop,
  UniversalBuildTools, NetFX.Native and Windows10SDK.19041 components.
  Installation completion and exact resulting versions must be checked afterward.
- Git author identity and GitHub credential account were not configured on this
  new machine at recovery. Prior-machine push success is not current authorization
  state; verify a real push after the recovery commit.

## Initial build attempts (before implementation changes)

From the repository root, with bundled Git added to the process PATH:

| Command | Result | Classification |
| --- | --- | --- |
| `./scripts/Build-DesktopBaseline.ps1` | FAIL before compilation: vswhere.exe not found | WINDOWS TOOLCHAIN ERROR |
| `./scripts/Build-XboxProof.ps1` | FAIL before compilation: Visual Studio Installer / vswhere is required | WINDOWS TOOLCHAIN ERROR |
| Framework MSBuild `SimsVille/SimsVille.csproj /p:Configuration=Release /p:Platform=x86 /verbosity:minimal /nologo` | Exit 1: MSB3644 net45 reference assemblies missing; OutputPath undefined for sims.common and sims.files at Release/x86 | MISSING DEPENDENCY + RECOVERED PORT ERROR in baseline invocation |
| Framework MSBuild `experiments/XboxUwpProof/XboxUwpProof.csproj /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /verbosity:minimal /nologo` | Exit 1: MSB4066 PackageReference Version unrecognized by old MSBuild | WINDOWS TOOLCHAIN ERROR |

Framework MSBuild path:
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`.
Desktop runtime: NOT TESTED. Engine UWP target: absent / NOT TESTED.
UWP Release/x64 proof and packaging: FAIL before compatible compilation.
No package generated; Xbox test is not yet actionable.

## BLOCKER

The recovered targets cannot be validated with the initially installed toolchain.
The desktop script also propagates an invalid platform to library dependencies.

## EVIDENCE

Missing vswhere/SDK, MSB3644, MSB4066 and Release/x86 OutputPath errors above.
`SimsVille/SimsVille.sln` already maps the Release/x86 solution configuration to
Release/Any CPU libraries.

## CAUSE

Fresh Windows installation lacks development components. Building the client
project directly bypasses solution configuration mappings.

## OPTIONS

Install the matching VS2022/UWP tools and build locally, or restore access to
GitHub Actions. Use the existing solution's SimsVille target for the desktop
baseline rather than inventing library x86 configurations.

## RECOMMENDATION

Finish local toolchain setup, checkpoint/push this recovery, correct the baseline
invocation, then compile/package the existing proof with minimal evidence-driven
fixes. Do not change the engine or upgrade MonoGame to compensate for missing
tooling. Keep the historical failed CI experiment for context.

## Windows recovery validation: desktop baseline

Recovery checkpoint `be5baa1cc96c5376e5407b114ff5a3e54d33d784` was pushed to
origin/xbox-uwp-port and its remote hash verified after GitHub browser sign-in.

- Installed VS2022 Build Tools reports 17.14.41 / 17.14.37710.0; MSBuild
  17.14.60.43110. The outer installer is still completing remaining components.
- Microsoft-signed NuGet CLI 6.14.0 obtained from
  https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe.
- Restored Microsoft.NETFramework.ReferenceAssemblies.net45 **1.0.3**.
  No desktop retargeting or dependency upgrade was necessary.
- The original baseline script failed under modern MSBuild with the same invalid
  Release/x86 library configuration, confirming a recovered script defect.
- Building `SimsVille/SimsVille.sln /t:SimsVille` with Release/x86 and the restored
  TargetFrameworkRootPath **PASS** (exit 0). It compiled Simslib.dll, Common.dll and
  SimsVille.exe. Existing warnings include obsolete graphics calls, unused fields
  and the unassigned SteamInstallPath. No source errors were found.
- Updated `Build-DesktopBaseline.ps1` to use that solution target, check tools,
  accept `-NuGetPath`, and normalize its log output directory.
  The updated script also **PASS** (exit 0).
- The proof restored MonoGame **3.8.1.303** / UWP **6.2.14** successfully, then failed
  with MSB3644 for `.NETCore,Version=v5.0` while the toolchain installation was still
  completing. This is the legacy UWP framework identity, not a request to upgrade
  this application to modern .NET 5. Wait for installation completion and retry.
- Desktop runtime/game loading and all Xbox behavior remain NOT TESTED.

### Reproducible Windows setup

`.vsconfig` records the VS2022 **Build Tools** workload/component selections.
Use the VS2022 bootstrapper from https://aka.ms/vs/17/release/vs_buildtools.exe,
verify its Microsoft signature, and install from an elevated PowerShell:

```powershell
# Run from the repository root. Wait for the installer to finish before building.
$setup = Start-Process .\vs_buildtools.exe -ArgumentList @(
    '--quiet', '--wait', '--norestart', '--nocache',
    '--config', ('"' + (Join-Path $PWD '.vsconfig') + '"')
) -WindowStyle Hidden -PassThru -Wait
$setup.ExitCode # 0 = success; 3010 = restart required
# NuGet CLI can be placed outside the repository; pass its full path:
./scripts/Build-DesktopBaseline.ps1 -NuGetPath 'C:\Tools\nuget.exe'
./scripts/Build-XboxProof.ps1 -Sign
```

The Visual Studio IDE itself is optional for command-line builds. Keep the Windows
SDK at 10.0.19041.0 and the package pins above. Generated output remains ignored.
`.gitignore` now also excludes nested GameData/UserData/LocalState/Downloads/Saves
directories, because desktop output and UWP local data need not be at repo root.
No game data, private keys, credentials or newly obtained binary dependencies were
staged or committed. No application assets were downloaded.

Local logs: `artifacts/desktop-baseline/build.log`, `desktop.binlog`, and the
initial diagnostic `solution.log`/`solution.binlog`; proof diagnostics under
`artifacts/proof`. These are ignored machine-specific artifacts, not release files.

## UWP packaging validation and manifest correction

The VS2022 Build Tools installer finished with exit **0** on 2026-09-20.
Windows SDK 10.0.19041.0 is installed; MakeAppx reports 10.0.19041.5609.
The earlier missing `.NETCore,v5.0` references were resolved by completing the
installation, without retargeting the project.

With the completed toolchain, `./scripts/Build-XboxProof.ps1 -Sign` compiled C#
and .NET Native Release/x64, then hit **RECOVERED PORT ERROR** `APPX1673:
App manifest is missing required element 'PhoneIdentity'`.

The recovered manifest had omitted the phone namespace/identity present in
[MonoGame v3.8.1's CoreApp template](https://github.com/MonoGame/MonoGame/blob/v3.8.1/Templates/MonoGame.Templates.CSharp/content/MonoGame.Application.UWP.CoreApp.CSharp/Package.appxmanifest).
Restored that metadata using the proof project's stable GUID and the template's
zero PhonePublisherId. Kept Windows.Universal targeting, x64 and all package pins.
This satisfies the UWP packaging tooling; it does not add a phone build target.

Validation after this two-line metadata addition: **PASS** for C#, .NET Native and
unsigned AppX creation (MSBuild exit 0). Command:

```powershell
MSBuild.exe experiments/XboxUwpProof/XboxUwpProof.csproj /m /verbosity:minimal /p:Configuration=Release /p:Platform=x64 /p:TargetPlatformVersion=10.0.19041.0 /p:UseDotNetNativeToolchain=true /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false /p:AppxPackageDir=artifacts/proof-validation/AppPackages/ /bl:artifacts/proof-validation/validation.binlog
```

MSBuild is the full VS2022 MSBuild path reported in the recovery section.
The provisional unsigned validation output is not the hardware handoff: its
generated BuildInfo still identifies the preceding commit. After committing this
fix, rebuild through Build-XboxProof.ps1 to stamp the actual source commit and sign.

### Known compiler warnings

.NET Native emits five **MCG0007** warnings from SharpDX.MediaFoundation for
`Mfplat.dll!MFCreateMuxStreamAttributes`, `MFCreateMuxStreamMediaType`,
`MFCreateMuxStreamSample`, `MFCreateSensorGroup`, and `MFCreateSensorStream`.
The compiler says invoking these unresolved imports would throw at runtime.
The proof contains no direct calls to these methods (its tone uses SoundEffect
with generated PCM), but Xbox runtime behavior remains unverified. Preserve these
warnings in the record; do not suppress them or patch third-party binaries without
a demonstrated runtime failure.

### Current GitHub Actions evidence

Run https://github.com/Dushe22/FreeSimsUWP/actions/runs/35531409009 tested
`bdc373c0d49a7ba669b3d21f211fd0a6ab5b6ccd`. Both jobs again had zero executed steps;
the API annotations report the account locked due to a billing issue.
Local compiler results above are independent of that failed workflow. No account
billing settings were changed.

## Signed hardware handoff verified on 2026-09-21

The signed build from clean source commit
`3aaafa134f31b7fdb4216e8a1b12c44447323a22` completed through the unchanged
`scripts/Build-XboxProof.ps1 -Sign` workflow. Its BUILD.txt was written after
successful build/signing and records `Signed test package: True`.

| Target | Result | Scope |
| --- | --- | --- |
| Desktop Release/x86 | PASS | Untouched engine compilation through the corrected baseline script; runtime NOT TESTED |
| UWP proof Release/x64 | PASS | C# and .NET Native compilation with MonoGame 3.8.1.303 |
| AppX packaging/signing | PASS | Generated signed x64 application and framework dependency packages |
| Integrated FreeSims UWP runtime | NOT TESTED / not implemented | Proof remains isolated from the engine |
| Xbox Series X/S | NOT TESTED | User hardware test required; stop here |

Validation of the final main AppX:
- Manifest identity `Dushe22.FreeSimsXboxProof`, version `0.1.0.0`, architecture x64.
- SHA256 matches BUILD.txt:
  `F53942D037A329986A8229484E26345374DAE3A6D466FBAA6CD89F37C5E5B08C`.
- All **171** blocks in AppxBlockMap.xml match their payload SHA256 hashes.
- Embedded CMS signature passes cryptographic verification; its signer matches
  FreeSimsXboxDevelopment.cer. This is self-signed development identity verification,
  not an assertion of Microsoft Store or operating-system trust.
- Certificate thumbprint: `95DD0B7D658FFC77B7A33B3A1E8363EA57FB4125`.
  Public .cer has no private key; no matching private-key certificate remains
  in CurrentUser/My after the signing script's cleanup.
- Payload inventory contains the native proof executable/library, clrcompression,
  MonoGame build metadata, four original generated PNG logos, resource/manifest
  and package-signing metadata. No inherited SimsVille Content tree, game data,
  credentials or private keys are included.
- The three bundled dependency manifests identify x64 and satisfy the application's
  .NET Native Framework/Runtime 2.2 and VCLibs 140 requirements.
- No console deployment/launch has been performed, and no Xbox-success tag exists.

### Exact local artifacts

Paths below are relative to the repository root:

- Signed main package:
  `artifacts/proof/AppPackages/XboxUwpProof_0.1.0.0_x64_Test/XboxUwpProof_0.1.0.0_x64.appx`
- Public certificate: `artifacts/proof/FreeSimsXboxDevelopment.cer`
- Build identity/hash: `artifacts/proof/BUILD.txt`
- Compiler diagnostics: `artifacts/proof/build.log` and `proof.binlog`
- Convenient handoff directory: `artifacts/xbox-test-3aaafa134f31/`
- Handoff archive: `artifacts/xbox-test-3aaafa134f31.zip` (9,979,876 bytes)
- Archive SHA256:
  `4EBC0B4DC4EE6F407C2946E214B84634ADE945CF469F49F7FEA8FD92CC418017`

The handoff ZIP contains exactly seven files: main AppX, three x64 dependencies,
public .cer, BUILD.txt and TESTING.md. It excludes ARM/x86 dependencies, symbols,
private keys and original game assets. Build artifacts remain ignored; all source,
build scripts and the complete handoff procedure are preserved in Git.
To regenerate after losing local output, check out the source commit above,
install the documented toolchain and run Build-XboxProof.ps1 -Sign. A regenerated
development certificate/package has a new signature/hash; record its new BUILD.txt.

### Upstream refresh

A fresh fetch on 2026-09-21 found two newer upstream/master commits:
`c2db3bc21b69c570d461ae0b4b86cbb77e74b117` and
`2a7d9da9817b88773e56a81da6c674df25636c2a`. Their complete diff was reviewed:
IniConfig error logging, default avatar handgroup preparation, TS1HandSet metadata
and unused imports. Neither is required by the isolated proof. They were not merged.
The recovery baseline remains `a7e9dba6cd4067b4efec54ae8e0443787da22992`;
the earlier statement about matching master tips describes the original recovery time.

The next action is the hardware procedure below. Await real console results before
engine integration or any hardware success claim.

# Xbox hardware test: FreeSims UWP proof

Source commit: 3aaafa134f31b7fdb4216e8a1b12c44447323a22
Branch: xbox-uwp-port
Repository: https://github.com/Dushe22/FreeSimsUWP
Built: 2026-09-20. Package independently verified: 2026-09-21.
Configuration: Release / x64 / UWP / .NET Native / MonoGame 3.8.1.303.
Hardware status: NOT TESTED.

## Package

Install XboxUwpProof_0.1.0.0_x64.appx from this directory.
SHA256: F53942D037A329986A8229484E26345374DAE3A6D466FBAA6CD89F37C5E5B08C
Size: 3,690,891 bytes.
App identity: Dushe22.FreeSimsXboxProof, version 0.1.0.0, x64.
Publisher: CN=FreeSimsXboxDevelopment.

The three Dependencies/x64 packages are:
- Microsoft.NET.Native.Framework.2.2.appx (2.2.29512.0)
- Microsoft.NET.Native.Runtime.2.2.appx (2.2.28604.0)
- Microsoft.VCLibs.x64.14.00.appx (14.0.33519.0)

FreeSimsXboxDevelopment.cer is the PUBLIC development certificate only.
The private signing key was deleted after signing. The certificate expires
2027-03-20. This is a self-signed Developer Mode test package, not a Store package.
No original Sims game data is needed or included.

## Installation

1. Extract this ZIP on the Windows computer. Verify the main AppX hash:
   Get-FileHash .\XboxUwpProof_0.1.0.0_x64.appx -Algorithm SHA256
2. Boot the Xbox Series X/S into Developer Mode and open Dev Home.
3. In Dev Home, open Remote Access Settings, enable Xbox Device Portal, set
   a local username/password, and return to Home. Do not send those credentials.
4. Open the exact Remote Access URL displayed by the console in a browser on the
   same local network; sign in with the console's local credentials.
5. In Device Portal's Home / My games & apps area, choose Add/Deploy (the label
   depends on console OS). Choose XboxUwpProof_0.1.0.0_x64.appx as the application.
6. On the dependency step, add all THREE files from Dependencies/x64.
   Submit deployment and wait for success. Record any HRESULT verbatim.
   Do not select .cer as an app or dependency; use it only if explicitly asked
   for a certificate. Do not use an x86/ARM package.
7. Launch FreeSims Xbox Proof from Dev Home or Device Portal. If App/Game type is
   offered in Dev Home's app details, use Game and report the selected type.

Microsoft Device Portal reference:
https://learn.microsoft.com/en-us/previous-versions/windows/uwp/xbox-apps/device-portal-xbox

## Expected result and test procedure

Expect a dark screen with FREESIMS XBOX UWP PROOF, commit prefix 3AAAFA134F31,
a moving blue rectangle, controller/audio status and a white crosshair.
This proof does not contain the FreeSims main menu or simulation.

1. Leave it running for at least 60 seconds; confirm the rectangle keeps moving.
2. Use controller 1's left stick: crosshair moves and stays within the display.
3. Press A: rectangle alternates blue/orange and SELECT COUNT increments once.
4. Press B: crosshair returns to its initial position near the middle.
5. Press X: hear a short generated 440 Hz tone (about a quarter second).
   TONE SUBMITTED on screen alone is not an audio pass.
6. Press Menu: animation pauses; press it again to resume.
7. Disconnect/reconnect controller 1 and repeat movement/A/X.
8. Leave to the Xbox home screen, return and report whether graphics, input and
   audio still work. This observes lifecycle behavior; it does not verify game saves.

Other requested engine mappings (right stick, D-pad, bumpers, triggers and View)
are not implemented in this isolated proof and are not part of this test.

## Logs and results to return

In Device Portal File Explorer, select this app's local data and retrieve:
- LocalState/proof.log
- LocalState/proof.log.previous, if present

Return BUILD.txt, console model, console OS version, App/Game classification,
visible rendering result, each control result, whether sound was audible, and
resume behavior. Include any deployment/launch HRESULT or crash dump.
If the app never writes a log, return the Device Portal error instead.

## Success criteria

Deployment and launch succeed on the actual Series console; the expected scene
animates; the pointer and button actions work; the generated tone is audible;
and the app remains usable through the requested controller checks.
Report resume separately. Logs should identify the exact source commit and include
GRAPHICS READY / FIRST DRAW SUBMITTED / input and audio events. Those log events
do not replace visual/audible confirmation.

Five SharpDX Media Foundation MCG0007 warnings were emitted during native
compilation. They are documented in docs/XBOX_PORT.md. Any related runtime
exception must be returned, not treated as a successful test.

Stop here and return the hardware results. No Xbox-success tag or engine
integration milestone is claimed by this package.

## Hardware result received 2026-09-21: proof PASS

The user completed the supplied hardware test procedure and reported:
> Test procedure completed, exited with no errors, every functionality works as intended

This confirms the procedure for source
`3aaafa134f31b7fdb4216e8a1b12c44447323a22` and the previously supplied signed
`XboxUwpProof_0.1.0.0_x64.appx`, SHA256
`F53942D037A329986A8229484E26345374DAE3A6D466FBAA6CD89F37C5E5B08C`.

User-reported PASS: deployment/launch, visible animated DirectX scene, controller
pointer and A/B/Menu controls, audible X tone, controller reconnect, and leaving/
returning to the app as covered by that procedure. The user also reports no errors
on exit. This is user hardware confirmation, not a test run performed by the agent.
Console confirmed by the user: Xbox Series S. OS described as "latest"; exact build number not supplied. No diagnostic logs were supplied; do not invent log contents or claim
both console models were tested.

The annotated tag `xbox-poc-uwp` identifies the exact tested source commit.
The earlier NOT TESTED entries and immutable BUILD.txt describe the state at build/
handoff time and are superseded by this result for this specific proof package.

The five native SharpDX MCG0007 warnings did not prevent the reported proof test.
This does not verify unused Media Foundation APIs, the FreeSims engine, real game
data, neighborhood/lot loading, simulation, game saves/reload, or engine suspend safety.

Next implementation: share the existing sims.files source with a UWP library,
isolate desktop image APIs, and verify a UWP decoder plus IFF parsing using
original synthetic fixtures. Preserve the tested standalone proof.

## Shared files library milestone (2026-09-21)

- Added sims.files.uwp, compiling the existing parser source against UWP 6.2.14
  and MonoGame.Framework.WindowsUniversal 3.8.1.303. No engine parser rewrite.
- ImageLoader delegates decoding through IImageDecoder. The desktop GDI+ decoder
  retains its existing behavior. UWP uses Windows.Graphics.Imaging.BitmapDecoder
  with explicit RGBA8, straight alpha, no EXIF rotation and no color management.
  Color keys and premultiplication remain in the shared ImageLoader.
  [Microsoft API contract](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder.getpixeldataasync).
- BMP.GetBitmap remains available to the desktop debugger through a desktop-only
  partial class. Removed unused System.Drawing and System.Media imports.
- Original synthetic fixtures cover 2x2 BMP row padding/orientation/channels,
  PNG alpha/dimensions, caller stream lifetime, invalid image rejection, IFF
  reflective chunk creation/lazy BCON parsing/endianness and invalid IFF rejection.
- Desktop Release/x86 client compilation: PASS (existing upstream warnings).
  Desktop CPU compatibility tests: **6/6 PASS**. Desktop game runtime not tested.
- UWP Release/x64 files library compilation: PASS. This alone is not a native
  application build or a hardware result.
- The Xbox probe will additionally run GPU readback checks for all three BMP
  color keys and PNG alpha modes 0, 1 and -1. These checks are not yet run.
- No original game assets or signing keys added.

Commands (from repository root, Visual Studio MSBuild and NuGet installed):

~~~powershell
./scripts/Build-DesktopBaseline.ps1 -NuGetPath <path-to-nuget.exe>
./scripts/Test-FilesCompatibility.ps1
& '<VS2022>/MSBuild/Current/Bin/MSBuild.exe' sims.files.uwp/sims.files.uwp.csproj /restore /p:Configuration=Release /p:Platform=x64
~~~

Build failure resolved: CS0234 System.Media in XAFile.cs was an unused import,
classified UWP INCOMPATIBILITY. Removing the import preserved the XA parser.
Next: native Xbox probe linking this library, then user hardware verification
before integrating sims.common and the SimsVille runtime.
## Native files probe build and hardware boundary (2026-09-21)

- Added experiments/XboxFilesProbe, a distinct UWP package linking sims.files.uwp.
  The original proof sources/package identity are preserved.
- Reuses the proof's pixel font/logging and the desktop fixture source. Displays
  ten independent test outcomes, an animated bar, run count and source commit.
  A reruns; B exits. Reflection activation metadata preserves IFF constructors.
- Build-XboxProof.ps1 now accepts -Target FilesProbe (default remains Proof).
  Outputs are isolated under artifacts/files-probe. Signed handoffs require a
  clean tree and the actual HEAD commit; BUILD.txt records target/source state.
- Release/x64 C# + .NET Native + unsigned AppX build: PASS. The same five
  SharpDX Media Foundation MCG0007 warnings remain, with no new build errors.
- Xbox files-probe runtime: NOT TESTED. Do not infer success from the earlier
  hardware proof or the desktop decoder tests.
- [Exact hardware test procedure](../experiments/XboxFilesProbe/TESTING.md).
  The signed package's BUILD.txt will identify the exact source commit/hash.
- Next after successful hardware results: port the sims.common platform boundary
  and storage paths, then integrate SimsVille's runtime incrementally. Real game
  data, UI/neighborhood/lot loading, simulation and saves are still unverified.
## Signed files probe handoff (2026-09-21)

**Xbox test required: YES. Stop at this hardware boundary.**

- Exact package source: 173ddaa0855939406413d4acea15c6798e03528a.
- Source commit message: uwp: add native files compatibility probe for Xbox.
- Branch: xbox-uwp-port; source pushed to Dushe22/FreeSimsUWP.
- Clean committed build command:
  ./scripts/Build-XboxProof.ps1 -Target FilesProbe -Sign.
- AppX:
  artifacts/files-probe/AppPackages/XboxFilesProbe_0.1.0.0_x64_Test/XboxFilesProbe_0.1.0.0_x64.appx.
- AppX size: 4,117,837 bytes.
- AppX SHA256: BF4B487B31A331A16219197DE619C1BC9D085B2508236CE6291F80C57D33CCE1.
- Convenience handoff: artifacts/xbox-files-test-173ddaa08559.zip.
- ZIP SHA256: 5C216264B7DA14015D423CD568AB7F98C76A7FCDD3A2413A077626A037A4615F.
- Contents: application AppX, three x64 framework dependencies, public .cer,
  BUILD.txt, TESTING.md. Exactly seven files; no private key or Sims assets.
- Public certificate thumbprint: C629DE5C4EF57D5C1F6D730F44596773374A5AEC.
  Expires 2027-03-21. Signing key removed from CurrentUser/My after signing.
- Verified manifest identity Dushe22.FreeSimsXboxFilesProbe 0.1.0.0 x64,
  native x64 executable, all 191 block-map SHA256 hashes, CMS signature and
  matching public certificate. This validates package structure/integrity;
  it is not a claim of a trusted public certificate chain or hardware execution.
- Dependency versions provided: .NET Native Framework 2.2.29512.0,
  .NET Native Runtime 2.2.28604.0, VCLibs 14.0.33519.0.
- Original tested proof package SHA256 remains
  F53942D037A329986A8229484E26345374DAE3A6D466FBAA6CD89F37C5E5B08C;
  its source files are unchanged in this cycle.

Deploy the main AppX and three x64 dependencies through Xbox Device Portal.
Launch **FreeSims Files Probe**. Require **10/10 PASS** on first launch and
after A reruns, home/return and controller reconnect. Wait 60 seconds with the
blue bar moving; B exits, then relaunch and require 10/10 again.
[Full installation, controls, log path and success criteria](../experiments/XboxFilesProbe/TESTING.md).

Return this app's LocalState/proof.log (and proof.log.previous if present),
displayed commit, console model/OS and any failure. Original proof hardware
PASS remains scoped to the user's Series S; this new package is NOT TESTED.

Completed: shared UWP parser library, platform image decoder, six passing
desktop CPU cases, ten-case native probe, signed package, integrity audit.
Desktop Release/x86: PASS compilation; game runtime NOT TESTED.
UWP files probe Release/x64/.NET Native: PASS compilation.
Packaging/signing: PASS. New Xbox runtime: NOT TESTED.
GitHub Actions: still blocked by previously observed account billing restriction;
the existing workflow covers the standalone proof/desktop baseline, not this
new probe. Local commands above are the authoritative validation for this cycle.

Current blocker: user hardware verification of the new decoder, reflective
parser and GPU readback cases. After PASS, proceed to sims.common platform
services/storage and incremental SimsVille integration.

Files changed/created in this cycle (relative to daf573d):
- .gitignore
- docs/XBOX_PORT.md
- experiments/XboxFilesProbe/FilesProbeGame.cs
- experiments/XboxFilesProbe/Package.appxmanifest
- experiments/XboxFilesProbe/Program.cs
- experiments/XboxFilesProbe/Properties/AssemblyInfo.cs
- experiments/XboxFilesProbe/Properties/Default.rd.xml
- experiments/XboxFilesProbe/TESTING.md
- experiments/XboxFilesProbe/XboxFilesProbe.csproj
- scripts/Build-XboxProof.ps1
- scripts/Test-FilesCompatibility.ps1
- sims.files.uwp/Platform/PlatformImageDecoder.cs
- sims.files.uwp/sims.files.uwp.csproj
- sims.files/ImageLoader.cs
- sims.files/Platform/Desktop/BMP.Desktop.cs
- sims.files/Platform/Desktop/PlatformImageDecoder.cs
- sims.files/Platform/IImageDecoder.cs
- sims.files/XA/XAFile.cs
- sims.files/formats/iff/chunks/BMP.cs
- sims.files/formats/iff/chunks/SPR2FrameEncoder.cs
- sims.files/sims.files.csproj
- tests/FilesCompatibility/FilesCompatibility.csproj
- tests/FilesCompatibility/FilesCompatibilityTests.cs
- tests/FilesCompatibility/Program.cs

## Files-probe hardware result and resize defect (2026-09-21)

The user reports all checks passed except a resizing discrepancy after going to
Xbox Home and reopening the app. Both provided photos show source 173DDAA08559,
10/10 PASS and RUN 1. The second shows smaller text/layout occupying less of the
display. User hardware: Xbox Series S; OS previously described as "latest",
without an exact build number.

Evidence: IMG_20260921_200912.jpg (initial) and
rn_image_picker_lib_temp_fee7cc6d-d357-414f-9038-e8e01fe8cf45.jpg (after Home).
Photos were inspected in the conversation, not added to GitHub. No log was
provided. RUN 1 in both photos does not establish whether the process resumed or
restarted; the exact viewport/back-buffer dimensions were not visible.

Result: the ten synthetic parser/decoder/GPU checks are hardware PASS on this
console. Overall presentation has a known Home/return defect. Earlier NOT TESTED
entries are historical handoff records, superseded within this limited scope.
This is not proof of real game data loading or a complete engine port.

Source finding: FilesProbeGame used hard-coded 1280x720 layout coordinates with
an identity SpriteBatch transform. PreferredBackBufferWidth/Height are not a
guarantee that every later viewport retains those dimensions. A larger viewport
would make the same pixel coordinates occupy less of the screen, consistent with
the photos. The exact platform resolution transition remains an inference until
the new DISPLAY logs are returned.

Fix for version 0.1.1.0:
- Keep the logical 1280x720 layout and uniformly fit/center it in the current
  viewport every draw, preserving aspect ratio. Skip zero-sized viewports.
- Do not force a back-buffer reset inside activation/resize callbacks; derive
  scale at draw time even if framework resize happens after those callbacks.
- Log viewport, back-buffer and client bounds plus scale/offset when dimensions
  change or the app activates/device resets. Add an on-screen viewport readout.
- Increment package/assembly version to 0.1.1.0 under the existing app identity.
- Preserve all ten existing hardware cases and the original standalone proof.

Regression checks: original six desktop CPU cases PASS; resize sequence
720p -> 1080p -> 4K -> 720p, 4:3/ultrawide aspect fitting and zero-size recovery
PASS. These validate transform math, not Xbox lifecycle behavior.
Reference: [MonoGame viewport-based sprite scaling](https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Scale_Sprites_Matrix.html).

Current boundary: build/package the focused correction, then request five
Home/return cycles and photos/logs. Do not claim the resize fix works on Xbox or
advance to sims.common integration until this retest is complete.

Local validation for the resize correction: UWP Release/x64 C# compilation,
.NET Native and unsigned AppX generation PASS. The same five existing SharpDX
Media Foundation MCG0007 warnings remain. No shared engine/decoder source was
changed; desktop client build remains the previously verified baseline, while
the desktop fixture executable was rebuilt and passed this cycle.

## Signed resize retest handoff (2026-09-22)

Build completed successfully before the session interruption. Resumed work
verified the existing signed output; no rebuild or source change was needed.

- Exact package source: 080a1332078ad1604aa88db881bd8932de4e1d2f.
- Commit: fix: keep Xbox files probe layout stable across viewport changes.
- Identity: Dushe22.FreeSimsXboxFilesProbe, version 0.1.1.0, x64.
- Package:
  artifacts/files-probe-0.1.1.0/AppPackages/XboxFilesProbe_0.1.1.0_x64_Test/XboxFilesProbe_0.1.1.0_x64.appx.
- AppX size: 4,121,197 bytes.
- AppX SHA256: 7C3C1B580D48803C835B842DA182306797DC9760034653906D2BBA3971F138B0.
- Test ZIP: artifacts/xbox-files-resize-080a1332078a.zip.
- ZIP SHA256: 3BF7F012337B17A7F16EC4DC958E391DBBC665731B79615931CB29BFC209E1D1.
- ZIP contains exactly seven files: AppX, three x64 framework dependencies,
  public certificate, BUILD.txt and TESTING.md. No Sims assets or private key.
- Public certificate: 70FDB65AF2231B79203A8821B75FBEC0B61617DC,
  expires 2027-03-21. Private signing key removed from CurrentUser/My.
- Verified x64 PE/manifest identity/version, all 191 block-map hashes, CMS
  signature and matching public certificate. No public-chain trust claim.

Build from the exact clean source commit (Git on PATH):

~~~powershell
./scripts/Build-XboxProof.ps1 -Target FilesProbe -OutputDirectory artifacts/files-probe-0.1.1.0 -Sign
~~~

Install/update the main AppX through Xbox Device Portal and supply the three
dependencies from Dependencies/x64 as needed. Launch FreeSims Files Probe.
[Full installation and focused resize test](../experiments/XboxFilesProbe/TESTING.md).

Require the same apparent UI size/position on initial launch and after five
Home/return cycles (including 30 seconds at Home), with 10/10 PASS after each
A rerun. Viewport numbers may change; layout scale must remain visually stable.
Also test B exit/relaunch and controller reconnect. Return before/after photos
and this app's LocalState/proof.log (plus .previous when present), particularly
the DISPLAY records with viewport/back-buffer dimensions, scale and offsets.

Completed: recorded ten-case Series S hardware PASS with the observed resize
defect; implemented dynamic viewport layout and diagnostics; added CPU resize
regression checks; produced and audited the signed update.
Desktop client: unchanged, previous Release/x86 build PASS; not rerun this cycle.
Desktop CPU fixtures: 6/6 PASS plus resize/aspect/zero-size checks PASS.
UWP Release/x64/.NET Native and packaging/signing: PASS.
New resize correction on Xbox: NOT TESTED. This is the current blocker.
Five existing SharpDX MCG0007 warnings remain. GitHub Actions remains previously
billing-blocked; this handoff uses local build evidence.

Source and this report are pushed on xbox-uwp-port in Dushe22/FreeSimsUWP.
Recovered proof and shared parser/decoder work are retained. After a successful
resize retest, proceed to sims.common platform/storage integration. Stop here
for the user's hardware result.

Files changed in this correction:
- docs/XBOX_PORT.md
- experiments/XboxFilesProbe/FilesProbeGame.cs
- experiments/XboxFilesProbe/Package.appxmanifest
- experiments/XboxFilesProbe/ProbeLayout.cs
- experiments/XboxFilesProbe/Properties/AssemblyInfo.cs
- experiments/XboxFilesProbe/TESTING.md
- experiments/XboxFilesProbe/XboxFilesProbe.csproj
- tests/FilesCompatibility/FilesCompatibility.csproj
- tests/FilesCompatibility/ProbeLayoutTests.cs
- tests/FilesCompatibility/Program.cs

## Resize retest hardware PASS received 2026-09-22

User: "All tests passed as intended, you can proceed".
This confirms the supplied 0.1.1.0 procedure for source
080a1332078ad1604aa88db881bd8932de4e1d2f and package SHA256
7C3C1B580D48803C835B842DA182306797DC9760034653906D2BBA3971F138B0.

The reported pass covers five Home/return cycles with stable apparent layout,
10/10 fixture checks, controller reconnect and exit/relaunch on the previously
identified Xbox Series S. No new logs or exact OS build supplied. We do not
infer specific viewport dimensions or process-resume behavior.
The known presentation defect is resolved according to the user's retest.
Earlier NOT TESTED entries remain historical build/handoff records.

Next authorized work: share sims.common's actual compile inputs with UWP,
isolate desktop-only APIs as needed, and establish distinct packaged-content,
provisioned-game-data and writable-user-data paths. Do not claim engine/gameplay
or save compatibility merely from the probe pass.
## sims.common UWP and storage foundation (2026-09-22)

The actual desktop compile list now lives in sims.common/SharedSources.projitems
and is imported by both desktop and sims.common.uwp. This excludes existing
uncompiled WinForms utilities and AssemblyUtils rather than accidentally adding
them via a recursive source glob. The UWP library references sims.files.uwp,
pins UWP 6.2.14 and MonoGame WindowsUniversal 3.8.1.303, and does not reference
OpenTK, WinForms or System.Drawing.

Small source compatibility fixes:
- Log.cs delegates event output and default/fallback directories to a platform
  adapter. Desktop retains Windows EventLog and its original paths. UWP stores
  logs under LocalState/UserData/Logs; its event sink rotates at 1 MiB.
- Removed the unused InputManager.MapVirtualKey user32 import (no callers).
- Replaced removed MonoGame Color.TransparentBlack with Color.Transparent
  (transparent zero RGBA) in PPXDepthEngine.
- Reproduced IndexOutOfRangeException in IniConfig.Load with a blank-line fixture,
  then added an empty-line guard. IniConfig.Save continues to serialize
  DefaultValues; the probe explicitly synchronizes that dictionary when saving.

Storage foundation:
- IGamePaths/GamePaths define disjoint absolute ContentRoot, GameDataRoot and
  UserDataRoot, resolve relative paths and reject lexical root escapes/drive or
  stream names. These checks do not constitute a filesystem sandbox.
- UwpGameStorage maps those to InstalledLocation/Content, LocalState/GameData,
  and LocalState/UserData. Only the two local roots are created.
- ApplyToEnvironment maps the existing FSOEnvironment fields, selecting DX
  packaged content. It does not change process cwd or desktop defaults.
- This is a host boundary, not a completed audit/migration of all SimsVille
  relative paths and writes. Real game data is still not loaded.

Validation:
- Desktop Release/x86 client compilation PASS (existing warnings).
- Test-CommonCompatibility.ps1: 8/8 PASS covering content ID, curve interpolation,
  shared text input, path/environment mapping and rejection, settings creation/
  write/reload/blank lines, and file logging.
- Existing files CPU fixtures: 6/6 PASS; viewport regression checks PASS.
- sims.common.uwp Release/x64 library compilation PASS. Full native application
  execution still needs the next hardware probe.

Next: a distinct common/storage probe using the shared GameScreen layer loop,
native INI reflection, render-target clearing, package content reads, UWP logging,
persistent synthetic state and an externally provisioned text marker. This is not
a game save implementation and does not require original Sims assets.

## Common/storage probe implementation (2026-09-22)

Added experiments/XboxCommonProbe, identity Dushe22.FreeSimsXboxCommonProbe
0.1.0.0, preserving both hardware-tested earlier probe apps. It links the shared
common/files libraries and uses the existing GameScreen/IGraphicsLayer update
and draw loop. The proven viewport-fit helper and display diagnostics are reused.

Eight shared CPU cases run under .NET Native, plus four UWP/GPU cases:
packaged text read, app-local event sink, PPX transparent render-target clear and
GameScreen callback ordering/counts. X persists a synthetic counter via the
existing IniConfig contract and verifies readback; relaunch tests durable state.
A also reads a user-uploaded original text marker from LocalState/GameData.
The external marker is not included in the AppX and is never generated there
by the app. The packaged content fixture is a different, original text file.

Paths: InstalledLocation/Content (packaged), LocalState/GameData (provisioned),
LocalState/UserData (writable). Real Sims data and saves are not used. The app
logs exact resolved roots and persistent state identity/value. No package writes
or process-current-directory changes are attempted.

Build selector: Build-XboxProof.ps1 -Target CommonProbe; outputs are isolated
under artifacts/common-probe. Existing selectors still target their prior apps.
[Complete hardware procedure](../experiments/XboxCommonProbe/TESTING.md).

Microsoft references:
[App file access](https://learn.microsoft.com/en-us/windows/apps/develop/files/file-access-permissions),
[Device Portal uploads](https://blogs.windows.com/windowsdeveloper/2016/06/08/using-the-app-file-explorer-to-see-your-app-data/).

Next boundary: verify the signed common/storage probe on the user's Series S.
After PASS, begin integrating the SimsVille runtime and audit its real content
provenance, filesystem writes and platform startup. The synthetic persistence
test must not be described as game save/reload support.

Common probe local validation: Release/x64 C# compilation, .NET Native and
unsigned AppX generation PASS. The only native interop warnings are the same
five SharpDX Media Foundation MCG0007 warnings seen in prior probes.
Final desktop client Release/x86 rebuild after the INI fix also PASS.
Common probe hardware execution, retained counter and provisioning: NOT TESTED.

## Signed common/storage handoff (2026-09-22)

**Hardware test required: YES. Stop here for the user's results.**

- Exact package source: 27f318c55deb2dadff92ba827f63ba9b572fd1d5.
- Source commit: uwp: add common library and persistent storage hardware probe.
- Branch: xbox-uwp-port in Dushe22/FreeSimsUWP; pushed.
- Package identity/version/architecture: Dushe22.FreeSimsXboxCommonProbe /
  0.1.0.0 / x64. Display name: FreeSims Common Probe.
- Main AppX:
  artifacts/common-probe/AppPackages/XboxCommonProbe_0.1.0.0_x64_Test/XboxCommonProbe_0.1.0.0_x64.appx.
- AppX size: 4,193,122 bytes.
- AppX SHA256: 0D9EF31685B137C1C2840E810EE4AA6B79AC5B6A6AA26D3DF843F534C8B2C077.
- ZIP: artifacts/xbox-common-test-27f318c55deb.zip.
- ZIP SHA256: 8AA1F2FE15E620FE5C478325F2DC93CEC66ADCDDA126BD7454DB790BB7D1EA4F.
- Exactly eight files: main AppX, three x64 dependencies, public certificate,
  BUILD.txt, TESTING.md and the separate original game-data-probe.txt.
- Certificate thumbprint: E30ABFF90EE3A14DAD4AD3D151BEA8CA8448F1AC,
  expires 2027-03-22. Private key removed from CurrentUser/My after signing.
- Verified identity/version, x64 executable and native DLL, all 194 block-map
  hashes, CMS signature and matching public certificate. No public-chain trust
  claim. Packaged content contains only the original package fixture and generated
  logos; the external marker/GameData/UserData are absent from the AppX.
- BUILD.txt confirms the actual source commit, no uncommitted changes, Release/
  x64/.NET Native, pinned MonoGame 3.8.1.303 and signed package/hash.

Build from the exact clean source:

~~~powershell
./scripts/Build-XboxProof.ps1 -Target CommonProbe -Sign
~~~

Install main AppX plus Dependencies/x64 through Xbox Device Portal. Launch
FreeSims Common Probe; require 12/12 PASS and an increasing layer frame counter.
Use X to increment synthetic persistent state three times, then verify the same
value after B exit/relaunch and Home/return. A reruns without incrementing it.

Upload the separate game-data-probe.txt into this app's LocalState/GameData
through File explorer, then press A and require GAME DATA MARKER PASS.
Before upload, WAITING FOR GAME DATA MARKER is expected. Require marker PASS and
retained counter after relaunch. No Sims installation files are needed.
[Exact install/controls/procedure/logs/success criteria](../experiments/XboxCommonProbe/TESTING.md).

Logs: this app's LocalState/proof.log (+ previous if present),
UserData/Logs/events.log for logger failures, UserData/common-probe.ini for
persistence failures. Return final screen, displayed commit and counter values.

Completed this cycle: user-confirmed Series S resize PASS recorded; shared
sims.common UWP library; separate content/game/user paths; platform log adapters;
blank-line settings fix; CPU regression cases; native common/storage probe;
signed package and integrity audit. Earlier proof/files/resize work preserved.

Build status:
- Desktop Release/x86 client: PASS compilation after final shared-source changes.
- Common CPU checks: 8/8 PASS. Files CPU checks: 6/6 PASS. Resize checks: PASS.
- Common probe Release/x64/.NET Native: PASS compilation.
- Packaging/signing: PASS.
- New common/storage probe hardware: NOT TESTED.

Five prior SharpDX MCG0007 warnings remain. Previously observed GitHub Actions
billing failures are not used as build evidence; this cycle's verification is
local. The existing workflow does not build the common probe.

Current blocker: user hardware verification of the twelve native checks,
persistent counter and external marker. Next after PASS: integrate SimsVille's
runtime/startup and audit content and filesystem assumptions. In particular,
IniConfig still serializes DefaultValues and does not populate property defaults
on first-file creation; the probe explicitly stores/reloads settings. Game
settings integration needs that behavior reviewed. No game saves or real Sims
data loading are claimed by this milestone.

Files changed/created in this cycle (relative to 0548071):
- .gitignore
- docs/XBOX_PORT.md
- experiments/XboxCommonProbe/CommonNativeTests.cs
- experiments/XboxCommonProbe/CommonProbeGame.cs
- experiments/XboxCommonProbe/Content/package-probe.txt
- experiments/XboxCommonProbe/Fixtures/game-data-probe.txt
- experiments/XboxCommonProbe/Package.appxmanifest
- experiments/XboxCommonProbe/ProbeLayer.cs
- experiments/XboxCommonProbe/Program.cs
- experiments/XboxCommonProbe/Properties/AssemblyInfo.cs
- experiments/XboxCommonProbe/Properties/Default.rd.xml
- experiments/XboxCommonProbe/TESTING.md
- experiments/XboxCommonProbe/XboxCommonProbe.csproj
- scripts/Build-XboxProof.ps1
- scripts/Test-CommonCompatibility.ps1
- sims.common.uwp/Platform/PlatformLog.cs
- sims.common.uwp/Platform/UwpGameStorage.cs
- sims.common.uwp/sims.common.uwp.csproj
- sims.common/IniConfig.cs
- sims.common/Log.cs
- sims.common/Platform/Desktop/PlatformLog.cs
- sims.common/Platform/GamePaths.cs
- sims.common/Platform/IGamePaths.cs
- sims.common/SharedSources.projitems
- sims.common/rendering/framework/io/InputManager.cs
- sims.common/sims.common.csproj
- sims.common/utils/PPXDepthEngine.cs
- tests/CommonCompatibility/CommonCompatibility.csproj
- tests/CommonCompatibility/CommonCompatibilityTests.cs
- tests/CommonCompatibility/ProbeSettings.cs
- tests/CommonCompatibility/Program.cs

## Common/storage Series S hardware result (2026-09-22)

User supplied proof.log and IMG_20260922_141554.jpg from the previously identified
Xbox Series S (OS described as latest; exact OS build remains unspecified).
Tested source: 27f318c55deb2dadff92ba827f63ba9b572fd1d5, Release/x64,
Common Probe 0.1.0.0. This result supersedes the preceding pending-hardware status.

Evidence:
- Log SHA256: 1BD6B5D5C228F4D5F12FE5B87A921B8CBB298044D166FE4E3F4C6A5B5E38D744.
- Three START COMMON PROBE records at 17:11:55, 17:15:31 and 17:16:20 UTC.
- Twenty RESULT 12/12 PASS records (8 + 4 + 8); no recorded failures.
- First launch creates counter 0 and stores values 1 through 5. Both later
  launches load value 5 with the same generated state marker.
- External marker initially absent, then GAME DATA MARKER PASS on every test
  from the second launch onward (12 successful reads).
- Packaged content, app-local event logging, shared screen layer callbacks,
  native reflection/INI and GPU render-target readback all report PASS.
- Five DISPLAY records consistently show viewport/backbuffer 1280x720, client
  1728x972, UI 1280x720, scale 1, offset 0,0, including after reactivation at
  19:48:37 UTC. A suspension is logged at 17:12:44; a later fresh start follows.
  No same-process RESUMING event or explicit B-exit event is present, so those
  exact lifecycle paths are not inferred from the log.
- Photo shows 12/12 PASS, persisted value 5, GAME DATA MARKER PASS, engine layer
  frames 1363 and viewport/UI 1280x720.

The common/storage milestone is hardware verified for the evidenced paths.
This is synthetic settings persistence, not Sims game saves or neighborhood
loading. Raw user files remain outside Git; only this result/hash is published.
Earlier files/resize/proof implementations are retained. No build changes in
this checkpoint; previous desktop/native/package build results remain valid.
Next: audit and integrate SimsVille runtime sources, platform startup, settings,
content provenance and read/write paths into a dedicated UWP target.
## SimsVille startup audit and settings repair (2026-09-22)

After the common/storage hardware pass, began auditing the actual client startup.
Found and reproduced an EXISTING UPSTREAM ERROR in IniConfig, used by SimsVille's
GlobalSettings. Four new cases link the actual GlobalSettings.cs into the desktop
compatibility runner. Before the fix all four fail while the eight common tests
pass; artifacts/common-settings-before.log preserves the local reproduction.

Implemented in the shared IniConfig source:
- Apply declared defaults before file overrides, including first-file creation.
  A new GlobalSettings now has 1024x768, windowed=true, language=1, FX volume=10,
  and skip-intro=true immediately. Partial/invalid files keep valid defaults.
- Retain the declared fallback dictionary across loads instead of replacing it
  with loaded values. Reloading a missing/invalid key cannot reuse a stale value.
- Save the current properties named in the settings schema, using invariant
  conversions; preserve the existing INI format. Changes to graphics size,
  windowed mode, language, paths and UInt64 values survive a new instance.
- Resolve serialized values before opening the destination; report I/O failures
  instead of silently claiming success. Hosts must handle save errors. This is
  not an atomic-write or crash-recovery implementation, and does not add range
  validation for otherwise parseable values.
- The synthetic probe now stores its properties normally; removed its dictionary
  synchronization workaround. No controller/layout/provisioning behavior changed.

The four new client settings cases run only in the desktop compatibility runner;
the existing Common Probe's twelve native cases and manifest remain unchanged.
The user's tested 27f318c package remains the verified hardware checkpoint. These
new settings changes are not claimed to have executed on Xbox.

### BLOCKER: existing SimsVille startup is not an offline TS1 host

EVIDENCE (source-level audit; no full SimsVille UWP build claimed):
- Program.InitWithArguments requires FindTheSimsOnline before starting, dynamically
  selects desktop MonoGame and uses registry/assembly loading paths.
- Game.cs initializes FSO.Content.Content with GlobalSettings.StartupPath and
  processes NetworkFacade.Client packets each frame.
- ContentManager/Content.cs constructs Tuning from basePath/tuning.dat and eagerly
  initializes the inherited TSO avatar/UI/audio providers. UIGraphicsProvider
  expects FAR3 uigraphics/*.dat; GameContent/ContentManager.cs separately reads
  relative Content/animtable.xml and Content/uigraphics.xml.
- Network/NetworkFacade.cs constructs the GonzoNet client and registers login/city
  handlers in its static initializer. UI/GameController still contains TSO flow.
- UI/Framework/Parser/UIScriptParser.cs references the existing GOLDEngine binary
  and reads relative Content/UIScript.egt.
- ContentManager/other/TS1NeighbourProvider.cs reads original UserData/Neighborhood.iff
  directly under SimsCompleteDir. Writable neighborhood separation is unfinished.
- Game.LoadContent needs Fonts/SimsFont, Fonts/SimsFontBig and Effects/Vitaboy.
  Existing content provenance/native compatibility has not been cleared.
- CoreGameScreen directly creates a WinForms Simantics debug window. UWP cannot
  include the desktop debug UI or use desktop Registry/Assembly.LoadFrom startup.

CAUSE: the surviving client combines TSO startup/content/network assumptions with
TS1 simulation and neighborhood code. Pointing both roots at a Sims 1 installation
would not satisfy those concrete dependencies. The external marker test proves
file access only; it does not validate a real game installation or remove them.

OPTIONS: port the entire legacy online dependency closure, or introduce a small
explicit offline UWP host while retaining shared rendering/UI/simulation sources.
RECOMMENDATION: the latter, preserving desktop startup. Next implementation should
build the explicit runtime source closure, isolate debug/network entry points,
resolve content ownership and map each actual read/write before starting TSOGame.
Do not distribute legacy content or request/upload game assets to GitHub as a shortcut.

Files changed this implementation checkpoint:
- sims.common/IniConfig.cs
- tests/CommonCompatibility/ClientSettingsTests.cs (new)
- tests/CommonCompatibility/CommonCompatibility.csproj
- tests/CommonCompatibility/Program.cs
- tests/CommonCompatibility/ProbeSettings.cs
- docs/XBOX_PORT.md

Validation of settings implementation:
- scripts/Test-CommonCompatibility.ps1: 12/12 PASS (8 existing common + 4 actual
  client settings cases), after reproducing 8/12 before the fix.
- scripts/Build-DesktopBaseline.ps1 -NuGetPath <local nuget.exe>: desktop client
  Release/x86 compilation PASS; libraries AnyCPU. Desktop game runtime not tested.
- scripts/Build-XboxProof.ps1 -Target CommonProbe -OutputDirectory
  artifacts/common-settings-validation: Release/x64/.NET Native compilation and
  unsigned AppX generation PASS. Only the five existing SharpDX Media Foundation
  MCG0007 warnings; no new native compatibility warning category.
- New package BUILD.txt reports uncommitted changes and unsigned validation only.
  This artifact is not a hardware handoff. No new console execution claimed.
- Original signed 27f318c Common Probe AppX hash was rechecked unchanged:
  0D9EF31685B137C1C2840E810EE4AA6B79AC5B6A6AA26D3DF843F534C8B2C077.
- Existing files CPU 6/6 and viewport checks were not rerun for this settings-only
  change; previous successful results remain the baseline.
- Reviewed source diff and explicit file list; no assets, raw user logs, private
  keys or credentials added. Earlier hardware-tested work remains recoverable.

Current checkpoint: settings fix and startup audit on xbox-uwp-port, committed
and pushed as "fix: initialize and persist actual client settings". No new Xbox
procedure is requested in this cycle. The next hardware handoff must include
native execution of these settings changes alongside the next client milestone.
Next concrete work: build and isolate the offline SimsVille runtime dependency
closure, with desktop behavior retained and a content provenance audit before
packaging any existing content. Full client UWP startup/UI remains unimplemented.

## SimsVille UWP runtime library (2026-09-22)

Added SimsVille.uwp/SimsVille.uwp.csproj, Release/x64 UAP 10.0.19041.0 with
minimum 10.0.16299.0, UWP package 6.2.14 and MonoGame 3.8.1.303. Assembly:
SimsVille.Uwp. Imports 450 explicit source inputs from SimsVille/SharedRuntime.projitems;
the desktop project now imports the same list. Desktop Program/GameStartProxy,
registry locators, assembly swapping, assembly metadata and WinForms debug sources
remain solely in the desktop project. No Content tree is imported or packaged.

The first compilation identified exactly two source compatibility errors:
1. FSO.Debug missing after excluding WinForms sources. Extracted the original
   debugger action into Platform/Desktop/ClientPlatform.cs; the shared optional
   action is absent on UWP, and CoreGameScreen hides that debugger button.
2. Color.TransparentBlack removed in the pinned MonoGame. UIHouseMode now uses
   the equivalent Color.Transparent, as already done for shared PPXDepthEngine.
Removed an unused System.Drawing import from the compiled legacy ContentManager.

Validation: SimsVille.uwp Release/x64 C# compilation PASS; desktop client
Release/x86 compilation PASS after the shared-source import change. Local logs:
artifacts/runtime-first-build.log, runtime-second-build.log, runtime-library-build.log,
runtime-desktop-build.log. Native linking and execution are the next checks.

Existing GOLDEngine and GonzoNet managed binaries are referenced because current
UI parsing and VM packet signatures require them. GonzoNet references legacy
System.ServiceModel, so C# compilation alone does not prove native compatibility.
No standalone SimsNet/debug/parser project is added. Networking, original content
initialization and TSOGame are not yet started by a UWP host. This checkpoint is a
runtime library, not a playable app or verified offline simulation. Next: a native
host exercising actual client UI/settings without loading Sims/TSO assets or
starting the legacy login/server paths; report any native dependency failures.

## Offline client UI host and controller foundation (2026-09-22)

Added XboxRuntimeProbe as a separate app referencing SimsVille.Uwp, Common.Uwp and
Simslib.Uwp. It does not construct TSOGame, start NetworkFacade/VMServerDriver,
initialize legacy content/audio, or package existing SimsVille Content. This is
an initial offline client host, not a claim that the full simulator is offline.

Small shared UI adaptations discovered while constructing the host:
- UIButton's standard game texture is now loaded only for its default constructor.
  Supplying an explicit texture no longer triggers an unrelated game-data lookup.
  Default desktop buttons retain their existing asset path.
- UILayer can update before Content is initialized. Pending resource modifications
  still run when a content instance exists.
- UIButton click sound is conditional on an initialized HITVM; clicks can work in
  the startup UI before game audio exists.
- Added MouseCancel at the end of the existing event enum, leaving previous values
  unchanged, and InputManager.CancelMouseCapture. UIButton clears its pressed state
  without calling OnButtonClick. The host cancels on focus loss/disconnection/B.
  Other existing widgets have not been audited for all cancellation behaviors.

ControllerPointer lives in the shared runtime list (now 451 files), preserving
mouse-style UI. Left stick uses radial dead zone 0.2, speed 600 logical px/s,
clamped UI bounds and elapsed time capped at 0.1 seconds. Both settings are API
adjustable. A held across focus loss/reconnection is suppressed until released.
B cancels, Menu exits, Y reruns. Full camera/secondary-button mappings remain later.

The host creates a fixed 1280x720 UI render target and presents it with the tested
aspect-fit layout transform. The real UILayer/UIScreen/UIButton perform drawing,
caption layout and click routing. A generated SpriteFont rasterizes the prior
probe's original pixel glyphs; a four-state button texture is generated in memory.
No original game font, UI image or content pipeline output is included.

Four actual GlobalSettings regression cases and two ControllerPointer cases are
shared between desktop CPU and native host tests. Four native checks exercise a
scripted shared button click, cancellation, GPU UI pixel readback and absence of
legacy content/audio/debugger initialization. Total expected on Xbox: 10/10.
Human button clicks toggle GlobalSettings.ShowHints, save and read it back from
LocalState/UserData/config.ini; menu/relaunch tests real client setting persistence.
Synthetic automated fixtures have a separate directory and never replace config.ini.

Desktop checks: 14/14 PASS; desktop client Release/x86 rebuild PASS after UI/input
changes. First full native build succeeded but with 10 additional GonzoNet warnings:
ILT0005 missing System.Configuration types, ILT0012 configuration attributes and
ILT0003 methods that would always throw. These identify GonzoNet.GlobalSettings,
not FSO.Client.GlobalSettings. GonzoNet networking is still not UWP-compatible.
Five earlier SharpDX MCG0007 warnings also appeared. No warnings were suppressed.

The initial host template retained every application assembly for reflection.
The host's directives are being scoped to its own types and the actual reflected
FSO.Client.GlobalSettings properties, without forcing unused legacy network types.
This is not a fix for GonzoNet; any future host that invokes its unsupported paths
must isolate or replace them. Full-world/UI script reflection needs its own audit.
Microsoft reference: [runtime directives](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/runtime-directives-rd-xml-configuration-file-reference).

Build selector: scripts/Build-XboxProof.ps1 -Target RuntimeProbe [-Sign].
[Runtime Probe test procedure](../experiments/XboxRuntimeProbe/TESTING.md).
Next boundary is a clean signed native package and the user's Series S results.

Scoped native validation completed successfully: Release/x64/.NET Native AppX
build PASS, with zero ILT or MCG warning records in artifacts/runtime-native-scoped.log.
The prior 15 warning records are retained in runtime-native-second.log as evidence
of the broad-reflection experiment, not hidden or suppressed. The final host
retains reflection for its own assembly and FSO.Client.GlobalSettings only.
This proves compilation of the exercised host closure, not native viability of
all 451 shared runtime files. No live Xbox checks have run for this new app.

Checkpoint validation: desktop Release/x86 PASS, shared/runtime UWP x64 PASS,
14/14 desktop CPU checks PASS, native Runtime Probe unsigned packaging PASS.
The existing signed Common Probe remains unchanged. Next action: sign the clean
committed Runtime Probe source, audit the package, and stop for the hardware test.

## Signed Runtime Probe handoff (2026-09-22)

**Hardware test required: YES. Stop at this checkpoint for the user's results.**

- Package source: 0dc91451b81bcaa1c81116acb87d3f7fd037b349.
- Source commit: uwp: exercise shared client UI and controller input in native host.
- Branch xbox-uwp-port in Dushe22/FreeSimsUWP; source pushed before signing.
- Identity Dushe22.FreeSimsXboxRuntimeProbe, 0.1.0.0, x64.
- Display name: FreeSims Runtime Probe. Separate from all earlier probe apps.
- Main AppX: artifacts/runtime-probe/AppPackages/XboxRuntimeProbe_0.1.0.0_x64_Test/XboxRuntimeProbe_0.1.0.0_x64.appx.
- AppX size: 5,273,592 bytes.
- AppX SHA256: ED336718F27554F654C53743A73D256275DFC2626500CD4A743C79FD97752504.
- Test ZIP: artifacts/xbox-runtime-test-0dc91451b81b.zip, 11,568,049 bytes.
- ZIP SHA256: EC2201D8634FB24747B202D55C2DCFA7071C171730980C59A557443CFFB1CFE5.
- Exactly seven files: AppX, three Dependencies/x64 AppXs, public .cer, BUILD.txt,
  TESTING.md. No copyrighted game assets, private keys, credentials or user logs.
- Certificate thumbprint C47B01CE729B2AEBBB0BD068D6C36C252E5EFE1B,
  expires 2027-03-22. No private key in .cer; temporary signer removed from My store.
- Verified manifest name/version/architecture, native EXE/DLL machine 0x8664,
  all 221 block-map hashes, CMS signature-only validation and matching signer/
  publisher. This is not a public trust-chain certification claim.
- AppX has 14 entries: native runtime binaries, framework support, generated logos,
  PRI and package metadata. No legacy Content/GameData/UserData tree is packaged.
- BUILD.txt records clean source, exact commit, Release/x64/UWP/.NET Native,
  MonoGame 3.8.1.303, SDK 10.0.19041.0, signed=true, hardware verified=NO.
- Clean signed build PASS; zero ILT/MCG warnings confirmed in runtime-signed-build.log.

Build from clean source:

~~~powershell
./scripts/Build-XboxProof.ps1 -Target RuntimeProbe -Sign
~~~

Install the AppX and three x64 dependencies through Xbox Device Portal and launch
FreeSims Runtime Probe. Expect 10/10 PASS and a shared client UIButton captioned
TOGGLE HINTS. Left stick moves the cursor, A clicks once per release, B cancels a
held press, Y reruns tests, Menu exits. Require retained HINTS state after relaunch,
consistent size after Home/return, no unintended clicks on resume/reconnection,
and responsive UI. No game-data upload is required for this test.

[Complete installation, controls, procedure and success criteria](../experiments/XboxRuntimeProbe/TESTING.md).
Return this app's LocalState/proof.log (+ previous if present), a photo, console
model and exact OS build if available. For persistence failures, also provide
LocalState/UserData/config.ini. Do not use the earlier Common Probe's log folder.

Completed this cycle: shared SimsVille UWP runtime compilation, desktop debugger
boundary, pinned MonoGame color fix, pre-content client UI support, controller
pointer/cancellation foundation, generated font/button graphics, native UI and
settings test host, CPU regressions, signed package and integrity checks. Earlier
hardware-verified proof/files/common packages and code history are retained.

Build status: desktop Release/x86 PASS; SimsVille.Uwp Release/x64 C# PASS;
14/14 CPU checks PASS; scoped native host compile/package/sign PASS. The ten
runtime host tests and manual controls on Series S are NOT TESTED yet. Previous
common/settings hardware results do not automatically verify this new code.

Current blocker: actual Series S execution of the shared client UI/settings/input
host. Next after PASS: isolate an offline VM driver from the GonzoNet signatures,
audit/load legally supplied TS1 data with explicit missing-content diagnostics,
and complete the content provenance/read-write audit before neighborhood startup.
The native success here does not clear legacy networking, all runtime reflection,
TSO-dependent startup, main-menu assets, neighborhood/lot loading or game saves.

Files changed/created this cycle (since 2463a3e):
- .gitignore
- SimsVille.uwp/SimsVille.uwp.csproj
- SimsVille/GameContent/ContentManager.cs
- SimsVille/Platform/ClientPlatform.cs
- SimsVille/Platform/ControllerPointer.cs
- SimsVille/Platform/Desktop/ClientPlatform.cs
- SimsVille/SharedRuntime.projitems
- SimsVille/SimsVille.csproj
- SimsVille/UI/Controls/UIButton.cs
- SimsVille/UI/Panels/UIHouseMode.cs
- SimsVille/UI/Screens/CoreGameScreen.cs
- SimsVille/UI/UILayer.cs
- docs/XBOX_PORT.md
- experiments/XboxRuntimeProbe/Package.appxmanifest
- experiments/XboxRuntimeProbe/ProbeFont.cs
- experiments/XboxRuntimeProbe/Program.cs
- experiments/XboxRuntimeProbe/Properties/AssemblyInfo.cs
- experiments/XboxRuntimeProbe/Properties/Default.rd.xml
- experiments/XboxRuntimeProbe/RuntimeProbeGame.cs
- experiments/XboxRuntimeProbe/TESTING.md
- experiments/XboxRuntimeProbe/XboxRuntimeProbe.csproj
- scripts/Build-XboxProof.ps1
- sims.common/rendering/framework/io/InputManager.cs
- sims.common/rendering/framework/io/MouseEvent.cs
- tests/CommonCompatibility/CommonCompatibility.csproj
- tests/CommonCompatibility/ControllerPointerTests.cs
- tests/CommonCompatibility/Program.cs

## 2026-09-23 - Runtime Probe hardware PASS

Series S: user reports all Runtime Probe tests and manual procedures passed.
Photo IMG_20260922_235953.jpg confirms source 0dc91451b81b, 10/10 PASS, HINTS ON - SAVED, and 16 clicks. No new proof.log supplied.
This clears the runtime UI/settings/controller hardware gate above. Next: offline VM isolation and legally supplied TS1 content loading; full game startup remains unverified.
Documentation and updates will stay brief per user request to conserve quota.
