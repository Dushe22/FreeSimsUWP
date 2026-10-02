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
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    public enum TS1WallMode { Down, Cutaway, Up }

    // Static saved architecture with authored TS1 opening masks and roof textures.
    public sealed class TS1LotRenderData : IDisposable
    {
        private readonly TS1LotObjectSession session;
        private readonly TS1MaterialProvider materials;
        private readonly byte[] floorFlags, grass;
        private readonly string roofName;
        private readonly VMRoomMap[] roofRooms = new VMRoomMap[2];
        private readonly List<VMRoom> roofRoomData = new List<VMRoom> {new VMRoom {IsOutside = true}};
        private readonly Dictionary<short, Vector2> poolAttachmentOffsets = new Dictionary<short, Vector2>();
        private readonly Dictionary<short, Vector2> openingNormals = new Dictionary<short, Vector2>();
        private readonly Dictionary<short, List<string>> attachmentEdges = new Dictionary<short, List<string>>();
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
            var savedGrass=iff.Get<ARRY>(6);
            if(savedGrass==null || savedGrass.Width!=64 || savedGrass.Height!=64 || savedGrass.ByteSize()!=1)throw new InvalidDataException("Missing saved grass map.");
            grass=savedGrass.TransposeData;
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
                LoadCutawayAttachments();
                LoadPoolAttachments();
                // Water separates simulation room regions; it must not enclose a roof.
                for (int story = 0; story < 2; story++) {
                    roofRooms[story] = new VMRoomMap();
                    roofRooms[story].GenerateMap(session.VM.Context.Architecture.Walls[story], new FloorTile[Size * Size], Size, Size, roofRoomData);
                }
            } catch {session.Dispose();throw;}
        }
        private void LoadPoolAttachments()
        {
            // A saved ladder may straddle the wrong pool tile. Reconcile only
            // this known three-part attachment with its adjacent deck for display;
            // never move VM entities or rewrite the user's saved lot.
            foreach(var deck in session.VM.Entities.Where(e=>e.Object.OBJ.GUID==0x4208E20Bu && e.Position.Level==1)) {
                var group=deck.MultitileGroup;
                if(group==null || group.Objects.Count!=3 || !group.Objects.Any(e=>e.Object.OBJ.GUID==0x4208CF6Eu) ||
                    !group.Objects.Any(e=>e.Object.OBJ.GUID==0x6A3A76D7u))continue;
                var offset=PoolLadderOffset(session.VM.Context.Architecture.Floors[0],Size,deck.Position.TileX,deck.Position.TileY,deck.Direction);
                if(offset==Vector2.Zero)continue;
                foreach(var part in group.Objects)poolAttachmentOffsets.Add(part.ObjectID,offset);
            }
        }
        public static Vector2 PoolLadderOffset(FloorTile[] floors,int size,int x,int y,Direction direction)
        {
            if(floors==null||size<1||floors.Length!=size*size||x<0||y<0||x>=size||y>=size)
                throw new ArgumentException("Invalid pool attachment footprint.");
            int dx=0,dy=0;
            switch(direction) {
                case Direction.NORTH:dy=-1;break;case Direction.EAST:dx=1;break;
                case Direction.SOUTH:dy=1;break;case Direction.WEST:dx=-1;break;
                default:throw new ArgumentOutOfRangeException("direction");
            }
            Func<int,int,ushort> pattern=(xx,yy)=>xx<0||yy<0||xx>=size||yy>=size?(ushort)0:floors[yy*size+xx].Pattern;
            if(pattern(x,y)!=65535)return Vector2.Zero;
            ushort deck=pattern(x-dx,y-dy);
            if(deck==0||deck>=65534||pattern(x+dx,y+dy)!=65535)return Vector2.Zero;
            return new Vector2(-dx,-dy);
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
                    // Each saved half owns its face's mask. End sections of wide
                    // windows can use different masks on opposite faces.
                    var wall = session.VM.Context.Architecture.Walls[story][y * Size + x];
                    var segment = new[]{WallSegments.TopRight,WallSegments.BottomRight,WallSegments.BottomLeft,WallSegments.TopLeft}[side];
                    if ((wall.Segments & segment) == 0) continue;
                    var mask = definition.WallStyle > 21 ? entity.Object.Resource.Get<SPR>((ushort)(definition.WallStyleSpriteID + 2)) : materials.OpeningMask(definition.WallStyle);
                    if (mask == null) throw new InvalidDataException("Missing wall opening mask for object " + entity.ObjectID);
                    string identity = definition.WallStyle > 21 ? entity.Object.Resource.Name + ":" + definition.WallStyleSpriteID : "global:" + definition.WallStyle;
                    string key = (side == 0 ? EdgeKey(story,x,y,x+1,y) : side == 1 ? EdgeKey(story,x+1,y,x+1,y+1) : side == 2 ? EdgeKey(story,x,y+1,x+1,y+1) : EdgeKey(story,x,y,x,y+1)) + ":face:" + (side == 0 || side == 3);
                    Opening existing;
                    if (openings.TryGetValue(key, out existing) && existing.Identity != identity) throw new InvalidDataException("Conflicting wall openings at " + key);
                    List<string> hosts;
                    if(!attachmentEdges.TryGetValue(entity.ObjectID,out hosts))attachmentEdges.Add(entity.ObjectID,hosts=new List<string>());
                    hosts.Add(key.Substring(0,key.IndexOf(":face:",StringComparison.Ordinal)));
                    openings[key] = new Opening {Mask = mask, Identity = identity};
                    openingNormals[entity.ObjectID] = new[]{Vector2.UnitY,-Vector2.UnitX,-Vector2.UnitY,Vector2.UnitX}[side];
                }
            }
        }
        private void LoadCutawayAttachments()
        {
            // Saved wall lamps/art also carry the game's HideForCutaway flag.
            foreach(var entity in session.VM.Entities) {
                if(attachmentEdges.ContainsKey(entity.ObjectID)||entity.Container!=null ||
                    (((VMEntityFlags)entity.GetValue(VMStackObjectVariable.Flags))&VMEntityFlags.HideForCutaway)==0)continue;
                int x=entity.Position.TileX,y=entity.Position.TileY,story=entity.Position.Level-1;
                if(x<0||y<0||x>=Size||y>=Size||story<0||story>1)continue;
                int direction=entity.Direction==Direction.NORTH?0:entity.Direction==Direction.EAST?1:entity.Direction==Direction.SOUTH?2:entity.Direction==Direction.WEST?3:-1;
                if(direction<0)continue;
                int required=entity.GetValue(VMStackObjectVariable.WallPlacementFlags)&15;
                var hosts=new List<string>();var wall=session.VM.Context.Architecture.Walls[story][y*Size+x];
                for(int relative=0;relative<4;relative++) {
                    if((required&(1<<relative))==0)continue;
                    int side=(direction+relative)%4;
                    var flag=new[]{WallSegments.TopRight,WallSegments.BottomRight,WallSegments.BottomLeft,WallSegments.TopLeft}[side];
                    if((wall.Segments&flag)==0)continue;
                    hosts.Add(side==0?EdgeKey(story,x,y,x+1,y):side==1?EdgeKey(story,x+1,y,x+1,y+1):side==2?EdgeKey(story,x,y+1,x+1,y+1):EdgeKey(story,x,y,x,y+1));
                }
                if(hosts.Count>0)attachmentEdges.Add(entity.ObjectID,hosts);
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
            public string[] WallHosts;
        }
        public sealed class Surface
        {
            public TS1MaterialProvider.Material Material;
            public bool KeepWhenWallsHidden;
            public readonly List<VertexPositionColorTexture> Vertices = new List<VertexPositionColorTexture>();
        }
        public sealed class WallSection
        {
            public string Key, EndA, EndB;
            public Surface Surface;
            public int Offset, Story, TileX, TileY;
            public bool Cut;
            public VertexPositionColorTexture[] Low;
            public Vector3[] Full;
            public VertexPositionColor[] Top, LowTop, EndFaceA, EndFaceB, LowEndA, LowEndB;
        }
        public sealed class View
        {
            public readonly List<VertexPositionColor> Ground = new List<VertexPositionColor>();
            public readonly List<VertexPositionColor> Walls = new List<VertexPositionColor>();
            public readonly List<Surface> TerrainMaterials = new List<Surface>();
            public readonly List<Surface> FloorMaterials = new List<Surface>();
            public readonly List<Surface> WallMaterials = new List<Surface>();
            public readonly List<Surface> RoofMaterials = new List<Surface>();
            public readonly List<Sprite> Sprites = new List<Sprite>();
            public readonly List<WallSection> WallSections = new List<WallSection>();
            public readonly HashSet<uint> CutRooms = new HashSet<uint>();
            public readonly Queue<uint> CutRoomOrder = new Queue<uint>();
            public int Zoom, Rotation, Level, WallRevision;
            public TS1WallMode WallMode = (TS1WallMode)(-1);
            public Point? LastHover, LastCutOrigin;
            public Vector2? LastPointer;
            public Matrix LastCamera;
            public string LastWall;
            public int OpeningEdges, StoryJoints, RoofTriangles;
            public int TerrainTiles, PoolTiles, WaterTiles, PoolAttachmentAdjustments;
            public int Rendered, Hidden, OutOfWorld, Contained, NoGraphic, Unsupported, AboveLevel, FloorTiles, WallEdges;
            public readonly List<string> Issues = new List<string>();
        }
        public View Build(int zoom,int rotation,int level)
        {
            if (level < 1 || level > 3) throw new ArgumentOutOfRangeException("level");
            TS1SpriteLayer.Project(Vector3.Zero,zoom,rotation); // validate before allocating
            var view=new View {Zoom=zoom,Rotation=rotation,Level=level,PoolAttachmentAdjustments=poolAttachmentOffsets.Count/3}; var arch=session.VM.Context.Architecture;
            var spriteFrames=new Dictionary<SPR2Frame,TS1SpriteLayer>();
            var terrain=materials.Terrain(grass,Size);
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++) {
                var points=new[]{new Vector3(x,y,-.01f),new Vector3(x+1,y,-.01f),new Vector3(x+1,y+1,-.01f),new Vector3(x,y+1,-.01f)};
                var textureUV=new[]{new Vector2(x/(float)Size,y/(float)Size),new Vector2((x+1)/(float)Size,y/(float)Size),
                    new Vector2((x+1)/(float)Size,(y+1)/(float)Size),new Vector2(x/(float)Size,(y+1)/(float)Size)};
                Textured(view.TerrainMaterials,terrain,points,textureUV,new[]{0,1,2,0,2,3},Color.White,zoom,rotation);
                view.TerrainTiles++;
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
                        if(pattern>=65534){
                            Textured(view.FloorMaterials,materials.Water(pattern,WaterNeighbors(arch.Floors[story],Size,x,y,pattern),rotation),corners,uv,indices,Color.White,zoom,rotation);
                            if(pattern==65535)view.PoolTiles++;else view.WaterTiles++;
                        }
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
                        if(!persistent && openings.TryGetValue(EdgeKey(story,ax,ay,bx,by)+":face:"+(ax==bx ? camera.X>0 : camera.Y>0),out opening)) {
                            var delta=TS1SpriteLayer.Project(points[1]-points[0],zoom,rotation);
                            int frame=delta.X*delta.Y<0?0:1;
                            material=materials.WithOpening(material,opening.Mask,frame,delta.X<0,opening.Identity);
                            view.OpeningEdges++;
                        }
                        var faceUV=new[]{Vector2.UnitY,Vector2.One,Vector2.UnitX,Vector2.Zero};
                        var surface=Textured(view.WallMaterials,material,points,faceUV,new[]{0,1,2,0,2,3},color,zoom,rotation,persistent);
                        if(!persistent) {
                            var lowPoints=(Vector3[])points.Clone();lowPoints[2].Z=lowPoints[3].Z=z+.22f;
                            var lowUV=(Vector2[])faceUV.Clone();lowUV[2].Y=lowUV[3].Y=1-(z+.22f-bottom)/(top-bottom);
                            var lowSurface=new List<Surface>();
                            var low=Textured(lowSurface,material,lowPoints,lowUV,new[]{0,1,2,0,2,3},color,zoom,rotation).Vertices.ToArray();
                            // Extrude away from the visible plane, keeping authored opening depths intact.
                            var direction=Vector2.Normalize(new Vector2(bx-ax,by-ay));
                            var normal=new Vector2(-direction.Y,direction.X)*.09f;
                            if(Vector2.Dot(normal,camera)>0)normal=-normal;
                            var thickness=new Vector3(normal,0);
                            view.WallSections.Add(new WallSection {
                                Key=EdgeKey(story,ax,ay,bx,by),EndA=story+":"+ax+":"+ay,EndB=story+":"+bx+":"+by,
                                Story=story,TileX=x,TileY=y,Surface=surface,Offset=surface.Vertices.Count-6,Low=low,
                                Full=points.Select(p=>TS1SpriteLayer.ProjectWithDepth(p,zoom,rotation)).ToArray(),
                                Top=WallCap(points[3],points[2],thickness,zoom,rotation),LowTop=WallCap(lowPoints[3],lowPoints[2],thickness,zoom,rotation),
                                EndFaceA=WallCap(points[0],points[3],thickness,zoom,rotation),EndFaceB=WallCap(points[2],points[1],thickness,zoom,rotation),
                                LowEndA=WallCap(lowPoints[0],lowPoints[3],thickness,zoom,rotation),LowEndB=WallCap(lowPoints[2],lowPoints[1],thickness,zoom,rotation)
                            });
                        }
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
                    var layers=TS1SpriteLayer.Read(entity,zoom,rotation,spriteFrames);
                    if(layers.Count==0){view.NoGraphic++;continue;}
                    var world=new Vector3(entity.Position.x/16f,entity.Position.y/16f,(entity.Position.Level-1)*2.95f);
                    Vector2 poolOffset;if(poolAttachmentOffsets.TryGetValue(entity.ObjectID,out poolOffset))world+=new Vector3(poolOffset,0);
                    var point=TS1SpriteLayer.Project(world,zoom,rotation);
                    Vector2 openingNormal;
                    // Near-face frames are decals on the host wall. A bounded
                    // eight-sample SPR2 depth bias resolves overlap with the flat
                    // wall plane without bringing the opposite face forward.
                    float attachmentBias=openingNormals.TryGetValue(entity.ObjectID,out openingNormal) && Vector2.Dot(openingNormal,camera)>0 ? (float)Math.Sqrt(1.5)/.4f*8/255 : 0;
                    foreach(var layer in layers) view.Sprites.Add(new Sprite {
                        Layer=layer,Position=point+layer.Offset,ObjectID=entity.ObjectID,
                        WallHosts=attachmentEdges.ContainsKey(entity.ObjectID)?attachmentEdges[entity.ObjectID].ToArray():null,
                        BackNearness=TS1SpriteLayer.ProjectWithDepth(world-new Vector3(0.5f,0.5f,0)+backOffsets[rotation]+layer.WorldOffset,zoom,rotation).Z+attachmentBias
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
        private static VertexPositionColor[] WallCap(Vector3 a,Vector3 b,Vector3 thickness,int zoom,int rotation)
        {
            var target=new List<VertexPositionColor>();
            Quad(target,a,b,b+thickness,a+thickness,new Color(153,120,83),zoom,rotation);
            return target.ToArray();
        }
        // Pointer coordinates are local to the scene target, before the UI letterbox transform.
        public static Vector2 TileAtPointer(Vector2 pointer,Matrix camera,int width,int height,int zoom,int rotation,int level)
        {
            if(width<=0||height<=0||level<1||level>3)throw new ArgumentOutOfRangeException("level");
            TS1SpriteLayer.Project(Vector3.Zero,zoom,rotation);
            var clip=new Vector3(pointer.X*2/width-1,1-pointer.Y*2/height,0);
            var screen=Vector3.Transform(clip,Matrix.Invert(camera));
            float w=16*(1<<(zoom-1)),sum=2*(screen.Y+(Math.Min(level,2)-1)*2.95f*w*(float)Math.Sqrt(1.5))/w;
            float diff=screen.X/w,x=(sum+diff)/2,y=(sum-diff)/2;
            switch(rotation) {case 1:return new Vector2(y,-x);case 2:return new Vector2(-x,-y);case 3:return new Vector2(-y,x);default:return new Vector2(x,y);}
        }
        public static Vector2 PointerAtTile(Vector3 tile,Matrix camera,int width,int height,int zoom,int rotation)
        {
            var clip=Vector3.Transform(TS1SpriteLayer.ProjectWithDepth(tile,zoom,rotation),camera);
            return new Vector2((clip.X+1)*width/2,(1-clip.Y)*height/2);
        }
        private static bool TriangleHit(Vector2 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 weights)
        {
            float divisor=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);
            if(Math.Abs(divisor)<.0001f){weights=Vector3.Zero;return false;}
            float u=((b.Y-c.Y)*(p.X-c.X)+(c.X-b.X)*(p.Y-c.Y))/divisor;
            float v=((c.Y-a.Y)*(p.X-c.X)+(a.X-c.X)*(p.Y-c.Y))/divisor;
            weights=new Vector3(u,v,1-u-v);return u>=0&&v>=0&&u+v<=1;
        }
        private static WallSection PickWall(View view,Vector2 pointer,Matrix camera,int width,int height)
        {
            var clip=new Vector3(pointer.X*2/width-1,1-pointer.Y*2/height,0);
            var p=Vector3.Transform(clip,Matrix.Invert(camera));var point=new Vector2(p.X,p.Y);
            WallSection nearest=null;float near=float.MinValue;
            foreach(var section in view.WallSections) {
                if(section.Story!=Math.Min(view.Level,2)-1)continue;
                // Use the full wall even while cut, so its hover region does not flicker.
                var q=section.Full;Vector3 weights;float depth,u,v;
                if(TriangleHit(point,q[0],q[1],q[2],out weights)) {
                    depth=q[0].Z*weights.X+q[1].Z*weights.Y+q[2].Z*weights.Z;
                    u=weights.Y+weights.Z;v=1-weights.Z;
                } else if(TriangleHit(point,q[0],q[2],q[3],out weights)) {
                    depth=q[0].Z*weights.X+q[2].Z*weights.Y+q[3].Z*weights.Z;
                    u=weights.Y;v=weights.X;
                } else continue;
                var m=section.Surface.Material;
                int xx=Math.Min(m.Width-1,Math.Max(0,(int)(u*m.Width))),yy=Math.Min(m.Height-1,Math.Max(0,(int)(v*m.Height)));
                if(m.Pixels[yy*m.Width+xx].A<128)continue; // pick through authored openings
                if(depth>near){near=depth;nearest=section;}
            }
            return nearest;
        }
        public bool UpdateWalls(View view,TS1WallMode mode,Vector2? pointer,Matrix camera,int width,int height)
        {
            if(mode<TS1WallMode.Down||mode>TS1WallMode.Up)throw new ArgumentOutOfRangeException("mode");
            if(view.WallMode==mode&&view.LastPointer==pointer&&view.LastCamera==camera)return false;
            view.LastPointer=pointer;view.LastCamera=camera;
            int floor=Math.Min(view.Level,2);
            Vector2? tile=pointer.HasValue?(Vector2?)TileAtPointer(pointer.Value,camera,width,height,view.Zoom,view.Rotation,view.Level):null;
            Point? hover=tile.HasValue?(Point?)new Point((int)Math.Floor(tile.Value.X),(int)Math.Floor(tile.Value.Y)):null;
            var hit=mode==TS1WallMode.Cutaway&&pointer.HasValue&&view.Level!=3?PickWall(view,pointer.Value,camera,width,height):null;
            string hitKey=hit==null?null:hit.Key;
            var dir=VMArchitectureTools.CutCheckDir[view.Rotation];
            Point? cutOrigin=tile.HasValue?(Point?)new Point((int)(tile.Value.X-2.5f)-dir[0]*2,(int)(tile.Value.Y-2.5f)-dir[1]*2):null;
            if(view.WallMode==mode&&view.LastHover==hover&&view.LastWall==hitKey&&view.LastCutOrigin==cutOrigin)return false;
            view.WallMode=mode;view.LastHover=hover;view.LastWall=hitKey;view.LastCutOrigin=cutOrigin;
            bool[] cuts=null;
            if(mode==TS1WallMode.Cutaway && view.Level!=3) {
                var arch=session.VM.Context.Architecture;
                if(hover.HasValue && hover.Value.X>=0&&hover.Value.Y>=0&&hover.Value.X<Size&&hover.Value.Y<Size) {
                    uint room=session.VM.Context.GetRoomAt(LotTilePos.FromBigTile((short)hover.Value.X,(short)hover.Value.Y,(sbyte)floor));
                    if(!session.VM.Context.RoomInfo[(int)room].Room.IsOutside&&view.CutRooms.Add(room)) {
                        view.CutRoomOrder.Enqueue(room);
                        if(view.CutRoomOrder.Count>3)view.CutRooms.Remove(view.CutRoomOrder.Dequeue());
                    }
                }
                cuts=VMArchitectureTools.GenerateRoomCut(arch,(sbyte)floor,(WorldRotation)view.Rotation,view.CutRooms);
                if(tile.HasValue) {
                    VMArchitectureTools.ApplyCutRectangle(arch,(sbyte)floor,cuts,new Rectangle(cutOrigin.Value.X,cutOrigin.Value.Y,5,5));
                }
                // Direct wall hover also cuts that segment, including outer walls.
                if(hit!=null)cuts[hit.TileY*Size+hit.TileX]=true;
            }
            bool changed=false;
            foreach(var section in view.WallSections) {
                bool cut=view.Level!=3&&section.Story==floor-1&&(mode==TS1WallMode.Down ||
                    (mode==TS1WallMode.Cutaway&&cuts[section.TileY*Size+section.TileX]));
                if(cut!=section.Cut){section.Cut=cut;changed=true;}
            }
            if(changed)view.WallRevision++;
            return changed;
        }
        public static byte WaterNeighbors(FloorTile[] floors,int size,int x,int y,ushort pattern)
        {
            if(floors==null||size<1||floors.Length!=size*size||x<0||y<0||x>=size||y>=size || (pattern!=65534&&pattern!=65535))
                throw new ArgumentException("Invalid water footprint.");
            int[] dx={0,1,1,1,0,-1,-1,-1},dy={-1,-1,0,1,1,1,0,-1};int result=0;
            for(int i=0;i<8;i++) {
                int xx=x+dx[i],yy=y+dy[i];
                if(xx>=0&&yy>=0&&xx<size&&yy<size&&floors[yy*size+xx].Pattern==pattern)result|=1<<i;
            }
            return (byte)result;
        }
        private static Surface Textured(List<Surface> surfaces,TS1MaterialProvider.Material material,Vector3[] corners,Vector2[] uv,int[] indices,Color color,int zoom,int rotation,bool keepWhenWallsHidden=false)
        {
            var surface=surfaces.FirstOrDefault(x=>ReferenceEquals(x.Material,material) && x.KeepWhenWallsHidden==keepWhenWallsHidden);
            if(surface==null){surface=new Surface{Material=material,KeepWhenWallsHidden=keepWhenWallsHidden};surfaces.Add(surface);}
            foreach(int i in indices)surface.Vertices.Add(new VertexPositionColorTexture(TS1SpriteLayer.ProjectWithDepth(corners[i],zoom,rotation),color,uv[i]));
            return surface;
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
