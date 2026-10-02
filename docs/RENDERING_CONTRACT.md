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
