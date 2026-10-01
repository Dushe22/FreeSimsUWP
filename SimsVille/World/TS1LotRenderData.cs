using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    // Static saved architecture with TS1 material textures; no roofs or wall openings.
    public sealed class TS1LotRenderData : IDisposable
    {
        private readonly TS1LotObjectSession session;
        private readonly TS1MaterialProvider materials;
        private readonly byte[] floorFlags;
        public int Size { get { return session.VM.Context.Architecture.Width; } }
        public int ObjectCount { get { return session.SavedObjectCount; } }
        public TS1LotRenderData(GamePaths paths, int house)
        {
            if (house != 2 && house != 28) throw new ArgumentOutOfRangeException("house");
            if (VM.UseWorld) throw new InvalidOperationException("Static lot renderer requires a headless VM.");
            var store = new NeighborhoodStore(paths, 0);
            var iff = new IffFile(store.GetReadPath("Houses/House"+house.ToString("00")+".iff"));
            materials = new TS1MaterialProvider(paths, iff);
            var flags = iff.Get<ARRY>(8);
            if (flags == null || flags.Width != 64 || flags.Height != 64 || flags.ByteSize() != 1) throw new InvalidDataException("Missing lot floor flags.");
            floorFlags = flags.TransposeData;
            session = TS1LotObjectSession.Load(iff,new TS1ObjectProvider(paths));
            try {
                // OBJM loading holds behaviors and does not reconstruct dynamic sprite
                // flags. Refresh only the known TS1 stair upper-stub visual callbacks;
                // never run Init/Main or placement callbacks on saved objects.
                foreach(var stub in session.VM.Entities.Where(e=>e.Object.OBJ.GUID==0xB7F590C4u || e.Object.OBJ.GUID==0x9431BD2Au).ToArray()) {
                    int expected=stub.Object.OBJ.GUID==0xB7F590C4u?4123:4122;
                    if(stub.EntryPoints[6].ActionFunction!=expected || !stub.ExecuteEntryPoint(6,session.VM.Context,true))
                        throw new InvalidDataException("Unsupported stair visual callback: "+stub.ObjectID);
                }
            } catch {session.Dispose();throw;}
        }
        public sealed class Sprite
        {
            public TS1SpriteLayer Layer;
            public Vector2 Position;
            public float BackNearness;
            public short ObjectID;
        }
        public sealed class Surface
        {
            public TS1MaterialProvider.Material Material;
            public bool KeepWhenWallsHidden;
            public readonly List<VertexPositionColorTexture> Vertices = new List<VertexPositionColorTexture>();
        }
        public sealed class View
        {
            public readonly List<VertexPositionColor> Ground = new List<VertexPositionColor>();
            public readonly List<VertexPositionColor> Walls = new List<VertexPositionColor>();
            public readonly List<Surface> FloorMaterials = new List<Surface>();
            public readonly List<Surface> WallMaterials = new List<Surface>();
            public readonly List<Sprite> Sprites = new List<Sprite>();
            public int Rendered, Hidden, OutOfWorld, Contained, NoGraphic, Unsupported, AboveLevel, FloorTiles, WallEdges;
            public readonly List<string> Issues = new List<string>();
        }
        public View Build(int zoom,int rotation,int level)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException("level");
            TS1SpriteLayer.Project(Vector3.Zero,zoom,rotation); // validate before allocating
            var view=new View(); var arch=session.VM.Context.Architecture;
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) {
                byte green=(byte)(94+((x+y)%2)*6);
                Quad(view.Ground,new Vector3(x,y,0),new Vector3(x+1,y,0),new Vector3(x+1,y+1,0),new Vector3(x,y+1,0),new Color(56,green,44),zoom,rotation);
            }
            var camera = new[] {new Vector2(1,1),new Vector2(1,-1),new Vector2(-1,-1),new Vector2(-1,1)}[rotation];
            for(int story=0;story<level;story++) {
                var edges=new HashSet<string>(); float z=story*2.95f;
                Func<int,int,WallTile> wallAt=(xx,yy)=>xx<0||yy<0||xx>=Size||yy>=Size?default(WallTile):arch.Walls[story][yy*Size+xx];
                for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) {
                    var wall=wallAt(x,y);
                    bool global=(floorFlags[y*64+x]&0x20)!=0;
                    bool splitFloor=(wall.Segments&WallSegments.AnyDiag)!=0 && (wall.TopLeftPattern!=0 || wall.TopLeftStyle!=0);
                    var corners=new[]{new Vector3(x,y,z+.003f),new Vector3(x+1,y,z+.003f),new Vector3(x+1,y+1,z+.003f),new Vector3(x,y+1,z+.003f)};
                    var uv=new[]{Vector2.Zero,Vector2.UnitX,Vector2.One,Vector2.UnitY};
                    Action<ushort,int[]> floor=(pattern,indices)=>{
                        if(pattern==0)return;
                        if(pattern>=65534){TriangleSurface(view.Ground,corners,indices,new Color(45,125,175),zoom,rotation);}
                        else Textured(view.FloorMaterials,materials.Floor(pattern,global&&!splitFloor&&pattern<=30),corners,uv,indices,Color.White,zoom,rotation);
                        view.FloorTiles++;
                    };
                    // A diagonal wall can cross an ordinary full floor. Empty split fields do not erase it.
                    if((wall.Segments&WallSegments.AnyDiag)!=0 && !splitFloor) floor(arch.Floors[story][y*Size+x].Pattern,new[]{0,1,2,0,2,3});
                    else if((wall.Segments&WallSegments.HorizontalDiag)!=0){floor(wall.TopLeftPattern,new[]{1,2,3});floor(wall.TopLeftStyle,new[]{0,1,3});}
                    else if((wall.Segments&WallSegments.VerticalDiag)!=0){floor(wall.TopLeftPattern,new[]{0,1,2});floor(wall.TopLeftStyle,new[]{0,2,3});}
                    else floor(arch.Floors[story][y*Size+x].Pattern,new[]{0,1,2,0,2,3});
                    Action<WallSegments,int,int,int,int,ushort,ushort> edge=(flag,ax,ay,bx,by,pattern,style)=> {
                        if((wall.Segments&flag)==0) return;
                        int a=ay*(Size+1)+ax,b=by*(Size+1)+bx;
                        if(!edges.Add(Math.Min(a,b)+":"+Math.Max(a,b))) return;
                        var color=ax==bx?new Color(225,225,225):Color.White;
                        var points=new[]{new Vector3(ax,ay,z),new Vector3(bx,by,z),new Vector3(bx,by,z+2.95f),new Vector3(ax,ay,z+2.95f)};
                        Textured(view.WallMaterials,materials.Wall(pattern,style),points,new[]{Vector2.UnitY,Vector2.One,Vector2.UnitX,Vector2.Zero},new[]{0,1,2,0,2,3},color,zoom,rotation,style==2 || style==12 || style==13 || style==14);
                        view.WallEdges++;
                    };
                    edge(WallSegments.TopLeft,x,y,x,y+1,camera.X>0?wall.TopLeftPattern:wallAt(x-1,y).BottomRightPattern,wall.TopLeftStyle);
                    edge(WallSegments.TopRight,x,y,x+1,y,camera.Y>0?wall.TopRightPattern:wallAt(x,y-1).BottomLeftPattern,wall.TopRightStyle);
                    edge(WallSegments.BottomRight,x+1,y,x+1,y+1,camera.X<0?wall.BottomRightPattern:wallAt(x+1,y).TopLeftPattern,wallAt(x+1,y).TopLeftStyle);
                    edge(WallSegments.BottomLeft,x,y+1,x+1,y+1,camera.Y<0?wall.BottomLeftPattern:wallAt(x,y+1).TopRightPattern,wallAt(x,y+1).TopRightStyle);
                    edge(WallSegments.HorizontalDiag,x,y+1,x+1,y,camera.X+camera.Y>=0?wall.BottomRightPattern:wall.BottomLeftPattern,wall.TopRightStyle);
                    edge(WallSegments.VerticalDiag,x,y,x+1,y+1,camera.X-camera.Y>=0?wall.BottomRightPattern:wall.BottomLeftPattern,wall.TopRightStyle);
                }
            }
            var backOffsets=new[]{Vector3.Zero,new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0)};
            foreach(var entity in session.VM.Entities.OrderBy(e=>e.ObjectID)) {
                if(entity.Position.x<0 || entity.Position.y<0) {view.OutOfWorld++;continue;}
                if(entity.Position.Level>level) {view.AboveLevel++;continue;}
                if(entity.GetValue(VMStackObjectVariable.Hidden)!=0) {view.Hidden++;continue;}
                if(entity.Container!=null) {view.Contained++;continue;} // SLOT visual offsets are a separate rendering step
                if(entity.Object.OBJ.BaseGraphicID==0) {view.NoGraphic++;continue;}
                try {
                    var layers=TS1SpriteLayer.Read(entity,zoom,rotation);
                    if(layers.Count==0){view.NoGraphic++;continue;}
                    var world=new Vector3(entity.Position.x/16f,entity.Position.y/16f,(entity.Position.Level-1)*2.95f);
                    var point=TS1SpriteLayer.Project(world,zoom,rotation);
                    foreach(var layer in layers) view.Sprites.Add(new Sprite {
                        Layer=layer,Position=point+layer.Offset,ObjectID=entity.ObjectID,
                        BackNearness=TS1SpriteLayer.ProjectWithDepth(world-new Vector3(0.5f,0.5f,0)+backOffsets[rotation]+layer.WorldOffset,zoom,rotation).Z
                    });
                    view.Rendered++;
                } catch(Exception ex) {
                    if(!(ex is NotSupportedException) && !(ex is InvalidDataException)) throw;
                    view.Unsupported++;view.Issues.Add("OBJECT "+entity.ObjectID+" GUID="+entity.Object.OBJ.GUID.ToString("X8")+" "+ex.Message);
                }
            }
            return view;
        }
        private static void Textured(List<Surface> surfaces,TS1MaterialProvider.Material material,Vector3[] corners,Vector2[] uv,int[] indices,Color color,int zoom,int rotation,bool keepWhenWallsHidden=false)
        {
            var surface=surfaces.FirstOrDefault(x=>ReferenceEquals(x.Material,material) && x.KeepWhenWallsHidden==keepWhenWallsHidden);
            if(surface==null){surface=new Surface{Material=material,KeepWhenWallsHidden=keepWhenWallsHidden};surfaces.Add(surface);}
            foreach(int i in indices)surface.Vertices.Add(new VertexPositionColorTexture(TS1SpriteLayer.ProjectWithDepth(corners[i],zoom,rotation),color,uv[i]));
        }
        private static void TriangleSurface(List<VertexPositionColor> vertices,Vector3[] corners,int[] indices,Color color,int zoom,int rotation)
        {
            foreach(int i in indices)vertices.Add(new VertexPositionColor(TS1SpriteLayer.ProjectWithDepth(corners[i],zoom,rotation),color));
        }
        private static void Quad(List<VertexPositionColor> target,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int zoom,int rotation)
        {
            var pa=new VertexPositionColor(TS1SpriteLayer.ProjectWithDepth(a,zoom,rotation),color);
            var pb=new VertexPositionColor(TS1SpriteLayer.ProjectWithDepth(b,zoom,rotation),color);
            var pc=new VertexPositionColor(TS1SpriteLayer.ProjectWithDepth(c,zoom,rotation),color);
            var pd=new VertexPositionColor(TS1SpriteLayer.ProjectWithDepth(d,zoom,rotation),color);
            target.Add(pa);target.Add(pb);target.Add(pc);target.Add(pa);target.Add(pc);target.Add(pd);
        }
        public void Dispose() {session.Dispose();}
    }
}
