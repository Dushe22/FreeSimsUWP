using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Platform;
using FSO.LotView;
using FSO.SimAntics;
using FSO.LotView.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Tests
{
    public static class LotRenderTests
    {
        public const int Count=36;
        public static List<string> Run(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var result=new List<string>();bool oldWorld=VM.UseWorld;VM.UseWorld=false;
            Action<string,Action> check=(name,action)=>{try{action();result.Add("PASS "+name);}catch(Exception ex){result.Add("FAIL "+name);log(ex.ToString());}log(result.Last());};
            try {
                check("DISPOSED LOTS RELEASE VM AND CONTENT",()=>ReleasedLots(paths));
                check("GPU SHARED SPRITES AND RESOURCE DISPOSAL",()=>SharedSprites(device,paths,effect));
                check("CONTAINED SLOT GEOMETRY AND SAVED POSITIONS",()=>ContainedSlots(paths));
                check("GPU SLOT DEPTH AND RESOURCE REUSE",()=>SlotGpu(device,paths,effect));
                check("SPR2 COMPLETE ROWS WITHOUT END MARKER AND TRUNCATION",()=>SpriteRowBounds());
                check("LIVE OBJECTS GPU CHANGES AND BOUNDED RESOURCE REUSE",()=>LiveObjects(device,paths,effect,log));
                check("LIVE PRELOAD FOUR ANGLES THREE ZOOMS AND SESSION GUARDS",()=>LiveViews(paths));
                check("DISPOSED LIVE LOTS RELEASE VM AND FRAME DATA",()=>ReleasedLive(paths));
                check("TIME MODES FIXED TICKS PAUSE AND RESUME",()=>TimeModes());
                check("ROOM LIGHTING SAVED CONTRIBUTIONS AND MIDNIGHT",()=>LightingRooms(paths));
                check("GPU DAY NIGHT DEPTH AND RESOURCE REUSE",()=>LightingGpu(device,paths,effect));
                check("POINTER FOUR ANGLES ZOOMS AND FLOOR HEIGHTS",()=>PointerProjection());
                check("THREE WALL MODES HOVER AND STORY BOUNDARIES",()=>WallModes(paths));
                check("TIMED WALL RESTORE STATIONARY POINTER AND GPU",()=>TimedWallRestore(device,paths,effect));
                check("GPU WALL CAPS CUT ATTACHMENTS AND HOVER REUSE",()=>WallModeGpu(device,paths,effect));
                check("MASKED WALL THICKNESS AND FLOOR OCCLUSION",()=>WallThicknessGeometry(paths));
                check("GPU THICK OPENINGS AND DOOR THRESHOLDS",()=>ThickOpeningGpu(device,paths,effect));
                check("FENCE STYLE OWNS BOTH FACE MATERIALS",()=>FenceFaces(paths));
                check("CAMERA DEPTH AND WALL HEIGHT",()=>{if(!LotProjectionTests.Run())throw new InvalidOperationException("Projection checks failed.");});
                check("GPU DEPTH HOLES AND ALPHA",()=>DepthFixture(device,effect));
                check("TS1 MATERIAL MAPPING AND FENCE ALPHA",()=>MaterialMapping(paths));
                check("GPU WALL TOGGLE PRESERVES RAILINGS",()=>MaterialFixture(device,effect));
                check("DIAGONAL FULL FLOORS AND STAIR OPENING",()=>DiagonalFloors(paths));
                check("STAIR UPPER HANDRAIL SPRITES",()=>StairHandrails(paths));
                check("TS1 DOOR AND WINDOW OPENING MASKS",()=>OpeningMasks(paths));
                check("GPU OPENINGS PRESERVE DEPTH",()=>OpeningFixture(device,paths,effect));
                check("GPU WIDE WINDOW AND DOUBLE DOOR FRAMES",()=>OpeningFrames(device,paths,effect));
                check("SHARED FLOOR HEIGHT AND STORY JOINTS",()=>StoryJoints(paths));
                check("ROOF BMP RLE8 AND BOUNDS",()=>RoofBitmap());
                check("ROOF HIPS AND OPEN COURTYARDS",()=>RoofGeometry(paths));
                check("SAVED GRASS AND DETERMINISTIC TERRAIN",()=>TerrainMaterial(paths));
                check("AUTHORED POOL AND WATER BORDERS",()=>WaterMaterials(paths));
                check("GPU POOLS FOUR ANGLES THREE ZOOMS",()=>WaterViews(device,paths,effect));
                check("POOL LADDER ATTACHMENT FOUR ANGLES THREE ZOOMS",()=>PoolLadderAttachments(paths));
                foreach(int house in new[]{2,28}) check("HOUSE "+house+" FOUR ANGLES THREE LEVELS",()=> {
                    using(var lot=new TS1LotRenderData(paths,house)) {
                        for(int level=1;level<=3;level++) {
                            int floorCount=-1,wallCount=-1;
                            for(int rotation=0;rotation<4;rotation++) {
                                var data=lot.Build(1,rotation,level);
                                if(level==3 && (data.RoofTriangles==0 || data.RoofMaterials.Count==0))throw new InvalidOperationException("Missing saved roof.");
                                if(data.Rendered+data.Hidden+data.OutOfWorld+data.Contained+data.NoGraphic+data.Unsupported+data.AboveLevel!=lot.ObjectCount)
                                    throw new InvalidOperationException("Unaccounted saved objects.");
                                if(level==2 && !data.WallMaterials.Any(s=>s.KeepWhenWallsHidden && s.Material.Name=="wall:251")) throw new InvalidOperationException("Saved banisters missing from persistent geometry.");
                                if(data.Rendered!=data.Sprites.Select(s=>s.ObjectID).Distinct().Count()) throw new InvalidOperationException("Visible object count mismatch.");
                                if(data.Rendered<20 || data.FloorTiles==0 || data.WallEdges==0 || data.FloorMaterials.Count<3 || data.WallMaterials.Count<3) throw new InvalidOperationException("Missing lot geometry/content.");
                                if(floorCount>=0 && (data.FloorTiles!=floorCount || data.WallEdges!=wallCount)) throw new InvalidOperationException("Camera changed architecture.");
                                floorCount=data.FloorTiles;wallCount=data.WallEdges;
                                var previous=device.GetRenderTargets();var viewport=device.Viewport;
                                using(var renderer=new TS1LotRenderer(device,data,effect))
                                using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                                    try {
                                        device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);
                                        renderer.Draw(TS1LotRenderer.Camera(lot.Size,1,rotation,640,360,Vector2.Zero),true);
                                        device.SetRenderTargets(previous);device.Viewport=viewport;
                                        var pixels=new Color[640*360];target.GetData(pixels);
                                        if(pixels.Count(p=>p!=new Color(16,24,39))<10000 || pixels.Select(p=>p.PackedValue).Distinct().Count()<100)
                                            throw new InvalidOperationException("Rendered lot is empty or lacks object colors.");
                                    }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
                                }
                                log("LOT GPU house="+house+" level="+level+" rotation="+rotation+" drawn="+data.Rendered+" unsupported="+data.Unsupported+" contained="+data.Contained+" floors="+floorCount+" walls="+wallCount+" openings="+data.OpeningEdges+" joints="+data.StoryJoints+" roofTriangles="+data.RoofTriangles);
                            }
                        }
                    }
                });
            }finally{VM.UseWorld=oldWorld;}
            return result;
        }
        private static void SpriteRowBounds()
        {
            Func<bool,bool,FSO.Files.Formats.IFF.Chunks.SPR2Frame> read=(complete,marker)=>{
                var iff=new FSO.Files.Formats.IFF.IffFile();
                var palette=new FSO.Files.Formats.IFF.Chunks.PALT {ChunkID=1,ChunkProcessed=true,Colors=new Color[256]};
                iff.AddChunk(palette);
                var sprite=new FSO.Files.Formats.IFF.Chunks.SPR2 {ChunkID=1,ChunkProcessed=true,DefaultPaletteID=1};iff.AddChunk(sprite);
                using(var stream=new System.IO.MemoryStream()) {
                    var writer=new System.IO.BinaryWriter(stream);
                    writer.Write((ushort)1);writer.Write((ushort)1);writer.Write((uint)3);
                    writer.Write((ushort)1);writer.Write((ushort)0);writer.Write((short)0);writer.Write((short)0);
                    if(complete)writer.Write((ushort)0x8001); // one fully transparent row
                    else writer.Write((ushort)4); // row header with missing pixel payload
                    if(marker)writer.Write((ushort)0xA000);
                    writer.Flush();stream.Position=0;
                    var frame=new FSO.Files.Formats.IFF.Chunks.SPR2Frame(sprite);
                    using(var io=FSO.Files.Utils.IoBuffer.FromStream(stream,FSO.Files.Utils.ByteOrder.LITTLE_ENDIAN))frame.ReadDeferred(1001,io);
                    return frame;
                }
            };
            var withMarker=read(true,true);var without=read(true,false);
            if(!withMarker.PixelData.SequenceEqual(without.PixelData)||!withMarker.ZBufferData.SequenceEqual(without.ZBufferData)||
                without.PixelData[0].A!=0||without.ZBufferData[0]!=255)throw new InvalidOperationException("Complete frame terminator changes pixels.");
            bool rejected=false;try{read(false,false);}catch(System.IO.EndOfStreamException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Truncated row accepted.");
        }
        private static void LiveObjects(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house,true)) {
                var view=lot.Build(1,0,2);
                if(!view.Live||view.Unsupported!=0||lot.ActiveObjects==0||(house==28&&lot.ActiveObjects!=80))
                    throw new InvalidOperationException("Invalid controlled behavior/preload coverage: house="+house+" active="+lot.ActiveObjects+" unsupported="+view.Unsupported);
                var camera=TS1LotRenderer.Camera(lot.Size,1,0,640,360,Vector2.Zero);
                lot.UpdateWalls(view,TS1WallMode.Up,null,camera,640,360);
                using(var renderer=new TS1LotRenderer(device,view,effect))
                using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                    Func<Color[]> draw=()=>{
                        device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Transparent,1,0);
                        renderer.Draw(camera,TS1WallMode.Up);device.SetRenderTargets(previous);device.Viewport=viewport;
                        var pixels=new Color[640*360];target.GetData(pixels);return pixels;
                    };
                    renderer.UpdateLighting(12);
                    var before=draw();
                    // Preloading hidden states must preserve the static baseline exactly.
                    using(var original=new TS1LotRenderData(paths,house))
                    using(var staticRenderer=new TS1LotRenderer(device,original.Build(1,0,2),effect)) {
                        var staticView=staticRenderer.Data;original.UpdateWalls(staticView,TS1WallMode.Up,null,camera,640,360);
                        staticRenderer.UpdateLighting(12);device.SetRenderTarget(target);
                        device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Transparent,1,0);
                        staticRenderer.Draw(camera,TS1WallMode.Up);device.SetRenderTargets(previous);device.Viewport=viewport;
                        var pixels=new Color[640*360];target.GetData(pixels);
                        if(!before.SequenceEqual(pixels))throw new InvalidOperationException("Hidden live states altered initial pixels.");
                    }
                    int textures=renderer.TextureCount,capacity=renderer.WallGeometryCapacity,count=view.Sprites.Count;
                    long bytes=renderer.TextureBytes;var pixelsRefs=view.Sprites.Select(x=>x.Layer.Pixels).ToArray();
                    long start=lot.Clock.Ticks;
                    for(int batch=0;batch<80;batch++) {
                        if(lot.AdvanceSimulation(75,view))renderer.UpdateSimulation();
                        if(batch%4==0)draw();
                        if(renderer.TextureCount!=textures||renderer.TextureBytes!=bytes||renderer.WallGeometryCapacity!=capacity||view.Sprites.Count!=count)
                            throw new InvalidOperationException("Live ticks allocated/rebuilt scene resources.");
                    }
                    if(lot.Clock.Ticks!=start+6000||lot.CompletedTicks!=6000||lot.SimulationFault!=null||!lot.SimulationStopped||
                        lot.AdvanceSimulation(1,view)||!pixelsRefs.SequenceEqual(view.Sprites.Select(x=>x.Layer.Pixels)))
                        throw new InvalidOperationException("Live limit/clock/frame ownership mismatch.");
                    if(house==28&&(view.SpriteRevision==0||view.LightRevision==0||before.SequenceEqual(draw())))
                        throw new InvalidOperationException("Behavior changes did not reach GPU sprites/lighting.");
                    if(view.SlottedRendered!=(house==28?11:0))throw new InvalidOperationException("Live trial changed SLOT coverage.");
                    log("LIVE GPU house="+house+" active="+lot.ActiveObjects+" ticks="+lot.CompletedTicks+
                        " sprites="+count+" spriteRevision="+view.SpriteRevision+" lightRevision="+view.LightRevision+
                        " textures="+textures+" bytes="+bytes+" clockRate="+lot.Clock.TicksPerMinute);
                }
            }}finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static void LiveViews(GamePaths paths)
        {
            foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house,true)) {
                for(int r=0;r<4;r++)for(int zoom=1;zoom<=3;zoom++) {
                    var view=lot.Build(zoom,r,2);long ticks=lot.Clock.Ticks;
                    if(lot.AdvanceSimulation(0,view)||ticks!=lot.Clock.Ticks)throw new InvalidOperationException("Pause changed live state.");
                    foreach(int bad in new[]{-1,76}) {
                        bool rejected=false;try{lot.AdvanceSimulation(bad,view);}catch(ArgumentOutOfRangeException){rejected=true;}
                        if(!rejected)throw new InvalidOperationException("Unbounded live tick batch accepted.");
                    }
                    bool foreign=false;try{lot.AdvanceSimulation(1,new TS1LotRenderData.View());}catch(InvalidOperationException){foreign=true;}
                    if(!foreign||lot.Clock.Ticks!=ticks)throw new InvalidOperationException("Foreign view consumed ticks.");
                    lot.AdvanceSimulation(1,view);
                    if(view.Unsupported!=0||view.Sprites.Count>TS1LotRenderData.LiveSpriteBudget)throw new InvalidOperationException("Live view unsupported/oversized.");
                    foreach(var group in view.Sprites.Where(s=>s.Visible).GroupBy(s=>s.ObjectID))
                        if(group.Select(s=>s.Graphic).Distinct().Count()>1)throw new InvalidOperationException("Multiple graphic states drawn together.");
                }
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference[] DisposedLive(GamePaths paths)
        {
            using(var lot=new TS1LotRenderData(paths,28,true)) {
                var view=lot.Build(1,0,2);lot.AdvanceSimulation(75,view);
                return new[]{new WeakReference(lot),new WeakReference(view),new WeakReference(view.Sprites.First().Layer.Pixels)};
            }
        }
        private static void ReleasedLive(GamePaths paths)
        {
            var refs=DisposedLive(paths);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            if(refs.Any(r=>r.IsAlive))throw new InvalidOperationException("Live scene/frame data retained after disposal.");
        }
        private static void TimeModes()
        {
            foreach(var speed in new[]{VMTimeSpeed.Normal,VMTimeSpeed.Fast,VMTimeSpeed.Ultra})
            foreach(double dt in new[]{.01,.05,.1,.25}) {
                var pacing=new VMTimeController();pacing.SetSpeed(speed);
                var clock=new VMClock{Hours=23,Minutes=59,TicksPerMinute=VMTimeController.TS1TicksPerMinute};
                int ticks=0;
                for(int i=0;i<(int)Math.Round(10/dt);i++){
                    int step=pacing.Advance(dt);ticks+=step;
                    for(int t=0;t<step;t++)clock.Tick();
                }
                if(ticks!=300*pacing.Multiplier || clock.Ticks!=ticks ||
                    clock.Hours!=(10*pacing.Multiplier-1)/60 ||
                    clock.Minutes!=(10*pacing.Multiplier-1)%60 || clock.MinuteFractions!=0)
                    throw new InvalidOperationException("Speed/clock depends on frame rate or lost midnight ticks.");
                var before=clock.Save();pacing.TogglePause();
                if(pacing.Advance(100)!=0 || pacing.Speed!=VMTimeSpeed.Paused || clock.Ticks!=before.Ticks)
                    throw new InvalidOperationException("Pause advanced time.");
                pacing.TogglePause();
                if(pacing.Speed!=speed)throw new InvalidOperationException("Pause lost the selected speed.");
                pacing.Suspend();
                if(pacing.Advance(100)!=0 || pacing.Advance(.1)!=3*pacing.Multiplier)
                    throw new InvalidOperationException("Resume caught up suspended time.");
                if(pacing.Advance(100)>75)throw new InvalidOperationException("Long frame created unbounded tick work.");
            }
            var controls=new VMTimeController();
            controls.ChangeSpeed(1);
            if(controls.Speed!=VMTimeSpeed.Normal)throw new InvalidOperationException("Initial normal selection failed.");
            controls.ChangeSpeed(1);controls.ChangeSpeed(1);controls.ChangeSpeed(1);
            if(controls.Speed!=VMTimeSpeed.Ultra)throw new InvalidOperationException("Speed upper bound failed.");
            controls.ChangeSpeed(-1);controls.ChangeSpeed(-1);controls.ChangeSpeed(-1);controls.ChangeSpeed(-1);
            if(controls.Speed!=VMTimeSpeed.Paused)throw new InvalidOperationException("Speed lower bound failed.");
            controls.TogglePause();controls.Advance(.01);controls.TogglePause();controls.Advance(.1);controls.TogglePause();
            if(controls.Advance(.09)!=3)throw new InvalidOperationException("Pause discarded fractional ticks or resumed at wrong speed.");
            foreach(double bad in new[]{double.NaN,double.PositiveInfinity,-1.0}) {
                bool rejected=false;try{controls.Advance(bad);}catch(ArgumentOutOfRangeException){rejected=true;}
                if(!rejected)throw new InvalidOperationException("Invalid elapsed time accepted.");
            }
            var legacy=new VMClock();for(int t=0;t<150;t++)legacy.Tick();
            if(legacy.Minutes!=1)throw new InvalidOperationException("Legacy clock fallback changed.");
            var saved=new VMClock(new VMClock{TicksPerMinute=45,Hours=18,Minutes=59,MinuteFractions=44}.Save());
            saved.Tick();
            if(saved.TicksPerMinute!=45||saved.Hours!=19||saved.Minutes!=0)
                throw new InvalidOperationException("Clock ignored saved tick rate.");
        }

        private static void LightingRooms(GamePaths paths)
        {
            var night=VMArchitecture.OutsideLightAt(0);
            if(night!=new Color(75,105,183)||VMArchitecture.OutsideLightAt(.5)!=Color.White ||
                VMArchitecture.OutsideLightAt(.75)!=new Color(217,109,0) ||
                VMArchitecture.OutsideLightAt(1)!=night||VMArchitecture.OutsideLightAt(-1)!=night)
                throw new InvalidOperationException("Shared time palette/24-hour wrapping changed.");
            if(Vector3.Distance(VMArchitecture.OutsideLightAt(1-1e-7).ToVector3(),night.ToVector3())>.02f)
                throw new InvalidOperationException("Midnight light is discontinuous.");
            bool rejected=false;try{VMArchitecture.OutsideLightAt(double.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Non-finite lighting time accepted.");
            var dark=new RoomLighting();
            var lit=new RoomLighting{AmbientLight=100};
            var window=new RoomLighting{OutsideLight=100};
            if(dark.ColorAt(Color.White,false)!=new Color(86,86,86) ||
                lit.ColorAt(night,false)!=Color.White||window.ColorAt(night,false)!=night ||
                dark.ColorAt(night,true)!=night || dark.ColorAt(night,false).B<=dark.ColorAt(night,false).R)
                throw new InvalidOperationException("Room daylight/electric/minimum ambient classification failed.");
            foreach(int house in new[]{2,28}) {
                var iff=new FSO.Files.Formats.IFF.IffFile(new FSO.Common.Platform.NeighborhoodStore(paths,0).GetReadPath("Houses/House"+house.ToString("00")+".iff"));
                using(var saved=FSO.SimAntics.TS1LotObjectSession.Load(iff,new FSO.Content.TS1.TS1ObjectProvider(paths)))
                using(var lot=new TS1LotRenderData(paths,house)){
                    var view=lot.Build(2,0,3);var rooms=saved.VM.Context.RoomInfo;
                    if(view.Lighting.Length!=rooms.Length||!view.OutsideRooms.Any(x=>x)||!view.OutsideRooms.Any(x=>!x))
                        throw new InvalidOperationException("Missing indoor/outdoor room snapshot.");
                    for(int i=0;i<rooms.Length;i++){
                        if(ReferenceEquals(rooms[i].Light,view.Lighting[i]) ||
                            rooms[i].Light.AmbientLight!=view.Lighting[i].AmbientLight ||
                            rooms[i].Light.OutsideLight!=view.Lighting[i].OutsideLight ||
                            rooms[i].Room.IsOutside!=view.OutsideRooms[i])
                            throw new InvalidOperationException("Light snapshot retains or changes VM room data.");
                    }
                    foreach(var sprite in view.Sprites){
                        var entity=saved.VM.GetObjectById(sprite.ObjectID);var root=entity;
                        while(root.Container!=null)root=root.Container;
                        var flags=(FSO.SimAntics.VMEntityFlags2)entity.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.FlagField2);
                        bool emits=(flags&FSO.SimAntics.VMEntityFlags2.GeneratesLight)!=0 &&
                            (flags&(FSO.SimAntics.VMEntityFlags2.ArchitectualWindow|FSO.SimAntics.VMEntityFlags2.ArchitectualDoor))==0 &&
                            entity.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.LightingContribution)>0;
                        if(sprite.LightRoom!=(emits?65535:saved.VM.Context.GetObjectRoom(root)))
                            throw new InvalidOperationException("Sprite/SLOT lighting did not follow saved room or light source.");
                    }
                    if(view.TerrainMaterials.Concat(view.RoofMaterials).Any(s=>s.LightRooms.Any(r=>r!=TS1LotRenderData.ExteriorLightRoom)))
                        throw new InvalidOperationException("Exterior geometry uses a room-zero ambient fallback.");
                    foreach(var surface in view.FloorMaterials.Concat(view.WallMaterials).Concat(view.TerrainMaterials).Concat(view.RoofMaterials))
                        if(surface.LightRooms.Count!=surface.Vertices.Count||surface.LightRooms.Any(r=>r>=rooms.Length&&r!=TS1LotRenderData.ExteriorLightRoom))
                            throw new InvalidOperationException("Surface lighting metadata mismatches geometry.");
                    if(!view.FloorMaterials.Any(s=>s.LightRooms.Any(r=>!view.OutsideRooms[r])) ||
                        !view.FloorMaterials.Any(s=>s.LightRooms.Any(r=>view.OutsideRooms[r])) ||
                        !view.WallSections.Any(s=>!view.OutsideRooms[s.LightRoom]) ||
                        !view.WallSections.Any(s=>view.OutsideRooms[s.LightRoom]))
                        throw new InvalidOperationException("Floors/wall faces lack their independent interior/exterior rooms.");
                }
            }
        }
        private static void LightingGpu(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            LightingBands(device,effect);
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {
                foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
                for(int rotation=0;rotation<4;rotation++)foreach(int zoom in new[]{1,2,3}) {
                    var view=lot.Build(zoom,rotation,2);
                    var camera=TS1LotRenderer.Camera(lot.Size,zoom,rotation,640,360,Vector2.Zero);
                    lot.UpdateWalls(view,TS1WallMode.Up,null,camera,640,360);
                    var originals=view.WallMaterials.SelectMany(s=>s.Vertices).ToArray();
                    using(var renderer=new TS1LotRenderer(device,view,effect))
                    using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                        Func<bool,Color[]> draw=reverse=>{
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Transparent,1,0);
                            renderer.Draw(camera,view.WallMode,reverse);
                            device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[640*360];target.GetData(pixels);return pixels;
                        };
                        var original=draw(false);var reverseOriginal=draw(true);
                        int textures=renderer.TextureCount,capacity=renderer.WallGeometryCapacity;
                        long bytes=renderer.TextureBytes;
                        renderer.UpdateLighting(12);var day=draw(false);
                        renderer.UpdateLighting(0);var night=draw(false);
                        var reverseNight=draw(true);
                        // Authored coplanar DGRP layers can already tie in the unlit
                        // baseline. Lighting must introduce no new depth/order ties.
                        if(day.SequenceEqual(night)||night.Count(p=>p.A>0)!=day.Count(p=>p.A>0) ||
                            night.Where((p,i)=>p!=reverseNight[i]&&original[i]==reverseOriginal[i]).Any())
                            throw new InvalidOperationException("Lighting changed coverage/depth ordering at house="+house+" r="+rotation+" zoom="+zoom);
                        renderer.UpdateLighting(6);draw(false);renderer.UpdateLighting(18);draw(false);
                        renderer.UpdateLighting(24);
                        if(!night.SequenceEqual(draw(false))||renderer.UpdateLighting(0))
                            throw new InvalidOperationException("Cycle/midnight does not restore exact night pixels.");
                        renderer.UpdateLighting(12);
                        if(!day.SequenceEqual(draw(false)))throw new InvalidOperationException("Day restoration accumulates tint.");
                        var hover=FindWallHover(lot,view,camera,640,360);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,1);
                        renderer.UpdateLighting(0);draw(false);
                        lot.UpdateWalls(view,TS1WallMode.Up,null,camera,640,360,2);
                        if(!night.SequenceEqual(draw(false)))throw new InvalidOperationException("Night wall caps/attachments failed restoration.");
                        renderer.UpdateLighting(12,false);
                        if(!original.SequenceEqual(draw(false)))throw new InvalidOperationException("Unlit baseline changed after light/wall cycles.");
                        if(textures!=renderer.TextureCount||bytes!=renderer.TextureBytes||capacity!=renderer.WallGeometryCapacity ||
                            !originals.SequenceEqual(view.WallMaterials.SelectMany(s=>s.Vertices)))
                            throw new InvalidOperationException("Relighting uploads resources or mutates source geometry.");
                    }
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        // Independent white-texture bands distinguish exterior, electric interior,
        // unlit interior and emissive light on GPU (not just CPU color formulas).
        private static void LightingBands(GraphicsDevice device,byte[] effect)
        {
            var view=new TS1LotRenderData.View{
                Lighting=new[]{new RoomLighting(),new RoomLighting{AmbientLight=100},new RoomLighting()},
                OutsideRooms=new[]{false,false,false}};
            var surface=new TS1LotRenderData.Surface{Material=new FSO.Content.TS1.TS1MaterialProvider.Material{
                Width=1,Height=1,Pixels=new[]{Color.White}}};
            ushort[] rooms={TS1LotRenderData.ExteriorLightRoom,1,2,TS1LotRenderData.EmissiveLightRoom};
            for(int band=0;band<4;band++)foreach(var source in Quad(0,Color.White)){
                var vertex=source;vertex.Position.X=band*16+vertex.Position.X/4;
                surface.Vertices.Add(vertex);surface.LightRooms.Add(rooms[band]);
            }
            view.FloorMaterials.Add(surface);
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            using(var renderer=new TS1LotRenderer(device,view,effect))
            using(var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.Depth24)){
                try{
                    renderer.UpdateLighting(0);
                    device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Transparent,1,0);
                    renderer.Draw(Matrix.CreateOrthographicOffCenter(0,64,64,0,-128,128),true);
                    device.SetRenderTargets(previous);device.Viewport=viewport;
                    var pixels=new Color[4096];target.GetData(pixels);
                    Expect(pixels[32*64+8],new Color(75,105,183));
                    Expect(pixels[32*64+24],Color.White);
                    Expect(pixels[32*64+40],new Color(50,70,123));
                    Expect(pixels[32*64+56],Color.White);
                }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
            }
        }
        private static void PointerProjection()
        {
            foreach(int zoom in new[]{1,2,3})for(int r=0;r<4;r++)for(int level=1;level<=2;level++)
            foreach(var dimensions in new[]{new Point(1280,530),new Point(640,360)}) {
                var camera=TS1LotRenderer.Camera(64,zoom,r,dimensions.X,dimensions.Y,new Vector2(123,-67));
                var tile=new Vector3(21.25f,32.75f,(level-1)*2.95f);
                var pointer=TS1LotRenderData.PointerAtTile(tile,camera,dimensions.X,dimensions.Y,zoom,r);
                var restored=TS1LotRenderData.TileAtPointer(pointer,camera,dimensions.X,dimensions.Y,zoom,r,level);
                if(Vector2.Distance(restored,new Vector2(tile.X,tile.Y))>.001f)throw new InvalidOperationException("Pointer failed rotated/panned floor projection.");
            }
        }
        private static void ContainedSlots(GamePaths paths)
        {
            var standard=new FSO.Files.Formats.IFF.Chunks.SLOTItem {Type=0,Height=4,Offset=new Vector3(16,0,100)};
            var directions=new[]{Direction.NORTH,Direction.EAST,Direction.SOUTH,Direction.WEST};
            var expected=new[]{new Vector3(1,0,.8f),new Vector3(0,1,.8f),new Vector3(-1,0,.8f),new Vector3(0,-1,.8f)};
            for(int i=0;i<4;i++)if(Vector3.Distance(TS1LotRenderData.SlotOffset(standard,directions[i]),expected[i])>.00001f)
                throw new InvalidOperationException("SLOT units, standard height or parent rotation incorrect.");
            standard.Height=5;standard.Offset=new Vector3(8,-16,2.5f);
            if(TS1LotRenderData.SlotOffset(standard,Direction.NORTH)!=new Vector3(.5f,-1,.5f))throw new InvalidOperationException("Custom SLOT height ignored.");
            standard.Height=0;bool invalid=false;
            try {TS1LotRenderData.SlotOffset(standard,Direction.NORTH);}catch(System.IO.InvalidDataException){invalid=true;}
            if(!invalid)throw new InvalidOperationException("Malformed SLOT height accepted.");
            foreach(int house in new[]{2,28}) {
                var iff=new FSO.Files.Formats.IFF.IffFile(new NeighborhoodStore(paths,0).GetReadPath("Houses/House"+house.ToString("00")+".iff"));
                using(var saved=TS1LotObjectSession.Load(iff,new FSO.Content.TS1.TS1ObjectProvider(paths)))using(var lot=new TS1LotRenderData(paths,house)) {
                    var children=saved.VM.Entities.Where(e=>e.Container!=null).ToArray();
                    if(children.Length!=(house==28?11:0))throw new InvalidOperationException("Changed saved containment count.");
                    foreach(var child in children) {
                        var position=child.Position;var direction=child.Direction;short slot=child.ContainerSlot;var parent=child.Container;
                        var visual=TS1LotRenderData.VisualPosition(child);
                        var floor=new Vector3(parent.Position.x/16f,parent.Position.y/16f,(parent.Position.Level-1)*TS1LotRenderData.StoryHeight);
                        if(Vector3.Distance(visual,floor+new Vector3(0,0,.8f))>.00001f)throw new InvalidOperationException("Saved counter child is not on its surface.");
                        if(child.Position!=position||child.Direction!=direction||child.ContainerSlot!=slot||child.Container!=parent)
                            throw new InvalidOperationException("SLOT rendering mutated saved placement.");
                    }
                    for(int zoom=1;zoom<=3;zoom++)for(int r=0;r<4;r++) {
                        var data=lot.Build(zoom,r,2);
                        if(data.Contained!=0||data.SlottedRendered!=children.Length||data.Unsupported!=0)
                            throw new InvalidOperationException("Contained children held/missing/unsupported.");
                        foreach(var child in children) {
                            var sprites=data.Sprites.Where(s=>s.ObjectID==child.ObjectID).ToArray();
                            if(sprites.Length==0)throw new InvalidOperationException("Missing contained sprite.");
                            var visual=TS1LotRenderData.VisualPosition(child);
                            foreach(var sprite in sprites)if(Vector2.Distance(sprite.Position,TS1SpriteLayer.Project(visual,zoom,r)+sprite.Layer.Offset)>.001f)
                                throw new InvalidOperationException("Contained sprite not projected from its SLOT.");
                        }
                    }
                    if(children.Length==0)continue;
                    // Independent nested layout exercises general composition, not lot coordinates.
                    var root=new VMGameObject(children[0].Container.Object,null){Position=new LotTilePos(160,320,2),Direction=Direction.EAST};
                    var middle=new VMGameObject(root.Object,null){Direction=Direction.SOUTH};var leaf=new VMGameObject(children[0].Object,null){Direction=Direction.NORTH};
                    Func<float,FSO.Files.Formats.IFF.Chunks.SLOT> slots=z=>new FSO.Files.Formats.IFF.Chunks.SLOT {Slots=new Dictionary<ushort,List<FSO.Files.Formats.IFF.Chunks.SLOTItem>> {{0,new List<FSO.Files.Formats.IFF.Chunks.SLOTItem>{new FSO.Files.Formats.IFF.Chunks.SLOTItem{Type=0,Height=5,Offset=new Vector3(16,0,z)}}}}};
                    root.Slots=slots(5);middle.Slots=slots(2.5f);root.Contained=new VMEntity[1];middle.Contained=new VMEntity[1];
                    root.PlaceInSlot(middle,0,false,saved.VM.Context);middle.PlaceInSlot(leaf,0,false,saved.VM.Context);
                    if(Vector3.Distance(TS1LotRenderData.VisualPosition(leaf),new Vector3(9,21,TS1LotRenderData.StoryHeight+1.5f))>.0001f)
                        throw new InvalidOperationException("Nested SLOT composition used child direction or double-counted story.");
                    // A malformed graph must terminate, rather than hang a view build.
                    leaf.Slots=slots(0);leaf.Contained=new VMEntity[]{root};root.Container=leaf;root.ContainerSlot=0;invalid=false;
                    try {TS1LotRenderData.VisualPosition(leaf);}catch(System.IO.InvalidDataException){invalid=true;}
                    if(!invalid)throw new InvalidOperationException("Cyclic SLOT graph accepted.");
                }
            }
        }
        private static void SlotGpu(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {using(var lot=new TS1LotRenderData(paths,28))
                for(int zoom=1;zoom<=3;zoom++)for(int r=0;r<4;r++) {
                    var view=lot.Build(zoom,r,1);
                    var first=view.Sprites.Where(s=>s.ObjectID==400).ToArray();var second=view.Sprites.Where(s=>s.ObjectID==401).ToArray();
                    if(first.Length==0||first.Length!=second.Length||Enumerable.Range(0,first.Length).Any(i=>!ReferenceEquals(first[i].Layer.Pixels,second[i].Layer.Pixels)||!ReferenceEquals(first[i].Layer.Depth,second[i].Layer.Depth)))
                        throw new InvalidOperationException("Repeated slotted objects duplicated frame arrays.");
                    view.TerrainMaterials.Clear();view.FloorMaterials.Clear();view.WallMaterials.Clear();view.WallSections.Clear();
                    view.Sprites.RemoveAll(s=>s.ObjectID!=552&&s.ObjectID!=561);var child=view.Sprites.Where(s=>s.ObjectID==561).ToArray();
                    if(child.Length==0)throw new InvalidOperationException("Missing counter register fixture.");
                    var baseCamera=TS1LotRenderer.Camera(lot.Size,zoom,r,640,360,Vector2.Zero);
                    var pointer=TS1LotRenderData.PointerAtTile(new Vector3(19,26,.4f),baseCamera,640,360,zoom,r);
                    var camera=TS1LotRenderer.Camera(lot.Size,zoom,r,640,360,new Vector2(320,180)-pointer);
                    Func<Color[]> draw=()=> {
                        using(var renderer=new TS1LotRenderer(device,view,effect))using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                            lot.UpdateWalls(view,TS1WallMode.Up,null,camera,640,360);
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);renderer.Draw(camera,TS1WallMode.Up);
                            device.SetRenderTargets(previous);device.Viewport=viewport;var pixels=new Color[640*360];target.GetData(pixels);
                            int textures=renderer.TextureCount;long bytes=renderer.TextureBytes;
                            lot.UpdateWalls(view,TS1WallMode.Cutaway,null,camera,640,360);renderer.Draw(camera,TS1WallMode.Cutaway);
                            if(renderer.TextureCount!=textures||renderer.TextureBytes!=bytes)throw new InvalidOperationException("SLOT visibility allocated textures.");
                            return pixels;
                        }
                    };
                    var both=draw();var reordered=view.Sprites.OrderByDescending(s=>s.ObjectID).ToArray();view.Sprites.Clear();view.Sprites.AddRange(reordered);
                    if(!both.SequenceEqual(draw()))throw new InvalidOperationException("SLOT depth depends on insertion order.");
                    view.Sprites.RemoveAll(s=>s.ObjectID==561);var parentOnly=draw();
                    if(both.Zip(parentOnly,(a,b)=>a!=b).Count(changed=>changed)<3)throw new InvalidOperationException("Contained register missing/occluded by its counter.");
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static Vector2 FindWallHover(TS1LotRenderData lot,TS1LotRenderData.View view,Matrix camera,int width,int height,string exclude=null)
        {
            foreach(var wall in view.WallSections.Where(s=>s.Story==Math.Min(view.Level,2)-1)) {
                var point=(wall.Full[3]+wall.Full[2])*.475f+(wall.Full[0]+wall.Full[1])*.025f;
                var clip=Vector3.Transform(point,camera);
                var pointer=new Vector2((clip.X+1)*width/2,(1-clip.Y)*height/2);
                lot.UpdateWalls(view,TS1WallMode.Cutaway,pointer,camera,width,height);
                bool hit=view.LastWall!=null&&view.LastWall!=exclude&&!view.WallSections.Any(s=>s.RequestedCut&&s.Key==exclude);
                lot.UpdateWalls(view,TS1WallMode.Up,null,camera,width,height);
                if(hit)return pointer;
            }
            throw new InvalidOperationException("No opaque wall hover fixture found.");
        }
        private static void TimedWallRestore(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {
                foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
                for(int rotation=0;rotation<4;rotation++)for(int level=1;level<=2;level++) {
                    var view=lot.Build(1,rotation,level);
                    var camera=TS1LotRenderer.Camera(lot.Size,1,rotation,640,360,Vector2.Zero);
                    var hover=FindWallHover(lot,view,camera,640,360);
                    lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360);
                    string firstWall=view.LastWall;
                    var otherHover=FindWallHover(lot,view,camera,640,360,firstWall);
                    using(var renderer=new TS1LotRenderer(device,view,effect))
                    using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                        Func<Color[]> draw=()=> {
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);
                            renderer.Draw(camera,view.WallMode);
                            device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[640*360];target.GetData(pixels);return pixels;
                        };
                        var baseline=draw();int textures=renderer.TextureCount;long bytes=renderer.TextureBytes;
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,1);
                        if(view.WallSections.Count(s=>s.RequestedCut)<2)throw new InvalidOperationException("Cutaway did not include neighboring walls.");
                        var hit=view.WallSections.Single(s=>s.Key==view.LastWall);
                        foreach(var section in view.WallSections) {
                            bool nearby=section.Story==hit.Story&&Vector2.DistanceSquared(section.Center,hit.Center)<=TS1LotRenderData.WallCutawayRadiusTiles*TS1LotRenderData.WallCutawayRadiusTiles;
                            if(section.RequestedCut!=nearby)throw new InvalidOperationException("Cutaway radius crossed its world/story boundary.");
                        }
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,50);
                        if(!view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Actively hovered wall expired.");
                        // Moving onto unoccupied ground releases every requested cut.
                        var ground=new Vector2(-10000,-10000);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,ground,camera,640,360,60);
                        if(view.LastWall!=null||view.WallSections.Any(s=>s.RequestedCut))throw new InvalidOperationException("Room history retained cuts off-wall.");
                        int revision=view.WallRevision;
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,ground,camera,640,360,60.5);
                        if(!view.WallSections.Any(s=>s.Cut)||view.WallRevision!=revision)throw new InvalidOperationException("Wall restored before its delay.");
                        if(!lot.UpdateWalls(view,TS1WallMode.Cutaway,ground,camera,640,360,60+TS1LotRenderData.WallRestoreDelaySeconds))
                            throw new InvalidOperationException("Stationary pointer did not expire wall cuts.");
                        if(view.WallSections.Any(s=>s.Cut)||!baseline.SequenceEqual(draw())||renderer.HiddenAttachmentCount!=0)
                            throw new InvalidOperationException("Expired walls/caps/attachments did not restore original GPU output.");
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,70);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,null,camera,640,360,71);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,71.5);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,72);
                        if(!view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Re-entered wall expired on old deadline.");
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,null,camera,640,360,73);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,null,camera,640,360,74);
                        if(view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Disabled pointer kept cuts.");
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,75);
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,otherHover,camera,640,360,75.1);
                        string secondWall=view.LastWall;
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,otherHover,camera,640,360,76);
                        if(view.WallSections.Any(s=>s.Cut!=s.RequestedCut)||view.WallSections.Single(s=>s.Key==firstWall).Cut||!view.WallSections.Any(s=>s.Cut&&s.Key==secondWall))
                            throw new InvalidOperationException("Previous wall stayed hidden while pointing at a different wall.");
                        lot.UpdateWalls(view,TS1WallMode.Down,null,camera,640,360,80);
                        lot.UpdateWalls(view,TS1WallMode.Down,null,camera,640,360,90);
                        if(!view.WallSections.Any(s=>s.Cut)||view.WallSections.Any(s=>s.Cut&&s.Story!=level-1))
                            throw new InvalidOperationException("Down mode changed or cut lower story.");
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,null,camera,640,360,91);
                        if(view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Down cuts leaked into cutaway.");
                        lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360,92);
                        lot.UpdateWalls(view,TS1WallMode.Up,hover,camera,640,360,92.1);
                        if(view.WallSections.Any(s=>s.Cut)||!baseline.SequenceEqual(draw()))throw new InvalidOperationException("Up did not cancel grace period.");
                        if(renderer.TextureCount!=textures||renderer.TextureBytes!=bytes)throw new InvalidOperationException("Timed restore allocated GPU textures.");
                    }
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static void WallModes(GamePaths paths)
        {
            foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
            for(int r=0;r<4;r++)for(int level=1;level<=3;level++) {
                var view=lot.Build(1,r,level);
                var camera=TS1LotRenderer.Camera(lot.Size,1,r,640,360,Vector2.Zero);
                lot.UpdateWalls(view,TS1WallMode.Down,null,camera,640,360);
                if(view.WallSections.Any(s=>s.Cut!=(level!=3&&s.Story==level-1)))throw new InvalidOperationException("Cut crossed story/roof boundary.");
                foreach(var section in view.WallSections) {
                    if(section.Low.Length!=6||section.Top.Length%6!=0||section.LowTop.Length%6!=0||section.Reveals.Length%6!=0)throw new InvalidOperationException("Missing wall thickness/stub.");
                    if(section.Top.Length>0&&!section.Top.Any(v=>Math.Abs(v.Position.Z-section.Top[0].Position.Z)>.001f))throw new InvalidOperationException("Wall cap has no thickness.");
                    if(section.Low[2].TextureCoordinate.Y<=0.8f)throw new InvalidOperationException("Low wall texture stretched.");
                }
                lot.UpdateWalls(view,TS1WallMode.Up,null,camera,640,360);
                if(view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Up did not restore walls.");
                if(level==3)continue;
                var hover=FindWallHover(lot,view,camera,640,360);
                lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360);
                if(!view.WallSections.Any(s=>s.Cut))throw new InvalidOperationException("Wall hover did not cut.");
                int revision=view.WallRevision;
                if(lot.UpdateWalls(view,TS1WallMode.Cutaway,hover,camera,640,360)||view.WallRevision!=revision)throw new InvalidOperationException("Stationary hover rebuilt wall geometry.");
                if(view.WallSections.Any(s=>s.Cut&&s.Story!=level-1))throw new InvalidOperationException("Hover cut lower story.");
            }
        }
        private static void WallModeGpu(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {
                foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house)) {
                    var view=lot.Build(2,0,2);var camera=TS1LotRenderer.Camera(lot.Size,2,0,640,360,Vector2.Zero);
                    using(var renderer=new TS1LotRenderer(device,view,effect))
                    using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                        int textures=renderer.TextureCount,capacity=renderer.WallGeometryCapacity;long bytes=renderer.TextureBytes;Color[] baseline=null,down=null;
                        foreach(var mode in new[]{TS1WallMode.Up,TS1WallMode.Down,TS1WallMode.Cutaway,TS1WallMode.Up}) {
                            lot.UpdateWalls(view,mode,new Vector2(320,180),camera,640,360);
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);
                            renderer.Draw(camera,mode);device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[640*360];target.GetData(pixels);
                            if(mode==TS1WallMode.Up) {
                                if(baseline==null)baseline=pixels;
                                else if(!baseline.SequenceEqual(pixels))throw new InvalidOperationException("Restored walls differ after pointer/mode cycle.");
                                if(renderer.CutWallCount!=0||renderer.HiddenAttachmentCount!=0)throw new InvalidOperationException("Stale cut attachments.");
                            } else if(mode==TS1WallMode.Down) {
                                down=pixels;
                                if(renderer.CutWallCount==0||renderer.HiddenAttachmentCount==0)throw new InvalidOperationException("Walls/windows did not cut.");
                                if(baseline.SequenceEqual(down))throw new InvalidOperationException("Wall mode did not change GPU output.");
                            }
                            if(renderer.WallCapVertexCount==0||renderer.TextureCount!=textures||renderer.TextureBytes!=bytes||renderer.WallGeometryCapacity!=capacity)throw new InvalidOperationException("Missing caps or textures reallocated on hover.");
                        }
                        // Moving the cursor only changes CPU visibility; textures remain owned once.
                        for(int i=0;i<80;i++) {
                            lot.UpdateWalls(view,TS1WallMode.Cutaway,new Vector2(100+i*5,100+i%10*12),camera,640,360);
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                            renderer.Draw(camera,TS1WallMode.Cutaway);
                            if(renderer.TextureCount!=textures||renderer.TextureBytes!=bytes||renderer.WallGeometryCapacity!=capacity)throw new InvalidOperationException("Hover reallocated GPU resources or geometry.");
                        }
                        device.SetRenderTargets(previous);device.Viewport=viewport;
                    }
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference[] DisposedSessions(GamePaths paths)
        {
            var iff=new FSO.Files.Formats.IFF.IffFile(new NeighborhoodStore(paths,0).GetReadPath("Houses/House02.iff"));
            var content=new FSO.Content.TS1.TS1ObjectProvider(paths);
            using(var first=TS1LotObjectSession.Load(iff,content))using(var second=TS1LotObjectSession.Load(iff,content)) {
                var bhav=first.VM.Entities.SelectMany(e=>e.Object.Resource.List<FSO.Files.Formats.IFF.Chunks.BHAV>()).First();
                var a=first.VM.Assemble(bhav);var b=second.VM.Assemble(bhav);
                if(!ReferenceEquals(a.VM,first.VM)||!ReferenceEquals(b.VM,second.VM)||ReferenceEquals(a,b))
                    throw new InvalidOperationException("Routine cache crossed VM ownership.");
                VM.BHAVChanged(bhav);
                var changed=first.VM.Assemble(bhav);
                if(ReferenceEquals(a,changed)||changed.RuntimeVer!=bhav.RuntimeVer||!second.VM.BHAVDirty)
                    throw new InvalidOperationException("Routine edit invalidation failed.");
                return new[]{new WeakReference(first.VM),new WeakReference(second.VM),new WeakReference(content),new WeakReference(bhav)};
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference[] DisposedGeometry(GamePaths paths)
        {
            var references=new List<WeakReference>();
            foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house)) {
                references.Add(new WeakReference(lot));
                for(int r=0;r<4;r++)for(int level=1;level<=3;level++) {
                    var view=lot.Build(1,r,level);references.Add(new WeakReference(view));
                    references.Add(new WeakReference(view.WallMaterials.First().Material.Pixels));
                    references.Add(new WeakReference(view.WallSections.First(s=>s.Reveals.Length>0).Reveals));
                }
            }
            return references.ToArray();
        }
        private static void ReleasedLots(GamePaths paths)
        {
            var references=DisposedSessions(paths).Concat(DisposedGeometry(paths)).ToArray();
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            if(references.Any(r=>r.IsAlive))throw new InvalidOperationException("Disposed VM/content retained by process-wide roots.");
        }
        private static void SharedSprites(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            using(var lot=new TS1LotRenderData(paths,28)) {
                var data=lot.Build(3,0,1);
                long before=data.Sprites.Sum(s=>(long)s.Layer.Pixels.Length*5);
                long after=data.Sprites.Select(s=>s.Layer.Pixels).Distinct().Sum(p=>(long)p.Length*5);
                if(after>=before*.8)throw new InvalidOperationException("Repeated sprites still allocate per-instance pixels.");
                var previous=device.GetRenderTargets();var viewport=device.Viewport;
                using(var target=new RenderTarget2D(device,640,360,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                    Color[] baseline=null;
                    for(int repeat=0;repeat<3;repeat++) {
                        var renderer=new TS1LotRenderer(device,data,effect);
                        try {
                            int expected=data.Sprites.Select(s=>s.Layer.Pixels).Distinct().Count()+data.Sprites.Select(s=>s.Layer.Depth).Distinct().Count()+
                                data.TerrainMaterials.Concat(data.FloorMaterials).Concat(data.WallMaterials).Concat(data.RoofMaterials).Select(s=>s.Material.Pixels).Distinct().Count();
                            if(renderer.TextureCount!=expected)throw new InvalidOperationException("Duplicate GPU texture uploads.");
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);
                            renderer.Draw(TS1LotRenderer.Camera(lot.Size,3,0,640,360,Vector2.Zero),true);
                            device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[640*360];target.GetData(pixels);
                            if(pixels.Count(p=>p!=new Color(16,24,39))<10000 || (baseline!=null&&!pixels.SequenceEqual(baseline)))
                                throw new InvalidOperationException("Recreated shared textures changed the rendered view.");
                            baseline=pixels;
                        } finally {device.SetRenderTargets(previous);device.Viewport=viewport;renderer.Dispose();}
                        if(renderer.TextureCount!=0)throw new InvalidOperationException("Disposed renderer retained textures.");
                    }
                }
            }
        }
        private static void PoolLadderAttachments(GamePaths paths)
        {
            foreach(Direction direction in new[]{Direction.NORTH,Direction.EAST,Direction.SOUTH,Direction.WEST}) {
                var floors=Enumerable.Repeat(new FSO.LotView.Model.FloorTile{Pattern=12},25).ToArray();
                int dx=direction==Direction.EAST?1:direction==Direction.WEST?-1:0;
                int dy=direction==Direction.SOUTH?1:direction==Direction.NORTH?-1:0;
                floors[12].Pattern=65535;floors[(2+dy)*5+2+dx].Pattern=65535;
                var delta=TS1LotRenderData.PoolLadderOffset(floors,5,2,2,direction);
                if(delta!=new Vector2(-dx,-dy))throw new InvalidOperationException("Wrong pool attachment direction.");
                floors[12].Pattern=12;
                if(TS1LotRenderData.PoolLadderOffset(floors,5,2,2,direction)!=Vector2.Zero)
                    throw new InvalidOperationException("Valid deck attachment moved.");
                floors[12].Pattern=65535;floors[(2-dy)*5+2-dx].Pattern=0;
                if(TS1LotRenderData.PoolLadderOffset(floors,5,2,2,direction)!=Vector2.Zero)
                    throw new InvalidOperationException("Unattached pool object moved onto grass.");
            }
            foreach(int house in new[]{2,28}) {
                var store=new NeighborhoodStore(paths,0);var iff=new FSO.Files.Formats.IFF.IffFile(store.GetReadPath("Houses/House"+house.ToString("00")+".iff"));
                using(var saved=TS1LotObjectSession.Load(iff,new FSO.Content.TS1.TS1ObjectProvider(paths)))using(var lot=new TS1LotRenderData(paths,house)) {
                    var decks=saved.VM.Entities.Where(e=>e.Object.OBJ.GUID==0x4208E20Bu).ToArray();
                    if(decks.Length!=(house==2?3:2))throw new InvalidOperationException("Missing pool ladders.");
                    for(int zoom=1;zoom<=3;zoom++)for(int rotation=0;rotation<4;rotation++) {
                        var view=lot.Build(zoom,rotation,1);
                        if(view.PoolAttachmentAdjustments!=(house==28?1:0))throw new InvalidOperationException("Unexpected ladder adjustment count.");
                        foreach(var deck in decks) {
                            var delta=TS1LotRenderData.PoolLadderOffset(saved.VM.Context.Architecture.Floors[0],lot.Size,deck.Position.TileX,deck.Position.TileY,deck.Direction);
                            var expected=house==28&&deck.ObjectID==201?Vector2.UnitX:Vector2.Zero;
                            if(delta!=expected)throw new InvalidOperationException("Unrelated ladder moved.");
                            foreach(var part in deck.MultitileGroup.Objects.Where(e=>e.Object.OBJ.BaseGraphicID!=0)) {
                                var before=part.Position;var layer=TS1SpriteLayer.Read(part,zoom,rotation).Single();
                                var world=new Vector3(before.x/16f+delta.X,before.y/16f+delta.Y,0);
                                var sprite=view.Sprites.Single(s=>s.ObjectID==part.ObjectID);
                                if(Vector2.Distance(sprite.Position,TS1SpriteLayer.Project(world,zoom,rotation)+layer.Offset)>.01f)
                                    throw new InvalidOperationException("Pool ladder parts separated after rotation.");
                                if(part.Position!=before)throw new InvalidOperationException("Saved ladder placement changed.");
                            }
                        }
                    }
                }
            }
        }
        private static void TerrainMaterial(GamePaths paths)
        {
            var store=new NeighborhoodStore(paths,0);var iff=new FSO.Files.Formats.IFF.IffFile(store.GetReadPath("Houses/House02.iff"));
            var grass=iff.Get<FSO.Files.Formats.IFF.Chunks.ARRY>(6).TransposeData;var before=(byte[])grass.Clone();
            var a=new FSO.Content.TS1.TS1MaterialProvider(paths,iff).Terrain(grass,56);
            var b=new FSO.Content.TS1.TS1MaterialProvider(paths,iff).Terrain(grass,56);
            if(a.Width!=1792||a.Height!=1792||a.Pixels.Any(p=>p.A!=255)||a.Pixels.Select(p=>p.PackedValue).Distinct().Count()<30)
                throw new InvalidOperationException("Terrain lacks opaque grass detail.");
            if(!a.Pixels.SequenceEqual(b.Pixels)||!grass.SequenceEqual(before))throw new InvalidOperationException("Terrain changed saved data or changed on reload.");
            bool rejected=false;try{new FSO.Content.TS1.TS1MaterialProvider(paths,iff).Terrain(new byte[10],56);}catch(System.IO.InvalidDataException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Truncated grass map accepted.");
        }
        private static void WaterMaterials(GamePaths paths)
        {
            var store=new NeighborhoodStore(paths,0);var iff=new FSO.Files.Formats.IFF.IffFile(store.GetReadPath("Houses/House02.iff"));
            var provider=new FSO.Content.TS1.TS1MaterialProvider(paths,iff);
            var floors=Enumerable.Repeat(new FSO.LotView.Model.FloorTile{Pattern=65535},9).ToArray();
            floors[4]=new FSO.LotView.Model.FloorTile();
            if(TS1LotRenderData.WaterNeighbors(floors,3,1,0,65535)!=108 || TS1LotRenderData.WaterNeighbors(floors,3,0,0,65535)!=20)
                throw new InvalidOperationException("Pool island/boundary adjacency is incorrect.");
            if(TS1LotRenderData.WaterNeighbors(floors,3,1,0,65534)!=0)throw new InvalidOperationException("Pond and pool regions were joined.");
            foreach(ushort pattern in new ushort[]{65534,65535}) {
                // All concave/convex neighbor combinations must decode without
                // introducing transparent holes or missing corner resources.
                for(int mask=0;mask<256;mask++) {
                    var m=provider.Water(pattern,(byte)mask,0);
                    if(m.Pixels.Any(p=>p.A!=255))throw new InvalidOperationException("Empty water interior.");
                }
                if(provider.Water(pattern,0,0).Pixels.SequenceEqual(provider.Water(pattern,255,0).Pixels))
                    throw new InvalidOperationException("Water borders were not selected.");
            }
            // A single northern neighbor moves through four authored edge
            // variants. Check the source pixels independently of the UV mapper.
            var source=new FSO.Files.Formats.IFF.IffFile(paths.GetGameDataPath("GameData/floors.iff"));
            int[] variants={1,8,4,2},px={63,64,63,62},py={31,32,32,32};
            for(int rotation=0;rotation<4;rotation++) {
                var frame=source.Get<FSO.Files.Formats.IFF.Chunks.SPR2>((ushort)(0x420+variants[rotation])).Frames[0];frame.DecodeIfRequired();
                var expected=frame.PixelData[py[rotation]*127+px[rotation]];expected.A=255;
                Expect(provider.Water(65535,1,rotation).Pixels[31*64+31],expected);
            }
        }
        private static void WaterViews(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {
                foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house)) {
                    int pool=-1,water=-1;
                    for(int zoom=1;zoom<=3;zoom++)for(int rotation=0;rotation<4;rotation++) {
                        var view=lot.Build(zoom,rotation,1);
                        if(view.TerrainTiles!=lot.Size*lot.Size || view.TerrainMaterials.Count!=1 || view.PoolTiles==0 || (house==28&&view.WaterTiles==0))
                            throw new InvalidOperationException("Saved terrain/water coverage missing.");
                        if(pool>=0&&(pool!=view.PoolTiles||water!=view.WaterTiles))throw new InvalidOperationException("Camera changed water coverage.");
                        pool=view.PoolTiles;water=view.WaterTiles;
                        var tile=view.FloorMaterials.First(s=>s.Material.Name.StartsWith("water:65535:"));
                        var isolated=new TS1LotRenderData.View();isolated.FloorMaterials.Add(tile);
                        var min=new Vector2(tile.Vertices.Min(v=>v.Position.X),tile.Vertices.Min(v=>v.Position.Y));
                        var max=new Vector2(tile.Vertices.Max(v=>v.Position.X),tile.Vertices.Max(v=>v.Position.Y));
                        float scale=Math.Min(220/(max.X-min.X),160/(max.Y-min.Y));
                        var center=(min+max)/2;
                        var camera=Matrix.CreateTranslation(-center.X,-center.Y,0)*Matrix.CreateScale(scale,scale,1)*
                            Matrix.CreateTranslation(128,96,0)*Matrix.CreateOrthographicOffCenter(0,256,192,0,-128,128);
                        using(var renderer=new TS1LotRenderer(device,isolated,effect))using(var target=new RenderTarget2D(device,256,192,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Magenta,1,0);
                            renderer.Draw(camera,true);device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[256*192];target.GetData(pixels);
                            if(pixels.Count(p=>p!=Color.Magenta)<1500 || pixels.Where(p=>p!=Color.Magenta).Select(p=>p.PackedValue).Distinct().Count()<20)
                                throw new InvalidOperationException("Pool GPU view lacks authored detail.");
                        }
                    }
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static void OpeningMasks(GamePaths paths)
        {
            var store=new NeighborhoodStore(paths,0);
            var iff=new FSO.Files.Formats.IFF.IffFile(store.GetReadPath("Houses/House02.iff"));
            var provider=new FSO.Content.TS1.TS1MaterialProvider(paths,iff);
            var wall=provider.Wall(1);
            var door=provider.WithOpening(wall,provider.OpeningMask(3),0,false,"door-fixture");
            if(door.Pixels[(door.Height/2)*door.Width+door.Width/2].A!=0 || door.Pixels[(door.Height/20)*door.Width+door.Width/2].A==0)
                throw new InvalidOperationException("Door opening/header not preserved.");
            using(var lot=new TS1LotRenderData(paths,2)) {
                var view=lot.Build(3,0,2);
                var windows=view.WallMaterials.Where(s=>s.Material.Name.Contains(":opening:")&&!s.Material.Name.Contains(":opening:global:")).Select(s=>s.Material).ToArray();
                if(view.OpeningEdges!=79 || windows.Length==0)throw new InvalidOperationException("Saved opening placements not mapped.");
                if(!windows.Any(m=>m.Pixels[(m.Height/2)*m.Width+m.Width/2].A==0 && m.Pixels[(m.Height-1)*m.Width+m.Width/2].A!=0))
                    throw new InvalidOperationException("Window cutout/sill not preserved.");
            }
        }
        private static void OpeningFixture(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var store=new NeighborhoodStore(paths,0);var iff=new FSO.Files.Formats.IFF.IffFile(store.GetReadPath("Houses/House02.iff"));
            var provider=new FSO.Content.TS1.TS1MaterialProvider(paths,iff);
            var source=new FSO.Content.TS1.TS1MaterialProvider.Material{Name="red-wall",Width=64,Height=240,Pixels=Enumerable.Repeat(Color.Red,64*240).ToArray()};
            var masked=provider.WithOpening(source,provider.OpeningMask(3),0,false,"gpu-door");
            var view=new TS1LotRenderData.View();var surface=new TS1LotRenderData.Surface{Material=masked};surface.Vertices.AddRange(Quad(2,Color.White));view.WallMaterials.Add(surface);
            foreach(var v in Quad(0,Color.Blue))view.Ground.Add(new VertexPositionColor(v.Position,v.Color));
            var old=device.GetRenderTargets();var viewport=device.Viewport;
            using(var renderer=new TS1LotRenderer(device,view,effect))using(var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.Depth24))try {
                device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                renderer.Draw(Matrix.CreateOrthographicOffCenter(0,64,64,0,-128,128),true);
                device.SetRenderTargets(old);device.Viewport=viewport;var pixels=new Color[4096];target.GetData(pixels);
                Expect(pixels[32*64+32],Color.Blue);Expect(pixels[2*64+32],Color.Red);
            }finally{device.SetRenderTargets(old);device.Viewport=viewport;}
        }
        private static void OpeningFrames(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            try {
                using(var lot=new TS1LotRenderData(paths,28))for(int zoom=1;zoom<=3;zoom++)for(int rotation=0;rotation<4;rotation++) {
                    var data=lot.Build(zoom,rotation,1);var isolated=new TS1LotRenderData.View();
                    // The original saved three-section window and adjacent double door.
                    var ids=rotation<2?new short[]{414,528,530,64,68}:new short[]{445,529,531,63,66};
                    foreach(var sprite in data.Sprites.Where(s=>ids.Contains(s.ObjectID))) {
                        for(int i=0;i<sprite.Layer.Pixels.Length;i++) {
                            byte a=sprite.Layer.Pixels[i].A;sprite.Layer.Pixels[i]=new Color(a,(byte)0,(byte)0,a);
                        }
                        isolated.Sprites.Add(sprite);
                    }
                    foreach(var source in data.WallMaterials) {
                        var material=source.Material;
                        var surface=new TS1LotRenderData.Surface{Material=new FSO.Content.TS1.TS1MaterialProvider.Material {
                            Name=material.Name,Width=material.Width,Height=material.Height,
                            Pixels=material.Pixels.Select(p=>new Color((byte)0,(byte)255,(byte)255,p.A)).ToArray()}};
                        for(int i=0;i<source.Vertices.Count;i+=6)for(int y=29;y<=33;y++) {
                            var a=TS1SpriteLayer.ProjectWithDepth(new Vector3(14,y,0),zoom,rotation);
                            var b=TS1SpriteLayer.ProjectWithDepth(new Vector3(14,y+1,0),zoom,rotation);
                            if(Vector3.DistanceSquared(source.Vertices[i].Position,a)<.0001f && Vector3.DistanceSquared(source.Vertices[i+1].Position,b)<.0001f)
                                surface.Vertices.AddRange(source.Vertices.Skip(i).Take(6));
                        }
                        if(surface.Vertices.Count>0)isolated.WallMaterials.Add(surface);
                    }
                    if(isolated.Sprites.Count<5 || isolated.WallMaterials.Sum(s=>s.Vertices.Count)!=30)
                        throw new InvalidOperationException("Missing saved opening/frame fixture.");
                    float scale=1<<(3-zoom);var center=TS1SpriteLayer.Project(new Vector3(14,31.5f,1.45f),zoom,rotation);
                    var camera=Matrix.CreateScale(scale,scale,1)*Matrix.CreateTranslation(256-center.X*scale,192-center.Y*scale,0)*
                        Matrix.CreateOrthographicOffCenter(0,512,384,0,-128,128);
                    using(var renderer=new TS1LotRenderer(device,isolated,effect))using(var target=new RenderTarget2D(device,512,384,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                        int baseline=0,visible=0;
                        foreach(bool walls in new[]{false,true}) {
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                            renderer.Draw(camera,walls);device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[512*384];target.GetData(pixels);int red=pixels.Count(p=>p.R>0);
                            if(walls)visible=red;else baseline=red;
                        }
                        if(baseline<500 || visible<baseline*.97f)
                            throw new InvalidOperationException("Attached frames clipped: zoom="+zoom+" rotation="+rotation+" visible="+visible+" baseline="+baseline);
                        // An unrelated opaque surface in front must still hide
                        // the attached frames: this is not an always-on-top pass.
                        float left=isolated.Sprites.Min(s=>s.Position.X)-2,right=isolated.Sprites.Max(s=>s.Position.X+s.Layer.Width)+2;
                        float top=isolated.Sprites.Min(s=>s.Position.Y)-2,bottom=isolated.Sprites.Max(s=>s.Position.Y+s.Layer.Height)+2;
                        float front=isolated.Sprites.Max(s=>s.BackNearness)+(float)Math.Sqrt(1.5)/.4f+.5f;
                        var corners=new[]{new Vector3(left,top,front),new Vector3(right,top,front),new Vector3(right,bottom,front),new Vector3(left,bottom,front)};
                        var occluded=new TS1LotRenderData.View();occluded.Sprites.AddRange(isolated.Sprites);
                        foreach(int i in new[]{0,1,2,0,2,3})occluded.Ground.Add(new VertexPositionColor(corners[i],Color.Cyan));
                        using(var foreground=new TS1LotRenderer(device,occluded,effect)) {
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                            foreground.Draw(camera,true);device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[512*384];target.GetData(pixels);
                            if(pixels.Count(p=>p.R>0)>baseline*.05f)throw new InvalidOperationException("Attached frames bypass foreground depth.");
                        }
                    }
                }
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static Vector2 WallUV(TS1LotRenderData.WallSection wall,Vector3 point)
        {
            var top=wall.Full[3];var along=wall.Full[2]-top;var down=wall.Full[0]-top;
            float u=(point.X-top.X)/along.X;
            return new Vector2(u,(point.Y-top.Y-u*along.Y)/down.Y);
        }
        private static float ProjectedHeight(Vector3 p,int zoom)
        {
            float w=16*(1<<(zoom-1)),axis=(float)Math.Sqrt(3.0/8);
            return (p.Z/(2*axis)-p.Y/w)/((float)Math.Sqrt(1.5)+1/(4*axis));
        }
        private static void WallThicknessGeometry(GamePaths paths)
        {
            int openings=0,thresholds=0;
            foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
            for(int zoom=1;zoom<=3;zoom++)for(int r=0;r<4;r++)for(int level=1;level<=2;level++) {
                var view=lot.Build(zoom,r,level);
                int vertices=0;
                foreach(var wall in view.WallSections) {
                    var mat=wall.Surface.Material;
                    var arrays=new[]{wall.Top,wall.LowTop,wall.EndFaceA,wall.EndFaceB,wall.LowEndA,wall.LowEndB,wall.Reveals,wall.LowReveals};
                    vertices+=arrays.Sum(a=>a.Length);
                    if(level==2&&wall.Story==0 && arrays.SelectMany(a=>a).Any(v=>ProjectedHeight(v.Position,zoom)>TS1LotRenderData.StoryHeight-.005f))
                        throw new InvalidOperationException("Lower wall thickness penetrates the upper floor.");
                    foreach(var cap in arrays.Take(6))for(int i=0;i<cap.Length;i+=6)
                    for(int sample=0;sample<20;sample++) {
                        var uv=WallUV(wall,Vector3.Lerp(cap[i].Position,cap[i+1].Position,(sample+.5f)/20));
                        int x=Math.Min(mat.Width-1,Math.Max(0,(int)(uv.X*mat.Width+.0001f)));
                        int y=Math.Min(mat.Height-1,Math.Max(0,(int)(uv.Y*mat.Height+.0001f)));
                        if(mat.Pixels[y*mat.Width+x].A<128)throw new InvalidOperationException("Opaque thickness bridges an opening.");
                    }
                    if(mat.Pixels.Any(p=>p.A<128)) {
                        openings++;
                        if(wall.Reveals.Length==0)throw new InvalidOperationException("Opening lacks inner depth.");
                        if(mat.Pixels.Skip((mat.Height-1)*mat.Width).Any(p=>p.A<128)) {
                            thresholds++;
                            if(wall.LowTop.Length==6)throw new InvalidOperationException("Door threshold has a continuous bar.");
                        }
                        float minV=wall.Low[2].TextureCoordinate.Y;
                        if(wall.LowReveals.Where((v,i)=>i%6<2).Any(v=>WallUV(wall,v.Position).Y<minV-.0001f))
                            throw new InvalidOperationException("Cut wall retained an upper reveal.");
                    }
                }
                if(vertices>TS1LotRenderer.WallGeometryVertexBudget*2)throw new InvalidOperationException("Wall geometry exceeded its bounded budget.");
            }
            if(openings<100 || thresholds<30)throw new InvalidOperationException("Opening geometry fixtures did not cover real lots.");
        }
        private static void FenceFaces(GamePaths paths)
        {
            foreach(int house in new[]{2,28}) {
                var iff=new FSO.Files.Formats.IFF.IffFile(new NeighborhoodStore(paths,0).GetReadPath("Houses/House"+house.ToString("00")+".iff"));
                var provider=new FSO.Content.TS1.TS1MaterialProvider(paths,iff);
                foreach(var pair in new[]{new[]{2,248},new[]{12,249},new[]{13,250},new[]{14,251}}) {
                    var canonical=provider.Wall((ushort)pair[1],(ushort)pair[0]);
                    foreach(ushort pattern in new ushort[]{0,4,5,21,250,251})
                        if(!ReferenceEquals(canonical,provider.Wall(pattern,(ushort)pair[0])))
                            throw new InvalidOperationException("Fence reverse face inherited room wallpaper.");
                }
                using(var lot=new TS1LotRenderData(paths,house))for(int r=0;r<4;r++) {
                    var data=lot.Build(2,r,2);
                    if(data.WallMaterials.Where(s=>s.KeepWhenWallsHidden).Any(s=>s.Material.Name!="wall:248"&&s.Material.Name!="wall:249"&&s.Material.Name!="wall:250"&&s.Material.Name!="wall:251"))
                        throw new InvalidOperationException("Persistent railing contains a wallpaper material.");
                }
            }
        }
        private static void ThickOpeningGpu(GraphicsDevice device,GamePaths paths,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;int tested=0;
            try {
                foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
                for(int zoom=1;zoom<=3;zoom++)for(int r=0;r<4;r++) {
                    var data=lot.Build(zoom,r,1);
                    foreach(bool door in new[]{false,true}) {
                        var source=data.WallSections.First(w=> {
                            var m=w.Surface.Material;
                            return m.Pixels[(m.Height/2)*m.Width+m.Width/2].A<128 &&
                                (m.Pixels[(m.Height-1)*m.Width+m.Width/2].A<128)==door;
                        });
                        var mat=source.Surface.Material;
                        var face=new TS1LotRenderData.Surface {Material=mat};
                        face.Vertices.AddRange(source.Surface.Vertices.Skip(source.Offset).Take(6));
                        source.Surface=face;source.Offset=0;
                        var isolated=new TS1LotRenderData.View {Level=1,Zoom=zoom,Rotation=r};
                        isolated.WallMaterials.Add(face);isolated.WallSections.Add(source);
                        var center=(source.Full[0]+source.Full[2])/2;
                        var camera=Matrix.CreateOrthographicOffCenter(center.X-80,center.X+80,center.Y+120,center.Y-120,-128,128);
                        using(var renderer=new TS1LotRenderer(device,isolated,effect))
                        using(var target=new RenderTarget2D(device,320,480,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                            foreach(var mode in new[]{TS1WallMode.Up,TS1WallMode.Down,TS1WallMode.Up}) {
                                lot.UpdateWalls(isolated,mode,null,camera,320,480);
                                device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Lime,1,0);
                                renderer.Draw(camera,mode);device.SetRenderTargets(previous);device.Viewport=viewport;
                                var pixels=new Color[320*480];target.GetData(pixels);
                                float v=door?.98f:.5f;
                                var point=source.Full[3]+(source.Full[2]-source.Full[3])*.5f+(source.Full[0]-source.Full[3])*v;
                                var clip=Vector3.Transform(point,camera);
                                int px=(int)((clip.X+1)*160),py=(int)((1-clip.Y)*240);
                                if(pixels[py*320+px]!=Color.Lime)throw new InvalidOperationException("Wall thickness blocked a transparent opening/door threshold.");
                                if(renderer.WallCapVertexCount==0)throw new InvalidOperationException("GPU fixture omitted thickness.");
                            }
                        }
                        tested++;
                    }
                }
                if(tested!=48)throw new InvalidOperationException("Missing thick opening rotation/zoom fixtures.");
            }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
        }
        private static void StoryJoints(GamePaths paths)
        {
            using(var lot=new TS1LotRenderData(paths,2))for(int z=1;z<=3;z++)for(int r=0;r<4;r++) {
                var view=lot.Build(z,r,2);
                if(view.StoryJoints<30)throw new InvalidOperationException("Missing adjoining wall seams.");
                var floor=TS1SpriteLayer.ProjectWithDepth(new Vector3(22,31,2.95f),z,r);
                if(!view.FloorMaterials.Any(s=>s.Vertices.Any(v=>Vector3.DistanceSquared(v.Position,floor)<.0001f)))
                    throw new InvalidOperationException("Upper floor does not share the story elevation.");
            }
        }
        private static void RoofBitmap()
        {
            byte[] bytes;
            using(var memory=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(memory)) {
                writer.Write((ushort)0x4d42);writer.Write(74);writer.Write(0);writer.Write(62);writer.Write(40);
                writer.Write(4);writer.Write(2);writer.Write((ushort)1);writer.Write((ushort)8);writer.Write(1);writer.Write(12);
                writer.Write(0);writer.Write(0);writer.Write(2);writer.Write(2);
                writer.Write(new byte[]{255,0,0,0,0,0,255,0});
                writer.Write(new byte[]{0,4,1,1,1,0,0,0,4,0,0,1});bytes=memory.ToArray();
            }
            Func<byte[],FSO.Content.TS1.TS1MaterialProvider.Material> decode=b=> {
                using(var stream=new System.IO.MemoryStream(b,false))return FSO.Content.TS1.TS1MaterialProvider.DecodeRoofBitmap(stream,"fixture");
            };
            var roof=decode(bytes);
            if(roof.Width!=4||roof.Height!=2||!roof.Pixels.SequenceEqual(new[]{Color.Blue,Color.Blue,Color.Blue,Color.Blue,Color.Red,Color.Red,Color.Red,Color.Blue}))
                throw new InvalidOperationException("RLE8 run/absolute rows or palette decoded incorrectly.");
            var odd=(byte[])bytes.Clone();new byte[]{0,3,1,0,1,0,0,0,4,1,0,1}.CopyTo(odd,62);
            if(!decode(odd).Pixels.SequenceEqual(new[]{Color.Red,Color.Red,Color.Red,Color.Red,Color.Red,Color.Blue,Color.Red,Color.Blue}))
                throw new InvalidOperationException("RLE8 absolute padding decoded incorrectly.");
            var overflow=(byte[])bytes.Clone();overflow[62]=5;overflow[63]=1;
            var delta=(byte[])bytes.Clone();new byte[]{0,2,3,1,2,1,0,1}.CopyTo(delta,62);
            foreach(var invalid in new[]{overflow,delta,bytes.Take(bytes.Length-2).ToArray()}) {
                bool rejected=false;try{decode(invalid);}catch(System.IO.InvalidDataException){rejected=true;}
                if(!rejected)throw new InvalidOperationException("Invalid roof RLE8 accepted.");
            }
        }
        private static void RoofGeometry(GamePaths paths)
        {
            var rectangle=TS1RoofMesh.Build(Enumerable.Repeat(true,32).ToArray(),8,4,5.9f,.66f);
            if(rectangle.Count!=6 || Math.Abs(rectangle.SelectMany(t=>t).Max(p=>p.Z)-6.56f)>.001f)
                throw new InvalidOperationException("Rectangular roof must have a straight ridge and planar hips.");
            foreach(var t in rectangle) {
                var n=Vector3.Normalize(Vector3.Cross(t[1]-t[0],t[2]-t[0]));
                if(Math.Min(Math.Abs(n.X),Math.Abs(n.Y))>.0001f)throw new InvalidOperationException("Rectangular roof has a twisted face.");
            }
            var footprint=new bool[64];
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)footprint[y*8+x]=x<2||y<2||x>=6||y>=6;
            var courtyard=TS1RoofMesh.Build(footprint,8,8,5.9f,.66f);
            foreach(var t in courtyard) {
                var center=(t[0]+t[1]+t[2])/3;
                if(center.X>1&&center.X<3&&center.Y>1&&center.Y<3)throw new InvalidOperationException("Roof covers open courtyard.");
            }
            if(courtyard.Count==0)throw new InvalidOperationException("Courtyard roof is missing.");
            using(var lot=new TS1LotRenderData(paths,2)) {
                var px=TS1SpriteLayer.ProjectWithDepth(Vector3.UnitX,3,0);
                var py=TS1SpriteLayer.ProjectWithDepth(Vector3.UnitY,3,0);
                var pz=TS1SpriteLayer.ProjectWithDepth(Vector3.UnitZ,3,0);
                var inverse=Matrix.Invert(new Matrix(px.X,px.Y,px.Z,0,py.X,py.Y,py.Z,0,pz.X,pz.Y,pz.Z,0,0,0,0,1));
                var vertices=lot.Build(3,0,3).RoofMaterials.SelectMany(s=>s.Vertices).Select(v=>Vector3.Transform(v.Position,inverse)).ToArray();
                Func<float,float,bool> covers=(x,y)=> {
                    for(int i=0;i<vertices.Length;i+=3) {
                        var a=vertices[i];var b=vertices[i+1];var c=vertices[i+2];
                        float ab=(b.X-a.X)*(y-a.Y)-(b.Y-a.Y)*(x-a.X);
                        float bc=(c.X-b.X)*(y-b.Y)-(c.Y-b.Y)*(x-b.X);
                        float ca=(a.X-c.X)*(y-c.Y)-(a.Y-c.Y)*(x-c.X);
                        if(Math.Min(ab,Math.Min(bc,ca))>=-.0001f || Math.Max(ab,Math.Max(bc,ca))<=.0001f)return true;
                    }
                    return false;
                };
                if(!covers(17.2f,17.8f))throw new InvalidOperationException("Room seed left a triangular roof hole.");
                if(covers(35.5f,15.5f))throw new InvalidOperationException("Pool island was mistaken for an enclosed room.");
                if(!covers(23.2f,22.8f) || !covers(15.75f,30.25f) || covers(15.25f,30.25f))throw new InvalidOperationException("Saved roof/eave coverage is incorrect.");
            }
        }
        private static void StairHandrails(GamePaths paths)
        {
            using(var lot=new TS1LotRenderData(paths,2)) {
                for(int zoom=1;zoom<=3;zoom++) for(int rotation=0;rotation<4;rotation++) {
                    var lower=lot.Build(zoom,rotation,1);
                    var upper=lot.Build(zoom,rotation,2);
                    foreach(short id in new short[]{101,107}) {
                        // House 2: two static layers plus the exposed-side dynamic
                        // handrail. The wall-side dynamic layer stays hidden.
                        if(lower.Sprites.Count(s=>s.ObjectID==id)!=3 || upper.Sprites.Count(s=>s.ObjectID==id)!=3)
                            throw new InvalidOperationException("Missing/extra stair handrail: "+id+" zoom="+zoom+" rotation="+rotation);
                    }
                    var repeat=lot.Build(zoom,rotation,2);
                    if(repeat.Sprites.Count!=upper.Sprites.Count) throw new InvalidOperationException("Stair view rebuild changed sprite state.");
                }
            }
        }
        private static void DiagonalFloors(GamePaths paths)
        {
            using(var lot=new TS1LotRenderData(paths,2))
            for(int zoom=1;zoom<=3;zoom++) for(int rotation=0;rotation<4;rotation++) {
                var data=lot.Build(zoom,rotation,2);
                // Actual saved carpet tiles reported in Xbox screenshots 5/6,
                // plus a genuine stairwell tile that must remain open.
                foreach(var tile in new[]{new Point(22,31),new Point(23,32),new Point(24,26)}) {
                    var corners=new[]{new Vector3(tile.X,tile.Y,2.95f),new Vector3(tile.X+1,tile.Y,2.95f),new Vector3(tile.X+1,tile.Y+1,2.95f),new Vector3(tile.X,tile.Y+1,2.95f)}
                        .Select(p=>TS1SpriteLayer.ProjectWithDepth(p,zoom,rotation)).ToArray();
                    int triangles=0;
                    foreach(var surface in data.FloorMaterials) for(int i=0;i<surface.Vertices.Count;i+=3)
                        if(Enumerable.Range(0,3).All(j=>corners.Any(p=>Vector3.DistanceSquared(p,surface.Vertices[i+j].Position)<0.000001f))) triangles++;
                    if(triangles!=(tile.X==24?0:2)) throw new InvalidOperationException("Incorrect saved floor coverage at "+tile+" zoom="+zoom+" rotation="+rotation);
                }
            }
        }
        private static void MaterialMapping(GamePaths paths)
        {
            var iff=new FSO.Files.Formats.IFF.IffFile(new FSO.Common.Platform.NeighborhoodStore(paths,0).GetReadPath("Houses/House28.iff"));
            var material=new FSO.Content.TS1.TS1MaterialProvider(paths,iff);
            if(!ReferenceEquals(material.Floor(31,false),material.Floor(287,false)))throw new InvalidOperationException("Extended floor ID did not resolve its lot map.");
            if(ReferenceEquals(material.Floor(1,true),material.Floor(1,false)))throw new InvalidOperationException("Global floor flag ignored.");
            if(material.Floor(31,false).Pixels.Select(p=>p.PackedValue).Distinct().Count()<8)throw new InvalidOperationException("Floor lacks authored texture.");
            if(material.Wall(1).Pixels.Any(p=>p.A<128))throw new InvalidOperationException("Full wall still includes transparent sprite margins.");
            var fence=material.Wall(250,13);
            if(!fence.Pixels.Any(p=>p.A==0)||!fence.Pixels.Any(p=>p.A==255))throw new InvalidOperationException("Fence cutout was lost.");
            if(fence.Pixels.Take(fence.Width*100).Any(p=>p.A!=0))throw new InvalidOperationException("Low fence mask became a full-height wall.");
            if(material.Wall(15).Name!="street_brick2.wll")throw new InvalidOperationException("Wallpaper map ignored.");
        }
        private static void MaterialFixture(GraphicsDevice device,byte[] effect)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            var data=new TS1LotRenderData.View();
            var quad=Quad(0,Color.Blue);
            data.Ground.AddRange(quad.Select(v=>new VertexPositionColor(v.Position,v.Color)));
            var wall=new TS1LotRenderData.Surface {Material=new FSO.Content.TS1.TS1MaterialProvider.Material {Width=2,Height=2,Pixels=new[]{Color.Red,Color.Transparent,Color.Lime,Color.Transparent}}};
            wall.Vertices.AddRange(Quad(1,Color.White));data.WallMaterials.Add(wall);
            foreach(bool keep in new[]{false,true}) {
                wall.KeepWhenWallsHidden=keep;
                using(var renderer=new TS1LotRenderer(device,data,effect)) using(var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                    try {
                        foreach(bool show in new[]{true,false}) {
                            device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                            renderer.Draw(Matrix.CreateOrthographicOffCenter(0,64,64,0,-128,128),show);
                            device.SetRenderTargets(previous);device.Viewport=viewport;
                            var pixels=new Color[4096];target.GetData(pixels);
                            Expect(pixels[16*64+16],(show||keep)?Color.Red:Color.Blue);Expect(pixels[16*64+48],Color.Blue);
                            Expect(pixels[48*64+16],(show||keep)?Color.Lime:Color.Blue);Expect(pixels[48*64+48],Color.Blue);
                        }
                    }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
                }
        }
        }
        private static void DepthFixture(GraphicsDevice device,byte[] bytes)
        {
            var previous=device.GetRenderTargets();var viewport=device.Viewport;
            using(var shader=new Effect(device,bytes)) using(var wall=new BasicEffect(device){VertexColorEnabled=true})
            using(var color=new Texture2D(device,2,2)) using(var depth=new Texture2D(device,2,2,false,SurfaceFormat.Alpha8))
            using(var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                try {
                    color.SetData(new[]{Color.Red,Color.Lime,Color.Transparent,new Color(0,0,128,128)});
                    depth.SetData(new byte[]{255,0,0,0});
                    var projection=Matrix.CreateOrthographicOffCenter(0,64,64,0,-128,128);
                    shader.Parameters["Projection"].SetValue(projection);shader.Parameters["DepthSpan"].SetValue((float)Math.Sqrt(1.5)/0.4f/256);
                    shader.Parameters["ColorTexture"].SetValue(color);shader.Parameters["DepthTexture"].SetValue(depth);
                    wall.Projection=projection;
                    var sprite=Quad(0,Color.White);var surface=Quad(1.5f,new Color(80,90,100));
                    Color[] first=null;
                    for(int reverse=0;reverse<2;reverse++) {
                        device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                        device.RasterizerState=RasterizerState.CullNone;device.DepthStencilState=DepthStencilState.Default;device.BlendState=BlendState.Opaque;
                        Action drawWall=()=>{foreach(var pass in wall.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,surface,0,2);}};
                        Action drawSprite=()=>{foreach(var pass in shader.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,sprite,0,2);}};
                        shader.Parameters["AlphaPass"].SetValue(0f);
                        if(reverse==0){drawWall();drawSprite();}else{drawSprite();drawWall();}
                        device.DepthStencilState=DepthStencilState.DepthRead;device.BlendState=BlendState.AlphaBlend;
                        shader.Parameters["AlphaPass"].SetValue(1f);drawSprite();
                        device.SetRenderTargets(previous);device.Viewport=viewport;
                        var pixels=new Color[64*64];target.GetData(pixels);
                        Expect(pixels[16*64+16],new Color(80,90,100));Expect(pixels[16*64+48],Color.Lime);
                        Expect(pixels[48*64+16],new Color(80,90,100));Expect(pixels[48*64+48],new Color(40,45,178));
                        if(first!=null && !first.SequenceEqual(pixels))throw new InvalidOperationException("Opaque wall/sprite submission order changed pixels.");
                        first=pixels;
                    }
                }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
            }
        }
        private static VertexPositionColorTexture[] Quad(float z,Color color)
        {
            var a=new VertexPositionColorTexture(new Vector3(0,0,z),color,new Vector2(0,0));
            var b=new VertexPositionColorTexture(new Vector3(64,0,z),color,new Vector2(1,0));
            var c=new VertexPositionColorTexture(new Vector3(64,64,z),color,new Vector2(1,1));
            var d=new VertexPositionColorTexture(new Vector3(0,64,z),color,new Vector2(0,1));return new[]{a,b,c,a,c,d};
        }
        private static void Expect(Color actual,Color expected)
        {
            if(Math.Abs(actual.R-expected.R)>2||Math.Abs(actual.G-expected.G)>2||Math.Abs(actual.B-expected.B)>2||actual.A!=255)
                throw new InvalidOperationException("GPU depth/alpha expected "+expected+" got "+actual);
        }
    }
}
