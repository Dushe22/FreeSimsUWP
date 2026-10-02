using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.System;
using System.IO;
using System.Linq;
using FSO.Common.Platform.Uwp;
using FSO.LotView;
using FSO.SimAntics;
using FreeSims.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace FreeSims.Xbox.Proof
{
    public sealed class OfflineProbeGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch batch; private Texture2D pixel; private RenderTarget2D image;
        private TS1LotRenderData lot; private TS1LotRenderer renderer;
        private byte[] effect; private GamePadState previous;
        private readonly List<string> results=new List<string>();
        private int house=2,rotation,zoom=1,level=1,runs;
        private TS1WallMode walls=TS1WallMode.Up;
        private bool pointerMode,redraw,reset;
        private Vector2 pointer=new Vector2(640,353);
        private int memoryPressure;
        private Vector2 pan; private string error; private Rectangle lastViewport;
        public OfflineProbeGame()
        {
            graphics=new GraphicsDeviceManager(this){PreferredBackBufferWidth=1280,PreferredBackBufferHeight=720,GraphicsProfile=GraphicsProfile.HiDef,SynchronizeWithVerticalRetrace=true};
            IsFixedTimeStep=false;
            Activated+=(s,e)=>{Program.RequestPause();ProofLog.Write("ACTIVATED");};
            Deactivated+=(s,e)=>{Program.RequestPause();ProofLog.Write("DEACTIVATED");};
            graphics.DeviceReset+=(s,e)=>{reset=true;ProofLog.Write("GRAPHICS DEVICE RESET");};
        }
        protected override void LoadContent()
        {
            VM.UseWorld=false;batch=new SpriteBatch(GraphicsDevice);pixel=new Texture2D(GraphicsDevice,1,1);pixel.SetData(new[]{Color.White});
            effect=File.ReadAllBytes(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path,"Effects","TS1SpriteDepth.mgfxo"));
            MemoryManager.AppMemoryUsageIncreased+=MemoryIncreased;
            LogMemory("startup");LoadLot();
        }
        private void MemoryIncreased(object sender,object args)
        {
            // UWP raises this off the game thread; dispose/collect only at a view boundary.
            System.Threading.Interlocked.Exchange(ref memoryPressure,1);
        }
        private void LogMemory(string stage)
        {
            ProofLog.Write("MEMORY "+stage+" appBytes="+MemoryManager.AppMemoryUsage+" limitBytes="+MemoryManager.AppMemoryUsageLimit+
                " managedBytes="+GC.GetTotalMemory(false)+" level="+MemoryManager.AppMemoryUsageLevel);
        }
        private void ReleaseView()
        {
            if(renderer!=null){renderer.Dispose();renderer=null;}
            if(image!=null){image.Dispose();image=null;}
            redraw=false;
        }
        private void CollectReleasedMemory()
        {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        private void ReleaseLot()
        {
            ReleaseView();if(lot!=null){lot.Dispose();lot=null;}
        }
        private void RunTests()
        {
            ReleaseLot();CollectReleasedMemory();results.Clear();runs++;ProofLog.Write("TEST RUN "+runs+" BEGIN");
            try{results.AddRange(LotRenderTests.Run(GraphicsDevice,UwpGameStorage.CreatePaths(),effect,ProofLog.Write));}
            catch(Exception ex){results.Add("FAIL LOT INITIALIZATION");ProofLog.Write(ex.ToString());}
            ProofLog.Write("RESULT "+results.Count(x=>x.StartsWith("PASS "))+"/"+LotRenderTests.Count+" PASS");ProofLog.Write("TEST RUN "+runs+" END");CollectReleasedMemory();LogMemory("tests-complete");LoadLot();
        }
        private void LoadLot()
        {
            // Release the previous VM and GPU scene before allocating a new lot.
            ReleaseLot();CollectReleasedMemory();var timer=Stopwatch.StartNew();LogMemory("load-begin");
            try{lot=new TS1LotRenderData(UwpGameStorage.CreatePaths(),house);TryRebuild();}
            catch(Exception ex){ReleaseLot();error="LOT LOAD FAILED - SEE LOG";ProofLog.Write("LOT LOAD FAILED house="+house+" "+ex);}
            ProofLog.Write("LOT LOAD ms="+timer.ElapsedMilliseconds);LogMemory("load-end");
        }
        private void Rebuild(TS1LotRenderData source)
        {
            // Never keep two complete CPU/GPU views alive across an upload.
            ReleaseView();
            if(MemoryManager.AppMemoryUsageLevel>=AppMemoryUsageLevel.High)CollectReleasedMemory();
            var timer=Stopwatch.StartNew();LogMemory("view-begin");
            TS1LotRenderer candidate=null;RenderTarget2D target=null;
            try{
                var data=source.Build(zoom,rotation,level);candidate=new TS1LotRenderer(GraphicsDevice,data,effect);
                target=new RenderTarget2D(GraphicsDevice,1280,530,false,SurfaceFormat.Color,DepthFormat.Depth24);Render(candidate,target,source.Size);
                if(renderer!=null)renderer.Dispose();if(image!=null)image.Dispose();renderer=candidate;candidate=null;image=target;target=null;error=null;
                ProofLog.Write("LOT VIEW house="+house+" rotation="+rotation+" zoom="+zoom+" level="+level+" walls="+walls+" rendered="+data.Rendered+" hidden="+data.Hidden+" contained="+data.Contained+" unsupported="+data.Unsupported+" floors="+data.FloorTiles+" wallEdges="+data.WallEdges+" floorMaterials="+data.FloorMaterials.Count+" wallMaterials="+data.WallMaterials.Count+" openings="+data.OpeningEdges+" joints="+data.StoryJoints+" roofTriangles="+data.RoofTriangles+" terrain="+data.TerrainTiles+" pools="+data.PoolTiles+" water="+data.WaterTiles+" poolAttachments="+data.PoolAttachmentAdjustments);
                foreach(var issue in data.Issues)ProofLog.Write(issue);
                ProofLog.Write("VIEW COST ms="+timer.ElapsedMilliseconds+" textures="+renderer.TextureCount+" uploadBytes="+renderer.TextureBytes);
                LogMemory("view-end");
            }finally{if(candidate!=null)candidate.Dispose();if(target!=null)target.Dispose();}
        }
        private void TryRebuild()
        {
            try {Rebuild(lot);}
            catch(OutOfMemoryException ex) {
                ProofLog.Write("RENDER OOM house="+house+" rotation="+rotation+" zoom="+zoom+" level="+level+" "+ex);
                ReleaseView();CollectReleasedMemory();LogMemory("oom-released");
                if(zoom>1) {
                    zoom=1;pan=Vector2.Zero;
                    try {Rebuild(lot);error="MEMORY LIMIT - ZOOM RESET";return;}
                    catch(Exception retry){ProofLog.Write("RENDER RECOVERY FAILED "+retry);}
                }
                error="MEMORY LIMIT - X RELOAD LOT";
            }
            catch(Exception ex){ReleaseView();error="RENDER FAILED - SEE LOG";ProofLog.Write("RENDER FAILED house="+house+" rotation="+rotation+" zoom="+zoom+" level="+level+" "+ex);}
        }
        private void Render(TS1LotRenderer scene,RenderTarget2D target,int size)
        {
            var old=GraphicsDevice.GetRenderTargets();var viewport=GraphicsDevice.Viewport;
            try{GraphicsDevice.SetRenderTarget(target);GraphicsDevice.Clear(ClearOptions.Target|ClearOptions.DepthBuffer,new Color(16,24,39),1,0);var camera=TS1LotRenderer.Camera(size,zoom,rotation,target.Width,target.Height,pan);lot.UpdateWalls(scene.Data,walls,pointerMode?(Vector2?)(pointer-new Vector2(0,88)):null,camera,target.Width,target.Height);scene.Draw(camera,walls);}
            finally{GraphicsDevice.SetRenderTargets(old);GraphicsDevice.Viewport=viewport;}
        }
        protected override void Update(GameTime time)
        {
            if(System.Threading.Interlocked.Exchange(ref memoryPressure,0)!=0) {
                CollectReleasedMemory();LogMemory("pressure");
            }
            var pad=GamePad.GetState(PlayerIndex.One);
            if(Program.ConsumePause()||!IsActive||!pad.IsConnected){previous=pad;base.Update(time);return;}
            Func<Buttons,bool> pressed=b=>pad.IsButtonDown(b)&&previous.IsButtonUp(b);
            if(pressed(Buttons.B)){ProofLog.Write("EXIT REQUESTED");Exit();return;}
            if(pressed(Buttons.Start))RunTests();
            bool rebuild=reset;reset=false;if(rebuild)pixel.SetData(new[]{Color.White});
            if(pressed(Buttons.X)){house=house==2?28:2;zoom=1;level=1;rotation=0;pan=Vector2.Zero;LoadLot();rebuild=false;}
            if(pressed(Buttons.A)){rotation=(rotation+1)%4;rebuild=true;}
            if(pressed(Buttons.LeftShoulder)){zoom=zoom%3+1;rebuild=true;}
            if(pressed(Buttons.RightShoulder)){level=level%3+1;rebuild=true;}
            if(pressed(Buttons.Y)){walls=(TS1WallMode)(((int)walls+1)%3);redraw=true;ProofLog.Write("WALL MODE "+walls);}
            if(pressed(Buttons.LeftStick)){pointerMode=!pointerMode;redraw=true;ProofLog.Write("POINTER "+pointerMode);}
            if(pressed(Buttons.Back)){pan=Vector2.Zero;zoom=1;rebuild=true;}
            float dt=(float)Math.Min(time.ElapsedGameTime.TotalSeconds,0.1);
            var stick=pad.ThumbSticks.Left;
            if(pointerMode) {
                if(stick.Length()>0.15f)pointer=Vector2.Clamp(pointer+new Vector2(stick.X,-stick.Y)*dt*420,new Vector2(4,92),new Vector2(1275,613));
            }
            stick=pad.ThumbSticks.Right+(pointerMode?Vector2.Zero:stick);
            if(stick.Length()>1)stick.Normalize();
            if(stick.Length()>0.15f){pan+=new Vector2(-stick.X,stick.Y)*dt*420;pan=Vector2.Clamp(pan,new Vector2(-3000),new Vector2(3000));redraw=true;}
            try {
            if(!rebuild&&lot!=null&&renderer!=null&&image!=null) {
                var camera=TS1LotRenderer.Camera(lot.Size,zoom,rotation,image.Width,image.Height,pan);
                if(lot.UpdateWalls(renderer.Data,walls,pointerMode?(Vector2?)(pointer-new Vector2(0,88)):null,camera,image.Width,image.Height))redraw=true;
            }
            if(rebuild&&lot!=null){TryRebuild();redraw=false;}if(redraw&&renderer!=null&&image!=null){Render(renderer,image,lot.Size);redraw=false;}
            }
            catch(Exception ex){ReleaseView();error="RENDER FAILED - SEE LOG";ProofLog.Write("REDRAW FAILED "+ex);LogMemory("redraw-failed");}
            previous=pad;base.Update(time);
        }
        protected override void Draw(GameTime time)
        {
            var viewport=GraphicsDevice.Viewport;Matrix transform;if(!ProbeLayout.TryCreateTransform(viewport.Width,viewport.Height,out transform))return;
            if(lastViewport!=viewport.Bounds){lastViewport=viewport.Bounds;ProofLog.Write("DISPLAY viewport="+viewport.Bounds+" ui=1280x720");}
            GraphicsDevice.Clear(new Color(16,24,39));batch.Begin(samplerState:SamplerState.PointClamp,transformMatrix:transform);
            Text("FREESIMS TEXTURED LOT RENDER",32,18,3,Color.White);
            bool passed=results.Count==LotRenderTests.Count&&results.All(x=>x.StartsWith("PASS "));
            string status=error!=null?"VIEW ERROR":results.Count==0?"MENU: TESTS":passed?(results.Count+"/"+LotRenderTests.Count+" PASS"):"TEST FAILURE";
            Text(status,970,24,2,error!=null?Color.OrangeRed:results.Count==0?Color.LightGray:passed?Color.LimeGreen:Color.OrangeRed);
            Text("COMMIT "+BuildInfo.Commit.Substring(0,12)+" - HOUSE "+house+" - ANGLE "+rotation+" - LEVEL "+(level==3?"ROOF":level.ToString())+" - ZOOM "+zoom,32,52,2,Color.LightGray);
            if(image!=null)batch.Draw(image,new Vector2(0,88),Color.White);
            Text("WALLS "+walls.ToString().ToUpperInvariant()+" - "+(pointerMode?"POINTER ON":"CAMERA")+" - STATIC - NO SIMULATION",32,625,2,Color.Gold);
            if(pointerMode) {
                int px=(int)pointer.X,py=(int)pointer.Y;
                batch.Draw(pixel,new Rectangle(px-7,py-1,15,3),Color.Black);batch.Draw(pixel,new Rectangle(px-1,py-7,3,15),Color.Black);
                batch.Draw(pixel,new Rectangle(px-6,py,13,1),Color.Gold);batch.Draw(pixel,new Rectangle(px,py-6,1,13),Color.Gold);
            }
            Text("A ROTATE - X HOUSE - Y WALLS - LB ZOOM - RB FLOOR/ROOF",32,653,2,Color.White);
            Text("LS CLICK POINTER - LS MOVE - RS PAN - VIEW CENTER - MENU TESTS - B EXIT",32,681,1,Color.LightGray);
            if(renderer!=null)Text("DRAWN "+renderer.Data.Rendered+" - SLOTTED HELD "+renderer.Data.Contained+" - UNSUPPORTED "+renderer.Data.Unsupported+" - RUN "+runs,32,76,1,Color.LightGray);
            if(error!=null)Text(error,32,596,2,Color.OrangeRed);
            batch.End();base.Draw(time);
        }
        private void Text(string text,int x,int y,int scale,Color color){PixelText.Draw(batch,pixel,text,x,y,scale,color);}
        protected override void UnloadContent(){MemoryManager.AppMemoryUsageIncreased-=MemoryIncreased;ReleaseLot();if(batch!=null)batch.Dispose();if(pixel!=null)pixel.Dispose();base.UnloadContent();}
    }
}
