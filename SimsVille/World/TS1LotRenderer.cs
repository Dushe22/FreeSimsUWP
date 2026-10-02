using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    public sealed class TS1LotRenderer : IDisposable
    {
        private sealed class Item : IDisposable
        {
            public Texture2D Color,Depth; public VertexPositionColorTexture[] Vertices;
            public float Nearness; public short ID;
            public void Dispose() {if(Color!=null)Color.Dispose();if(Depth!=null)Depth.Dispose();}
        }
        private sealed class MaterialItem
        {
            public Texture2D Texture; public VertexPositionColorTexture[] Vertices; public bool Wall, Roof;
        }
        private readonly List<MaterialItem> materialItems=new List<MaterialItem>();
        private readonly GraphicsDevice device;
        private readonly BasicEffect surfaces;
        private readonly AlphaTestEffect materials;
        private readonly Effect sprites;
        private readonly List<Item> items=new List<Item>();
        private readonly VertexPositionColor[] ground,walls;
        public readonly TS1LotRenderData.View Data;
        public TS1LotRenderer(GraphicsDevice device,TS1LotRenderData.View data,byte[] effect)
        {
            this.device=device;Data=data;
            try {
                surfaces=new BasicEffect(device) {VertexColorEnabled=true};sprites=new Effect(device,effect);
                materials=new AlphaTestEffect(device) {VertexColorEnabled=true,ReferenceAlpha=128,AlphaFunction=CompareFunction.GreaterEqual};
                ground=data.Ground.ToArray();walls=data.Walls.ToArray();
                foreach(var source in data.FloorMaterials.Concat(data.WallMaterials).Concat(data.RoofMaterials)) {
                    var item=new MaterialItem {Wall=data.WallMaterials.Contains(source) && !source.KeepWhenWallsHidden,Roof=data.RoofMaterials.Contains(source),Vertices=source.Vertices.ToArray()};materialItems.Add(item);
                    item.Texture=new Texture2D(device,source.Material.Width,source.Material.Height);item.Texture.SetData(source.Material.Pixels);
                }
                foreach(var source in data.Sprites) {
                    var layer=source.Layer;var item=new Item {Nearness=source.BackNearness,ID=source.ObjectID};items.Add(item);
                    item.Color=new Texture2D(device,layer.Width,layer.Height);item.Color.SetData(layer.Pixels);
                    item.Depth=new Texture2D(device,layer.Width,layer.Height,false,SurfaceFormat.Alpha8);item.Depth.SetData(layer.Depth);
                    float l=layer.Flip?1:0,r=1-l;var p=source.Position;float z=source.BackNearness;
                    var a=new VertexPositionColorTexture(new Vector3(p,z),Color.White,new Vector2(l,0));
                    var b=new VertexPositionColorTexture(new Vector3(p.X+layer.Width,p.Y,z),Color.White,new Vector2(r,0));
                    var c=new VertexPositionColorTexture(new Vector3(p.X+layer.Width,p.Y+layer.Height,z),Color.White,new Vector2(r,1));
                    var d=new VertexPositionColorTexture(new Vector3(p.X,p.Y+layer.Height,z),Color.White,new Vector2(l,1));
                    item.Vertices=new[]{a,b,c,a,c,d};
                }
            } catch {Dispose();throw;}
        }
        public void Draw(Matrix projection,bool showWalls,bool reverseOpaque=false)
        {
            device.RasterizerState=RasterizerState.CullNone;device.DepthStencilState=DepthStencilState.Default;device.BlendState=BlendState.Opaque;
            surfaces.World=Matrix.Identity;surfaces.View=Matrix.Identity;surfaces.Projection=projection;
            surfaces.TextureEnabled=false;
            foreach(var pass in surfaces.CurrentTechnique.Passes) {pass.Apply();DrawSurface(ground);if(showWalls)DrawSurface(walls);}
            materials.World=Matrix.Identity;materials.View=Matrix.Identity;materials.Projection=projection;device.SamplerStates[0]=SamplerState.PointClamp;
            foreach(var item in materialItems) {
                if(item.Wall&&!showWalls)continue;
                device.SamplerStates[0]=item.Roof?SamplerState.PointWrap:SamplerState.PointClamp;
                materials.Texture=item.Texture;
                foreach(var pass in materials.CurrentTechnique.Passes) {pass.Apply();for(int offset=0;offset<item.Vertices.Length;offset+=18000)device.DrawUserPrimitives(PrimitiveType.TriangleList,item.Vertices,offset,Math.Min(18000,item.Vertices.Length-offset)/3);}
            }
            sprites.Parameters["Projection"].SetValue(projection);
            // Same depth calibration as 2DWorldBatch.fx: a two-tile diagonal span
            // multiplied by (1 - SPR2 depth/255) / 0.4. Projection maps 256 units to [0,1].
            sprites.Parameters["DepthSpan"].SetValue((float)Math.Sqrt(1.5)/0.4f/256f);
            sprites.Parameters["AlphaPass"].SetValue(0f);
            var opaque=reverseOpaque?items.AsEnumerable().Reverse():items;
            foreach(var item in opaque) DrawItem(item);
            // Opaque texels write depth; partial alpha edges only read it, avoiding
            // invisible depth writes. Intersecting translucent surfaces are not OIT.
            device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.DepthRead;
            sprites.Parameters["AlphaPass"].SetValue(1f);
            foreach(var item in items.OrderBy(i=>i.Nearness).ThenBy(i=>i.ID)) DrawItem(item);
        }
        private void DrawSurface(VertexPositionColor[] vertices)
        {
            // Bounded batches avoid primitive-count limits on large lots.
            for(int offset=0;offset<vertices.Length;offset+=18000) device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,offset,Math.Min(18000,vertices.Length-offset)/3);
        }
        private void DrawItem(Item item)
        {
            sprites.Parameters["ColorTexture"].SetValue(item.Color);sprites.Parameters["DepthTexture"].SetValue(item.Depth);
            foreach(var pass in sprites.CurrentTechnique.Passes) {pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,item.Vertices,0,2);}
        }
        public static Matrix Camera(int size,int zoom,int rotation,int width,int height,Vector2 pan)
        {
            float scale=Math.Min((width-40f)/(size*32),(height-60f)/(size*16+90));
            var center=TS1SpriteLayer.Project(new Vector3(size/2f,size/2f,0),zoom,rotation);
            return Matrix.CreateScale(scale,scale,1)*Matrix.CreateTranslation(width/2f-center.X*scale+pan.X,height/2f-center.Y*scale+pan.Y,0)*
                Matrix.CreateOrthographicOffCenter(0,width,height,0,-128,128);
        }
        public void Dispose() {foreach(var item in materialItems)if(item.Texture!=null)item.Texture.Dispose();materialItems.Clear();foreach(var item in items)item.Dispose();items.Clear();if(surfaces!=null)surfaces.Dispose();if(materials!=null)materials.Dispose();if(sprites!=null)sprites.Dispose();}
    }
}
