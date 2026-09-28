# Object Sprite Probe 0.8.0.0

Update the existing Offline Probe with this AppX and its x64 dependencies.
No additional game files are needed. Keep the existing LocalState/GameData.
Expect **6/6 PASS**. This build draws real VM chair/sofa SPR2 sprites.

1. X switches chair/three-tile sofa. A cycles four camera angles.
2. Y cycles all three zoom levels for both objects. Check for separated sofa
   tiles, clipped parts, unexpected flips, or missing pixels.
3. LB switches light/dark checkerboards. Edges should be transparent without
   colored fringes or solid rectangular backgrounds.
4. Menu reruns twice: still 6/6, with the selected object visible.
5. Home/return, then B exit/relaunch: image and controls still work at either
   viewport size. Test run and view selections start fresh after a new launch.

Send proof.log and a photo of the sofa from a front angle. Report any visual
issue even if the automated comparisons pass.

The previous 0.7 checkpoint file is left untouched. This rendering probe does
not load/save simulation sessions. Objects are initialized in a headless VM;
there is no advancing simulation, terrain, walls, lighting or full lot depth
compositing yet. Depth textures are uploaded and checked, but fixture drawing
uses ordered color layers. No game assets or private keys are in this package.
