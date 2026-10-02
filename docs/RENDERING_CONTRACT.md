# Static Xbox rendering contract

Apply these rules to TS1LotRenderData, TS1MaterialProvider, TS1LotRenderer and
XboxOfflineProbe. They preserve the 0.12.2 memory fixes and depth calibration.

## Ownership and lifetime

- A VM owns assembled routines. Session disposal unregisters BHAV events and
  releases runtime references. Never retain VM/content/scene objects in static
  caches or event subscriptions without matching disposal.
- Pixel/depth arrays are shared by authored frame identity. A renderer uploads
  each array once and owns each Texture2D once; sprite instances borrow them.
- Contours/materials belong only to the current lot. Dispose clears contours;
  the caller releases the lot, renderer and render target.
- Release the old view/lot before loading replacements. Collect at the existing
  memory boundaries, never on cursor movement or each frame.
- Constructor failure disposes partial GPU resources. Preserve the single
  zoom-1 OOM recovery, memory logging and controlled view failures.

## Render and update costs

- Decode masks/build contours during view construction, never in Draw/hover.
  Merge straight alpha boundaries instead of creating quads per opaque texel.
- Preallocate active wall arrays and the colored cap/reveal batch once per view.
  Limit the batch to 262144 VertexPositionColor vertices (4 MiB), use checked
  capacity arithmetic and reject oversized data with a controlled exception.
- Reuse visibility scratch sets/buffers. Refresh only on a changed cut revision.
  Pointer movement and restoration never upload textures.
- Preserve static scene redraw invalidation, cached alpha ordering, bounded
  draw batches and skipping sprite passes without matching alpha/opaque texels.
  Optional tests run on Menu, never at startup.

## Geometry and depth

- Share the 2.95 story height. Lower solid faces/caps end 0.015 below a visible
  upper floor; upper faces overlap downward. Never raise caps through carpets.
- Face holes, caps and reveals use the same authored alpha >= 128 boundary.
  Crop low faces/reveals without stretching UVs. Split top/end caps at holes;
  door thresholds remain open.
- Add thickness behind the authored visible plane. Preserve SPR2 depth,
  bounded camera-facing attachment bias and foreground depth testing.
  Never disable depth tests to resolve trim clipping.
- Fences use their global pattern by style (2:248, 12:249, 13:250, 14:251),
  independently of reverse-side wallpaper.
- Preserve saved lots and authored assets. Display reconciliation never moves
  VM entities, writes saves or runs Init/Main placement callbacks.
- Preserve the user-accepted roof mesh/shading/sampling and stair visual rules.

## Validation gate

Run scripts/Test-LotRendering.ps1 -ArchitectureQa. It checks geometry/CPU/GPU,
resource sharing/release, floor seams, real masks, both fence faces, camera
angles/zooms, cut/restore cycles, stairs, roofs and water. Inspect the affected
dynamic captures in artifacts/architecture-qa: legacy bool-render fixtures
alone do not exercise caps/reveals.

GC weak-reference tests cover VM/content, disposed lots, views, material pixels
and cached reveal arrays. GPU tests require stable texture counts/bytes and
geometry capacity through repeated wall-mode/hover changes.

Run scripts/Test-OfflineCompatibility.ps1 for changes to session/VM ownership,
behavior loading, save semantics or simulation. Do not broaden unrelated tests
after relevant checks pass without a new failure or unresolved concern.

Signed handoffs require clean committed source, native x64 compilation,
identity/signature/block-map/shader audit and no private keys/game data.
Desktop checks do not certify Xbox FPS or memory: confirm on hardware and
record memory/view timings in proof.log with captures.

Cutaway neighborhoods use wall centers in lot units (two tiles), only on the
selected story. Each section keeps its own 0.75 s restoration deadline; moving
the pointer must not renew deadlines for walls outside the current radius.
This shared rule applies to any loaded footprint, without lot IDs/coordinates.

Contained sprites use their root's world position plus each parent's rotated
SLOT offset. Horizontal units are 1/16 tile; vertical units are 1/5 tile, using
standard surface heights or the custom Z field. Preserve saved placement and
child direction, inherit root cutaway hosting and ancestor hidden state. Bound
invalid chains; compute only during view builds and share existing frame arrays.

## Room lighting and time preview
- Snapshot saved room contributions/outside flags into each view, never retain
  room entity lists or VM objects. Geometry triangles/visible wall faces and
  sprite container roots use the architecture room map, independent of cutaway.
- Share VMArchitecture.OutsideLightAt and RoomLighting.ColorAt with the engine;
  wrap finite time inputs through midnight. Emissive saved objects stay untinted
  only when the engine GeneratesLight/contribution flags permit it.
- Preserve base vertex colors, authored alpha and SPR2 depth. Relight existing
  vertex arrays from the base on palette changes; no compounded tint, texture
  edits/uploads, new lighting targets or shader/depth calibration changes.
- Use shared VMTimeController fixed ticks and the configured VMClock rate,
  with PAUSED/NORMAL/FAST/ULTRA modes. The static viewer advances a separate
  clock; later live simulation must consume the same pacing ticks rather than
  adding a second wall-clock timer. Never overwrite saved TicksPerMinute.
- Suspend pacing across inactive/disconnected/load/test intervals and discard
  the first elapsed interval on resume. Bound slow-frame work to 250 ms;
  never accumulate catch-up debt. Pause preserves the selected running speed
  and fractional tick. Cutaway restoration/camera movement use real time.
- Quantize lighting to five clock minutes and invalidate only changed colors;
  retain static redraw behavior. Clock changes here run no behaviors or saves.
- Run Test-LotRendering.ps1 -LightingQa for day/night room/depth/resource gates
  and dynamic captures. Run OfflineCompatibility when changing shared engine
  lighting. Signed native/hardware validation remains separate.

## Controlled live object gate
- Use TS1SimulationController.SupportsControlledBehavior as the common resource
  compatibility policy; do not select behavior families by house/coordinate.
- Start fresh Main threads only for validated families. Keep other saved threads
  held. No restoration of saved stacks or implicit full gameplay.
- Preload graphic states/dynamic layers at the view boundary. Limit to 64 graphic
  states/object, 32768 instances/view and 64 MiB of shared premultiplied/depth
  frame arrays; reject over-budget additions before cloning live frame arrays.
- Primitive sprite visibility/light-room and room-light snapshots own no VM
  entities. Update them after one bounded pacing batch; reuse existing vertices,
  alpha order, textures and targets. Never decode/upload/rebuild on a live tick.
- Raw imports preserve the saved clock representation. Explicit live sessions
  configure 30 ticks/sim minute through ConfigureLiveClock, preserving hour,
  minute and fractional phase to the new two-sim-second resolution. Advance
  only through session.Tick at 30 base ticks/real second, with 1x/2x/4x speeds;
  never tick a parallel preview clock. Discard blocked/background elapsed time.
- End controlled trials at 6000 ticks. Faults or unsupported topology/placement
  changes freeze simulation with context and require a fresh lot; no reset/delete
  recovery, saved-file writes or repeated failed behavior attempts.
- Run Test-LotRendering -SimulationQa and offline compatibility for this gate.
  Check exact initial static pixels, live changes, all angles/zooms, bounded
  ticks, resource reuse and weak-reference release. Inspect before/after captures.
  Hardware SIM/MEMORY timings and pressure remain the performance gate.
