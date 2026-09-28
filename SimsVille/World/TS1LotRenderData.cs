using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    // Static architecture blockout: real saved floor/wall layout with diagnostic
    // colors. TS1 material textures, roof shapes and wall openings are not mapped.
    public sealed class TS1LotRenderData : IDisposable
    {
        private readonly TS1LotObjectSession session;
        public int Size { get { return session.VM.Context.Architecture.Width; } }
        public int ObjectCount { get { return session.SavedObjectCount; } }
        public TS1LotRenderData(GamePaths paths, int house)
        {
            if (house != 2 && house != 28) throw new ArgumentOutOfRangeException("house");
            if (VM.UseWorld) throw new InvalidOperationException("Static lot renderer requires a headless VM.");
            var store = new NeighborhoodStore(paths, 0);
            session = TS1LotObjectSession.Load(new IffFile(store.GetReadPath("Houses/House"+house.ToString("00")+".iff")),new TS1ObjectProvider(paths));
        }
        public sealed class Sprite
        {
            public TS1SpriteLayer Layer;
            public Vector2 Position;
            public float BackNearness;
            public short ObjectID;
        }
        public sealed class View
        {
            public readonly List<VertexPositionColor> Ground = new List<VertexPositionColor>();
            public readonly List<VertexPositionColor> Walls = new List<VertexPositionColor>();
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
            for(int story=0;story<level;story++) {
                var edges=new HashSet<string>(); float z=story*2.95f;
                for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) {
                    var floor=arch.Floors[story][y*Size+x];
                    if(floor.Pattern!=0) {
                        var color=floor.Pattern>=65534?new Color(45,125,175):new Color(145+(floor.Pattern*17)%65,130+(floor.Pattern*7)%55,100+(floor.Pattern*11)%60);
                        Quad(view.Ground,new Vector3(x,y,z+0.003f),new Vector3(x+1,y,z+0.003f),new Vector3(x+1,y+1,z+0.003f),new Vector3(x,y+1,z+0.003f),color,zoom,rotation);
                        view.FloorTiles++;
                    }
                    var wall=arch.Walls[story][y*Size+x];
                    Action<WallSegments,int,int,int,int> edge=(flag,ax,ay,bx,by)=> {
                        if((wall.Segments&flag)==0) return;
                        int a=ay*(Size+1)+ax,b=by*(Size+1)+bx;
                        var key=Math.Min(a,b)+":"+Math.Max(a,b);
                        if(!edges.Add(key)) return;
                        var color=ax==bx?new Color(189,180,161):new Color(220,209,189);
                        Quad(view.Walls,new Vector3(ax,ay,z),new Vector3(bx,by,z),new Vector3(bx,by,z+2.95f),new Vector3(ax,ay,z+2.95f),color,zoom,rotation);
                        view.WallEdges++;
                    };
                    edge(WallSegments.TopLeft,x,y,x,y+1);edge(WallSegments.TopRight,x,y,x+1,y);
                    edge(WallSegments.BottomRight,x+1,y,x+1,y+1);edge(WallSegments.BottomLeft,x,y+1,x+1,y+1);
                    edge(WallSegments.HorizontalDiag,x,y+1,x+1,y);edge(WallSegments.VerticalDiag,x,y,x+1,y+1);
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
