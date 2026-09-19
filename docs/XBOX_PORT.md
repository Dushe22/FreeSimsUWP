# FreeSims Xbox Developer Mode port

## Current status

Experimental, not yet compiled or tested on Xbox. Work is confined to
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

No deployable package exists yet. Windows build automation and the proof project
are the next milestone. Do not treat this document as confirmation of a build.
Signing keys must be created outside the repository and never uploaded in build
artifacts. A public test certificate may accompany a signed test package.

Future deployment: enable Xbox Developer Mode/Device Portal, deploy the exact
Release/x64 test package and its x64 framework dependencies, then test on hardware.
Record source commit, Actions run, package identity, expected scene, controls and
log retrieval instructions when a package actually exists.

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

## Current blockers / next milestones

1. Establish Windows desktop compiler baseline and capture failures separately.
2. Compile/package asset-free MonoGame 3.8.1 UWP proof with rendering, input,
   diagnostic logging and generated tone audio.
3. Stop for user Xbox test results; no hardware success tags before confirmation.
4. Only then introduce the shared-source FreeSims UWP target and platform services.

## Xbox-specific modifications

None yet. This initial milestone adds documentation and ignore rules only.

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
