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
    // Static saved architecture with authored TS1 opening masks and roof textures.
    public sealed class TS1LotRenderData : IDisposable
    {
        private readonly TS1LotObjectSession session;
        private readonly TS1MaterialProvider materials;
        private readonly byte[] floorFlags;
        private readonly string roofName;
        private readonly VMRoomMap[] roofRooms = new VMRoomMap[2];
        private readonly List<VMRoom> roofRoomData = new List<VMRoom> {new VMRoom {IsOutside = true}};
        private readonly Dictionary<string, Opening> openings = new Dictionary<string, Opening>();
        public int Size { get { return session.VM.Context.Architecture.Width; } }
        public int ObjectCount { get { return session.SavedObjectCount; } }
        public TS1LotRenderData(GamePaths paths, int house)
        {
            if (house != 2 && house != 28) throw new ArgumentOutOfRangeException("house");
            if (VM.UseWorld) throw new InvalidOperationException("Static lot renderer requires a headless VM.");
            var store = new NeighborhoodStore(paths, 0);
            var iff = new IffFile(store.GetReadPath("Houses/House"+house.ToString("00")+".iff"));
            materials = new TS1MaterialProvider(paths, iff);
            var houseInfo = iff.Get<HOUS>(0) ?? iff.Get<HOUS>(1);
            if(houseInfo==null)throw new InvalidDataException("Missing saved house metadata.");
            roofName = houseInfo.RoofName;
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
                LoadOpenings();
                // Water separates simulation room regions; it must not enclose a roof.
                for (int story = 0; story < 2; story++) {
                    roofRooms[story] = new VMRoomMap();
                    roofRooms[story].GenerateMap(session.VM.Context.Architecture.Walls[story], new FloorTile[Size * Size], Size, Size, roofRoomData);
                }
            } catch {session.Dispose();throw;}
        }
        private sealed class Opening
        {
            public SPR Mask;
            public string Identity;
        }
        private string EdgeKey(int story, int ax, int ay, int bx, int by)
        {
            int a = ay * (Size + 1) + ax, b = by * (Size + 1) + bx;
            return story + ":" + Math.Min(a, b) + ":" + Math.Max(a, b);
        }
        private void LoadOpenings()
        {
            foreach (var entity in session.VM.Entities.OrderBy(e => e.ObjectID)) {
                var definition = entity.Object.OBJ;
                var flags = (VMEntityFlags2)entity.GetValue(VMStackObjectVariable.FlagField2);
                if ((flags & (VMEntityFlags2.ArchitectualWindow | VMEntityFlags2.ArchitectualDoor)) == 0 || definition.WallStyle == 0 || entity.Container != null) continue;
                int x = entity.Position.TileX, y = entity.Position.TileY, story = entity.Position.Level - 1;
                if (x < 0 || y < 0 || x >= Size || y >= Size || story < 0 || story > 1) continue;
                int direction = entity.Direction == Direction.NORTH ? 0 : entity.Direction == Direction.EAST ? 1 : entity.Direction == Direction.SOUTH ? 2 : entity.Direction == Direction.WEST ? 3 : -1;
                if (direction < 0) continue;
                int required = entity.GetValue(VMStackObjectVariable.WallPlacementFlags) & 15;
                for (int relative = 0; relative < 4; relative++) {
                    if ((required & (1 << relative)) == 0) continue;
                    int side = (direction + relative) % 4;
                    // The VM uses only canonical top-left/top-right segments. The
                    // other saved multitile half supplies the matching opposite side.
                    if (side != 0 && side != 3) continue;
                    var wall = session.VM.Context.Architecture.Walls[story][y * Size + x];
                    var segment = side == 0 ? WallSegments.TopRight : WallSegments.TopLeft;
                    if ((wall.Segments & segment) == 0) continue;
                    var mask = definition.WallStyle > 21 ? entity.Object.Resource.Get<SPR>((ushort)(definition.WallStyleSpriteID + 2)) : materials.OpeningMask(definition.WallStyle);
                    if (mask == null) throw new InvalidDataException("Missing wall opening mask for object " + entity.ObjectID);
                    string identity = definition.WallStyle > 21 ? entity.Object.Resource.Name + ":" + definition.WallStyleSpriteID : "global:" + definition.WallStyle;
                    string key = side == 0 ? EdgeKey(story, x, y, x + 1, y) : EdgeKey(story, x, y, x, y + 1);
                    Opening existing;
                    if (openings.TryGetValue(key, out existing) && existing.Identity != identity) throw new InvalidDataException("Conflicting wall openings at " + key);
                    openings[key] = new Opening {Mask = mask, Identity = identity};
                }
            }
        }
        private bool Indoors(int x, int y, int story)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || story > 1) return false;
            uint room = roofRooms[story].Map[y * Size + x];
            return RoomInside(room & 65535) || RoomInside(room >> 16);
        }
        private bool RoomInside(uint room)
        {
            var rooms = roofRoomData;
            return room > 0 && room < rooms.Count && !rooms[(int)room].IsOutside;
        }
        private bool Roofable(int halfX, int halfY, int story)
        {
            int x=halfX/2,y=halfY/2,dx=halfX%2==1?1:-1,dy=halfY%2==1?1:-1;
            if(x<=0||y<=0||x>=Size-1||y>=Size-1)return false;
            bool overhang=!Indoors(x,y,story);
            if(overhang && !Indoors(x+dx,y,story) && !Indoors(x,y+dy,story) && !Indoors(x+dx,y+dy,story))return false;
            // Retain the half-tile eaves of the existing roof model. Upper rooms
            // and balcony floors suppress lower roofs, including their eaves.
            if(story==1)return true;
            var floors=session.VM.Context.Architecture.Floors[1];
            Func<int,int,bool> blocked=(xx,yy)=>Indoors(xx,yy,1)||floors[yy*Size+xx].Pattern!=0;
            if(blocked(x,y))return false;
            return !overhang || (!blocked(x+dx,y)&&!blocked(x,y+dy)&&!blocked(x+dx,y+dy));
        }
        private void BuildRoofs(View view, int zoom, int rotation)
        {
            var arch = session.VM.Context.Architecture;
            for (int story = 0; story < 2; story++) {
                int width=Size*2; var footprint=new bool[width*width];
                for(int y=0;y<width;y++)for(int x=0;x<width;x++)footprint[y*width+x]=Roofable(x,y,story);
                var mesh = TS1RoofMesh.Build(footprint,width,width,(story + 1) * 2.95f,arch.RoofPitch);
                if(mesh.Count==0)continue;
                var material = materials.Roof(roofName);
                foreach (var triangle in mesh) {
                    var normal = Vector3.Normalize(Vector3.Cross(triangle[1] - triangle[0], triangle[2] - triangle[0]));
                    if (normal.Z < 0) normal = -normal;
                    float shade = MathHelper.Clamp(.75f + .3f * Vector3.Dot(normal, Vector3.Normalize(new Vector3(-1, 1, 1.5f))), .55f, 1);
                    // RoofComponent uses 2x3 repeats/tile; rotate UVs per face
                    // rather than stretching the authored roof tiles.
                    bool alongX=Math.Abs(normal.Y)>=Math.Abs(normal.X);
                    var uv = triangle.Select(p => alongX?new Vector2(p.X*2,p.Y*3):new Vector2(p.Y*2,p.X*3)).ToArray();
                    Textured(view.RoofMaterials, material, triangle, uv, new[] {0, 1, 2}, new Color(shade, shade, shade), zoom, rotation);
                }
                view.RoofTriangles += mesh.Count;
            }
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
            public readonly List<Surface> RoofMaterials = new List<Surface>();
            public readonly List<Sprite> Sprites = new List<Sprite>();
            public int OpeningEdges, StoryJoints, RoofTriangles;
            public int Rendered, Hidden, OutOfWorld, Contained, NoGraphic, Unsupported, AboveLevel, FloorTiles, WallEdges;
            public readonly List<string> Issues = new List<string>();
        }
        public View Build(int zoom,int rotation,int level)
        {
            if (level < 1 || level > 3) throw new ArgumentOutOfRangeException("level");
            TS1SpriteLayer.Project(Vector3.Zero,zoom,rotation); // validate before allocating
            var view=new View(); var arch=session.VM.Context.Architecture;
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) {
                byte green=(byte)(94+((x+y)%2)*6);
                Quad(view.Ground,new Vector3(x,y,0),new Vector3(x+1,y,0),new Vector3(x+1,y+1,0),new Vector3(x,y+1,0),new Color(56,green,44),zoom,rotation);
            }
            var camera = new[] {new Vector2(1,1),new Vector2(1,-1),new Vector2(-1,-1),new Vector2(-1,1)}[rotation];
            for(int story=0;story<Math.Min(level,2);story++) {
                var edges=new HashSet<string>(); float z=story*2.95f;
                Func<int,int,WallTile> wallAt=(xx,yy)=>xx<0||yy<0||xx>=Size||yy>=Size?default(WallTile):arch.Walls[story][yy*Size+xx];
                for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) {
                    var wall=wallAt(x,y);
                    bool global=(floorFlags[y*64+x]&0x20)!=0;
                    bool splitFloor=(wall.Segments&WallSegments.AnyDiag)!=0 && (wall.TopLeftPattern!=0 || wall.TopLeftStyle!=0);
                    var corners=new[]{new Vector3(x,y,z),new Vector3(x+1,y,z),new Vector3(x+1,y+1,z),new Vector3(x,y+1,z)};
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
                        bool persistent = style==2 || style==12 || style==13 || style==14;
                        // Share the exact floor elevation and overlap adjoining solid
                        // walls by a subpixel amount to close raster cracks between stories.
                        bool joint = story==0 && level>=2 && !persistent && (arch.Walls[1][y*Size+x].Segments & flag)!=0;
                        float bottom = z - (story>0 && !persistent ? .015f : 0), top = z+2.95f+(joint ? .015f : 0);
                        if(joint)view.StoryJoints++;
                        var points=new[]{new Vector3(ax,ay,bottom),new Vector3(bx,by,bottom),new Vector3(bx,by,top),new Vector3(ax,ay,top)};
                        var material=materials.Wall(pattern,style);
                        Opening opening;
                        if(!persistent && openings.TryGetValue(EdgeKey(story,ax,ay,bx,by),out opening)) {
                            var delta=TS1SpriteLayer.Project(points[1]-points[0],zoom,rotation);
                            int frame=delta.X*delta.Y<0?0:1;
                            material=materials.WithOpening(material,opening.Mask,frame,delta.X<0,opening.Identity);
                            view.OpeningEdges++;
                        }
                        Textured(view.WallMaterials,material,points,new[]{Vector2.UnitY,Vector2.One,Vector2.UnitX,Vector2.Zero},new[]{0,1,2,0,2,3},color,zoom,rotation,persistent);
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
            if(level==3)BuildRoofs(view,zoom,rotation);
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
