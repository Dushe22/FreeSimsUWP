# FreeSims Xbox Developer Mode port

## Current status

Recovered on Windows. Desktop Release/x86 compilation, the MonoGame 3.8.1.303
UWP Release/x64 proof, .NET Native, and signed AppX generation all PASS locally.
The signed package from source commit 3aaafa134f31b7fdb4216e8a1b12c44447323a22
is ready for the user hardware procedure at the end of this document.
Xbox behavior is NOT TESTED. Stop at this boundary and await console results.
GitHub Actions remains blocked by an account billing restriction.
Work is on xbox-uwp-port in Dushe22/FreeSimsUWP; never push to original upstream.
The FreeSims engine source remains unchanged from recovery baseline
 a7e9dba6cd4067b4efec54ae8e0443787da22992. No hardware milestones are tagged.

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

1. Await the user's Xbox Series X/S test of the signed proof specified below.
2. Investigate any deployment/runtime error from that exact package and its logs.
3. Only after hardware confirmation introduce the shared-source FreeSims UWP
   target and platform services. Do not claim engine or save functionality yet.
4. GitHub Actions is still blocked by account billing, independently of successful
   local builds. The user controls billing; no account settings were changed.

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
