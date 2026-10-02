using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Platform;
using FSO.LotView;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Tests
{
    public static class LotRenderTests
    {
        public const int Count=13;
        public static List<string> Run(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var result=new List<string>();bool oldWorld=VM.UseWorld;VM.UseWorld=false;
            Action<string,Action> check=(name,action)=>{try{action();result.Add("PASS "+name);}catch(Exception ex){result.Add("FAIL "+name);log(ex.ToString());}log(result.Last());};
            try {
                check("CAMERA DEPTH AND WALL HEIGHT",()=>{if(!LotProjectionTests.Run())throw new InvalidOperationException("Projection checks failed.");});
                check("GPU DEPTH HOLES AND ALPHA",()=>DepthFixture(device,effect));
                check("TS1 MATERIAL MAPPING AND FENCE ALPHA",()=>MaterialMapping(paths));
                check("GPU WALL TOGGLE PRESERVES RAILINGS",()=>MaterialFixture(device,effect));
                check("DIAGONAL FULL FLOORS AND STAIR OPENING",()=>DiagonalFloors(paths));
                check("STAIR UPPER HANDRAIL SPRITES",()=>StairHandrails(paths));
                check("TS1 DOOR AND WINDOW OPENING MASKS",()=>OpeningMasks(paths));
                check("GPU OPENINGS PRESERVE DEPTH",()=>OpeningFixture(device,paths,effect));
                check("SHARED FLOOR HEIGHT AND STORY JOINTS",()=>StoryJoints(paths));
                check("ROOF BMP RLE8 AND BOUNDS",()=>RoofBitmap());
                check("ROOF HIPS AND OPEN COURTYARDS",()=>RoofGeometry(paths));
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
            Func<int,int,Vector2[]> tri=(x,y)=>new[]{new Vector2(x,y),new Vector2(x+1,y),new Vector2(x+1,y+1)};
            Func<int,int,Vector2[]> other=(x,y)=>new[]{new Vector2(x,y),new Vector2(x+1,y+1),new Vector2(x,y+1)};
            var footprint=new List<Vector2[]>();
            for(int y=0;y<2;y++)for(int x=0;x<4;x++){footprint.Add(tri(x,y));footprint.Add(other(x,y));}
            var rectangle=TS1RoofMesh.Build(footprint,5.9f,.66f);
            if(Math.Abs(rectangle.SelectMany(t=>t).Max(p=>p.Z)-6.56f)>.001f)throw new InvalidOperationException("Incorrect rectangular hip height.");
            footprint.Clear();
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)if(x==0||y==0||x==3||y==3){footprint.Add(tri(x,y));footprint.Add(other(x,y));}
            var courtyard=TS1RoofMesh.Build(footprint,5.9f,.66f);
            float area=0;
            var shared=new Dictionary<Vector2,float>();
            foreach(var t in courtyard){
                var center=(t[0]+t[1]+t[2])/3;
                if(center.X>1&&center.X<3&&center.Y>1&&center.Y<3)throw new InvalidOperationException("Roof covers open courtyard.");
                area+=Math.Abs((t[1].X-t[0].X)*(t[2].Y-t[0].Y)-(t[1].Y-t[0].Y)*(t[2].X-t[0].X))/2;
                foreach(var p in t){float previous;var xy=new Vector2(p.X,p.Y);if(shared.TryGetValue(xy,out previous)&&previous!=p.Z)throw new InvalidOperationException("Roof height crack.");shared[xy]=p.Z;}
            }
            if(Math.Abs(area-12)>.001f)throw new InvalidOperationException("Roof footprint area changed.");
            var diagonal=TS1RoofMesh.Build(new[]{new[]{Vector2.Zero,Vector2.UnitX,Vector2.UnitY}},5.9f,.66f);
            if(diagonal.SelectMany(t=>t).Any(p=>p.X+p.Y>1.001f))throw new InvalidOperationException("Diagonal roof corner was filled.");
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
                if(!covers(23.2f,22.8f) || covers(23.8f,22.2f))throw new InvalidOperationException("Saved diagonal roof half is reversed.");
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
