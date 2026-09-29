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
        public const int Count=7;
        public static List<string> Run(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var result=new List<string>();bool oldWorld=VM.UseWorld;VM.UseWorld=false;
            Action<string,Action> check=(name,action)=>{try{action();result.Add("PASS "+name);}catch(Exception ex){result.Add("FAIL "+name);log(ex.ToString());}log(result.Last());};
            try {
                check("CAMERA DEPTH AND WALL HEIGHT",()=>{if(!LotProjectionTests.Run())throw new InvalidOperationException("Projection checks failed.");});
                check("GPU DEPTH HOLES AND ALPHA",()=>DepthFixture(device,effect));
                check("TS1 MATERIAL MAPPING AND FENCE ALPHA",()=>MaterialMapping(paths));
                check("GPU MATERIAL CUTOUTS AND WALL TOGGLE",()=>MaterialFixture(device,effect));
                check("DIAGONAL FULL FLOORS AND STAIR OPENING",()=>DiagonalFloors(paths));
                foreach(int house in new[]{2,28}) check("HOUSE "+house+" FOUR ANGLES TWO LEVELS",()=> {
                    using(var lot=new TS1LotRenderData(paths,house)) {
                        for(int level=1;level<=2;level++) {
                            int floorCount=-1,wallCount=-1;
                            for(int rotation=0;rotation<4;rotation++) {
                                var data=lot.Build(1,rotation,level);
                                if(data.Rendered+data.Hidden+data.OutOfWorld+data.Contained+data.NoGraphic+data.Unsupported+data.AboveLevel!=lot.ObjectCount)
                                    throw new InvalidOperationException("Unaccounted saved objects.");
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
                                log("LOT GPU house="+house+" level="+level+" rotation="+rotation+" drawn="+data.Rendered+" unsupported="+data.Unsupported+" contained="+data.Contained+" floors="+floorCount+" walls="+wallCount);
                            }
                        }
                    }
                });
            }finally{VM.UseWorld=oldWorld;}
            return result;
        }
        private static void DiagonalFloors(GamePaths paths)
        {
            using(var lot=new TS1LotRenderData(paths,2))
            for(int zoom=1;zoom<=3;zoom++) for(int rotation=0;rotation<4;rotation++) {
                var data=lot.Build(zoom,rotation,2);
                // Actual saved carpet tiles reported in Xbox screenshots 5/6,
                // plus a genuine stairwell tile that must remain open.
                foreach(var tile in new[]{new Point(22,31),new Point(23,32),new Point(24,26)}) {
                    var corners=new[]{new Vector3(tile.X,tile.Y,2.953f),new Vector3(tile.X+1,tile.Y,2.953f),new Vector3(tile.X+1,tile.Y+1,2.953f),new Vector3(tile.X,tile.Y+1,2.953f)}
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
            using(var renderer=new TS1LotRenderer(device,data,effect)) using(var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
                try {
                    foreach(bool show in new[]{true,false}) {
                        device.SetRenderTarget(target);device.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,Color.Black,1,0);
                        renderer.Draw(Matrix.CreateOrthographicOffCenter(0,64,64,0,-128,128),show);
                        device.SetRenderTargets(previous);device.Viewport=viewport;
                        var pixels=new Color[4096];target.GetData(pixels);
                        Expect(pixels[16*64+16],show?Color.Red:Color.Blue);Expect(pixels[16*64+48],Color.Blue);
                        Expect(pixels[48*64+16],show?Color.Lime:Color.Blue);Expect(pixels[48*64+48],Color.Blue);
                    }
                }finally{device.SetRenderTargets(previous);device.Viewport=viewport;}
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
