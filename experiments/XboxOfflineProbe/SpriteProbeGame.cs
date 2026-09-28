using System;
using System.Collections.Generic;
using System.Linq;
using FreeSims.Tests;
using FSO.Common.Platform.Uwp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FreeSims.Xbox.Proof
{
    public sealed class OfflineProbeGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch batch;
        private Texture2D pixel;
        private TS1SpriteFixture fixture;
        private SpriteGpuScene scene;
        private List<string> results=new List<string>();
        private GamePadState previous;
        private bool sofa, light, rebuild;
        private int zoom=3, rotation, runs;
        private Rectangle lastViewport;
        private const int TestCount=TS1SpriteTests.Count+2;
        private string error;
        public OfflineProbeGame()
        {
            graphics=new GraphicsDeviceManager(this) {
                PreferredBackBufferWidth=ProbeLayout.Width,PreferredBackBufferHeight=ProbeLayout.Height,
                GraphicsProfile=GraphicsProfile.HiDef,SynchronizeWithVerticalRetrace=true
            };
            IsFixedTimeStep=false;
            Activated+=(s,e)=>{ Program.RequestPause(); ProofLog.Write("ACTIVATED"); };
            Deactivated+=(s,e)=>{ Program.RequestPause(); ProofLog.Write("DEACTIVATED"); };
            graphics.DeviceReset+=(s,e)=>{ rebuild=true; ProofLog.Write("GRAPHICS DEVICE RESET"); };
            Exiting+=(s,e)=>ProofLog.Write("EXIT REQUESTED");
        }
        protected override void LoadContent()
        {
            batch=new SpriteBatch(GraphicsDevice); pixel=new Texture2D(GraphicsDevice,1,1); pixel.SetData(new[]{Color.White});
            ProofLog.Write("GRAPHICS READY "+GraphicsDevice.GraphicsProfile); RunTests();
        }
        private void RunTests()
        {
            runs++; ProofLog.Write("TEST RUN "+runs+" BEGIN");
            if(scene!=null) {scene.Dispose();scene=null;}
            if(fixture!=null) {fixture.Dispose();fixture=null;}
            error=null;
            try {
                var paths=UwpGameStorage.CreatePaths();
                results=TS1SpriteTests.Run(paths,ProofLog.Write);
                fixture=new TS1SpriteFixture(paths);
                Check("GPU COLOR AND DEPTH UPLOAD",()=>ForEachView(s=>s.CheckUpload()));
                Check("GPU ALPHA FLIP AND SOFA LAYERS",()=>ForEachView(s=>s.CheckComposite(GraphicsDevice,batch)));
                SelectView();
            } catch(Exception ex) {error="CONTENT ERROR - SEE LOG";results.Add("FAIL SPRITE CONTENT");ProofLog.Write(ex.ToString());}
            ProofLog.Write("RESULT "+results.Count(x=>x.StartsWith("PASS "))+"/"+TestCount+" PASS");
            ProofLog.Write("TEST RUN "+runs+" END");
        }
        private void Check(string name,Action action)
        {
            try {action();results.Add("PASS "+name);} catch(Exception ex) {results.Add("FAIL "+name);ProofLog.Write(ex.ToString());}
            ProofLog.Write(results.Last());
        }
        private void ForEachView(Action<SpriteGpuScene> action)
        {
            for(int objectIndex=0;objectIndex<2;objectIndex++) for(int z=1;z<=3;z++) for(int r=0;r<4;r++) {
                using(var test=new SpriteGpuScene(GraphicsDevice,fixture.Read(objectIndex==1,z,r))) action(test);
                ProofLog.Write("GPU VIEW object="+objectIndex+" zoom="+z+" rotation="+r);
            }
        }
        private void SelectView()
        {
            if(fixture==null) return;
            try {
                var candidate=new SpriteGpuScene(GraphicsDevice,fixture.Read(sofa,zoom,rotation));
                if(scene!=null) scene.Dispose(); scene=candidate; error=null;
                ProofLog.Write("VIEW "+(sofa?"SOFA":"CHAIR")+" zoom="+zoom+" rotation="+rotation+" layers="+scene.Source.Layers.Count);
            } catch(Exception ex) {error="VIEW ERROR - SEE LOG";ProofLog.Write(ex.ToString());}
        }
        protected override void Update(GameTime time)
        {
            var pad=GamePad.GetState(PlayerIndex.One);
            if(Program.ConsumePause() || !IsActive || !pad.IsConnected) {previous=pad;base.Update(time);return;}
            if(rebuild) {rebuild=false;SelectView();}
            if(pad.IsButtonDown(Buttons.B)&&previous.IsButtonUp(Buttons.B)) {Exit();return;}
            if(pad.IsButtonDown(Buttons.Start)&&previous.IsButtonUp(Buttons.Start)) RunTests();
            if(pad.IsButtonDown(Buttons.A)&&previous.IsButtonUp(Buttons.A)) {rotation=(rotation+1)%4;SelectView();}
            if(pad.IsButtonDown(Buttons.X)&&previous.IsButtonUp(Buttons.X)) {sofa=!sofa;SelectView();}
            if(pad.IsButtonDown(Buttons.Y)&&previous.IsButtonUp(Buttons.Y)) {zoom=zoom%3+1;SelectView();}
            if(pad.IsButtonDown(Buttons.LeftShoulder)&&previous.IsButtonUp(Buttons.LeftShoulder)) light=!light;
            previous=pad; base.Update(time);
        }
        protected override void Draw(GameTime time)
        {
            var vp=GraphicsDevice.Viewport; Matrix transform;
            if(!ProbeLayout.TryCreateTransform(vp.Width,vp.Height,out transform)) return;
            if(vp.Bounds!=lastViewport) {lastViewport=vp.Bounds;ProofLog.Write("DISPLAY viewport="+vp.Bounds+" ui=1280x720");}
            GraphicsDevice.Clear(new Color(16,24,39));
            batch.Begin(blendState:BlendState.AlphaBlend,samplerState:SamplerState.PointClamp,transformMatrix:transform);
            Text("FREESIMS OBJECT SPRITE PROBE",48,36,4,Color.White);
            Text("COMMIT "+BuildInfo.Commit.Substring(0,12),48,80,2,Color.LightGray);
            bool pass=results.Count==TestCount&&results.All(x=>x.StartsWith("PASS "));
            Text(pass?TestCount+"/"+TestCount+" PASS":"TEST FAILURE - SEE LOG",48,122,3,pass?Color.LimeGreen:Color.OrangeRed);
            for(int i=0;i<results.Count;i++) Text(results[i],48,180+i*28,2,results[i].StartsWith("PASS ")?Color.LightGreen:Color.OrangeRed);
            Text((sofa?"SOFA - THREE VM TILES":"CHAIR - ONE VM TILE"),800,142,2,Color.White);
            Text("ANGLE "+rotation+" - ZOOM "+zoom,800,174,2,Color.LightGray);
            for(int y=0;y<10;y++) for(int x=0;x<13;x++) batch.Draw(pixel,new Rectangle(800+x*32,210+y*32,32,32),
                ((x+y)%2==0)?(light?new Color(215,215,215):new Color(32,42,55)):(light?Color.White:new Color(55,68,83)));
            if(scene!=null) {
                var b=scene.Source.Bounds;
                float scale=Math.Min(2f,Math.Min(380f/b.Width,280f/b.Height));
                scene.Draw(batch,new Vector2(1008-b.Width*scale/2,370-b.Height*scale/2),scale);
            }
            if(error!=null) Text(error,48,420,2,Color.OrangeRed);
            Text("REAL OBJECT SPRITES - NO HOUSE THUMBNAIL",48,454,2,Color.LightGray);
            Text("A ROTATE - X OBJECT - Y ZOOM - LB BACKGROUND",48,566,2,Color.White);
            Text("MENU RERUN - B EXIT - RUN "+runs,48,600,2,Color.LightGray);
            Text("VIEWPORT "+vp.Width+"X"+vp.Height+" - UI 1280X720",48,660,2,Color.LightGray);
            batch.End();base.Draw(time);
        }
        private void Text(string text,int x,int y,int scale,Color color) {PixelText.Draw(batch,pixel,text,x,y,scale,color);}
        protected override void UnloadContent()
        {
            if(scene!=null)scene.Dispose();if(fixture!=null)fixture.Dispose();if(pixel!=null)pixel.Dispose();if(batch!=null)batch.Dispose();base.UnloadContent();
        }
    }
}
