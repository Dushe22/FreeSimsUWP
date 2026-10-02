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
        public const int Count=20;
        public static List<string> Run(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var result=new List<string>();bool oldWorld=VM.UseWorld;VM.UseWorld=false;
            Action<string,Action> check=(name,action)=>{try{action();result.Add("PASS "+name);}catch(Exception ex){result.Add("FAIL "+name);log(ex.ToString());}log(result.Last());};
            try {
                check("DISPOSED LOTS RELEASE VM AND CONTENT",()=>ReleasedLots(paths));
                check("GPU SHARED SPRITES AND RESOURCE DISPOSAL",()=>SharedSprites(device,paths,effect));
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
        private static void ReleasedLots(GamePaths paths)
        {
            var references=DisposedSessions(paths);
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
