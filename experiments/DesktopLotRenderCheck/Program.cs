using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common.Platform;
using FSO.LotView;
using FSO.SimAntics;
class Program {
[STAThread] static int Main(string[] args) {
try {
var output=Path.GetFullPath("artifacts/lot-render-check");Directory.CreateDirectory(output);
var paths=new GamePaths(Path.Combine(output,"Content"),Path.GetFullPath(args.Length>0?args[0]:"artifacts/saved-lot-upload/GameData"),Path.Combine(output,"UserData"));
VM.UseWorld=false;
using(var c=new Control()) using(var d=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters {BackBufferWidth=1280,BackBufferHeight=720,DeviceWindowHandle=c.Handle})) {
var effect=File.ReadAllBytes("experiments/XboxOfflineProbe/Effects/TS1SpriteDepth.mgfxo");
if(!FreeSims.Tests.LotRenderTests.Run(d,paths,effect,Console.WriteLine).All(x=>x.StartsWith("PASS "))) return 1;
if(args.Contains("--architecture-qa")) {
var qa=Path.GetFullPath("artifacts/architecture-qa");Directory.CreateDirectory(qa);
foreach(int house in new[]{2,28})using(var lot=new TS1LotRenderData(paths,house))
for(int level=1;level<=3;level++)foreach(int r in new[]{0,2}) {
var data=lot.Build(2,r,level);
using(var renderer=new TS1LotRenderer(d,data,effect))using(var target=new RenderTarget2D(d,1280,720,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
foreach(var mode in new[]{TS1WallMode.Up,TS1WallMode.Down}) {
var camera=TS1LotRenderer.Camera(lot.Size,2,r,1280,720,Vector2.Zero);
lot.UpdateWalls(data,mode,null,camera,1280,720);
d.SetRenderTarget(target);d.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Microsoft.Xna.Framework.Color(16,24,39),1,0);
renderer.Draw(camera,mode);d.SetRenderTarget(null);
var pixels=new Microsoft.Xna.Framework.Color[1280*720];target.GetData(pixels);
SavePixels(pixels,1280,720,Path.Combine(qa,"house"+house+"-level"+level+"-r"+r+"-"+mode+".png"));
Console.WriteLine("ARCH QA house="+house+" level="+level+" r="+r+" mode="+mode+" caps="+renderer.WallCapVertexCount+" capacity="+renderer.WallGeometryCapacity+" textureBytes="+renderer.TextureBytes);
}
}
}
return 0;
}
foreach(int house in new[]{2,28}) using(var lot=new TS1LotRenderData(paths,house)) {
for(int level=1;level<=3;level++) for(int r=0;r<4;r++) {
var data=lot.Build(1,r,level);Console.WriteLine("HOUSE="+house+" LEVEL="+level+" ROT="+r+" SIZE="+lot.Size+" OBJECTS="+lot.ObjectCount+" DRAWN="+data.Rendered+" HIDDEN="+data.Hidden+" CONTAINED="+data.Contained+" NOGRAPHIC="+data.NoGraphic+" OOW="+data.OutOfWorld+" UPPER="+data.AboveLevel+" UNSUPPORTED="+data.Unsupported+" FLOORS="+data.FloorTiles+" WALLS="+data.WallEdges+" OPENINGS="+data.OpeningEdges+" JOINTS="+data.StoryJoints+" ROOF="+data.RoofTriangles);
foreach(var issue in data.Issues) Console.WriteLine(issue);
using(var renderer=new TS1LotRenderer(d,data,effect)) using(var target=new RenderTarget2D(d,1280,720,false,SurfaceFormat.Color,DepthFormat.Depth24)) {
foreach(bool walls in new[]{false,true}) {
d.SetRenderTarget(target);d.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Microsoft.Xna.Framework.Color(16,24,39),1,0);
renderer.Draw(TS1LotRenderer.Camera(lot.Size,1,r,1280,720,Vector2.Zero),walls);
d.SetRenderTarget(null);var pixels=new Microsoft.Xna.Framework.Color[1280*720];target.GetData(pixels);
SavePixels(pixels,1280,720,Path.Combine(output,"house"+house+"-level"+level+"-r"+r+"-walls"+walls+".png"));
}
}
}
}
}
return 0;
} catch(Exception ex){Console.WriteLine(ex);return 1;}
}
static void SavePixels(Microsoft.Xna.Framework.Color[] pixels,int width,int height,string path) {
using(var bitmap=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,width,height),System.Drawing.Imaging.ImageLockMode.WriteOnly,bitmap.PixelFormat);
try {var packed=new int[pixels.Length];for(int i=0;i<pixels.Length;i++){var p=pixels[i];packed[i]=unchecked((int)((uint)p.A<<24|(uint)p.R<<16|(uint)p.G<<8|p.B));}
System.Runtime.InteropServices.Marshal.Copy(packed,0,data.Scan0,packed.Length);}
finally {bitmap.UnlockBits(data);}
bitmap.Save(path);
}
}
}
