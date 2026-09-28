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
        public const int Count=4;
        public static List<string> Run(GraphicsDevice device,GamePaths paths,byte[] effect,Action<string> log)
        {
            var result=new List<string>();bool oldWorld=VM.UseWorld;VM.UseWorld=false;
            Action<string,Action> check=(name,action)=>{try{action();result.Add("PASS "+name);}catch(Exception ex){result.Add("FAIL "+name);log(ex.ToString());}log(result.Last());};
            try {
                check("CAMERA DEPTH AND WALL HEIGHT",()=>{if(!LotProjectionTests.Run())throw new InvalidOperationException("Projection checks failed.");});
                check("GPU DEPTH HOLES AND ALPHA",()=>DepthFixture(device,effect));
                foreach(int house in new[]{2,28}) check("HOUSE "+house+" FOUR ANGLES TWO LEVELS",()=> {
                    using(var lot=new TS1LotRenderData(paths,house)) {
                        for(int level=1;level<=2;level++) {
                            int floorCount=-1,wallCount=-1;
                            for(int rotation=0;rotation<4;rotation++) {
                                var data=lot.Build(1,rotation,level);
                                if(data.Rendered+data.Hidden+data.OutOfWorld+data.Contained+data.NoGraphic+data.Unsupported+data.AboveLevel!=lot.ObjectCount)
                                    throw new InvalidOperationException("Unaccounted saved objects.");
                                if(data.Rendered!=data.Sprites.Select(s=>s.ObjectID).Distinct().Count()) throw new InvalidOperationException("Visible object count mismatch.");
                                if(data.Rendered<20 || data.FloorTiles==0 || data.WallEdges==0) throw new InvalidOperationException("Missing lot geometry/content.");
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
