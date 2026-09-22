using System;
using System.IO;
using FSO.Common.Platform;
using FSO.Common.Platform.Uwp;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using System.Collections.Generic;
using System.Linq;
using FreeSims.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FreeSims.Xbox.Proof
{
    public sealed class CommonProbeGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch batch;
        private Texture2D pixel;
        private List<string> results = new List<string>();
        private GamePadState previous;
        private int runs;
        private GamePaths paths;
        private ProbeSettings persistent;
        private GameScreen screen;
        private ProbeLayer mainLayer;
        private bool storageFailed;
        private string storageStatus = "NOT INITIALIZED";
        private string provisionStatus = "WAITING FOR GAME DATA MARKER";
        private bool firstFrame;
        private bool layoutLogPending = true;
        private Rectangle lastViewportBounds;
        private Rectangle lastClientBounds;
        private int lastBackBufferWidth;
        private int lastBackBufferHeight;

        public CommonProbeGame()
        {
            graphics = new GraphicsDeviceManager(this) {
                PreferredBackBufferWidth = ProbeLayout.Width, PreferredBackBufferHeight = ProbeLayout.Height,
                GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = true
            };
            IsFixedTimeStep = true;
            Activated += (sender, args) => { layoutLogPending = true; ProofLog.Write("ACTIVATED"); };
            Deactivated += (sender, args) => ProofLog.Write("DEACTIVATED");
            graphics.DeviceReset += (sender, args) => { layoutLogPending = true; ProofLog.Write("GRAPHICS DEVICE RESET"); };
            Window.ClientSizeChanged += (sender, args) => { layoutLogPending = true; ProofLog.Write("CLIENT SIZE CHANGED"); };
            Exiting += (sender, args) => ProofLog.Write("EXIT REQUESTED");
        }

        protected override void LoadContent()
        {
            batch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            ProofLog.Write("GRAPHICS READY " + GraphicsDevice.GraphicsProfile);
            PPXDepthEngine.InitGD(GraphicsDevice);
            screen = new GameScreen(GraphicsDevice);
            mainLayer = new ProbeLayer(DrawContent);
            screen.Add(mainLayer);
            try
            {
                paths = UwpGameStorage.CreatePaths();
                paths.ApplyToEnvironment(true);
                ProofLog.Write("PATHS content=" + paths.ContentRoot + " game=" + paths.GameDataRoot + " user=" + paths.UserDataRoot);
                string path = paths.GetUserDataPath("common-probe.ini");
                bool exists = File.Exists(path);
                persistent = new ProbeSettings(path);
                if (!exists) persistent.Store(0,Guid.NewGuid().ToString("N"));
                persistent = new ProbeSettings(path);
                CommonCompatibilityTests.Require(persistent.Counter >= 0 && !string.IsNullOrEmpty(persistent.Marker),"Invalid persistent settings.");
                storageStatus = "PERSISTED VALUE " + persistent.Counter;
                ProofLog.Write((exists ? "PERSISTENT LOADED " : "PERSISTENT CREATED ") + persistent.Counter + " marker=" + persistent.Marker);
            }
            catch (Exception ex) { storageFailed = true; storageStatus = "STORAGE FAILED - SEE LOG"; ProofLog.Write("STORAGE INIT FAILED " + ex); }
            RunTests();
        }

        private void RunTests()
        {
            runs++;
            ProofLog.Write("TEST RUN " + runs + " BEGIN");
            try
            {
                if (paths == null) throw new InvalidOperationException("Storage paths unavailable.");
                results = CommonCompatibilityTests.Run(paths,ProofLog.Write);
                CommonNativeTests.Run(paths,GraphicsDevice,results);
                string provision = paths.GetGameDataPath("game-data-probe.txt");
                if (!File.Exists(provision)) provisionStatus = "WAITING FOR GAME DATA MARKER";
                else provisionStatus = File.ReadAllText(provision).Trim() == "FreeSims external game-data fixture v1" ?
                    "GAME DATA MARKER PASS" : "GAME DATA MARKER FAIL";
                ProofLog.Write(provisionStatus);
                ProofLog.Write("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/12 PASS");
            }
            catch (Exception ex) { results = new List<string> { "FAIL PROBE INITIALIZATION" }; ProofLog.Write("TEST INIT FAILED " + ex); }
            ProofLog.Write("TEST RUN " + runs + " END");
        }

        protected override void Update(GameTime gameTime)
        {

            var pad = GamePad.GetState(PlayerIndex.One);
            if (pad.IsConnected != previous.IsConnected)
                ProofLog.Write("CONTROLLER " + (pad.IsConnected ? "CONNECTED" : "DISCONNECTED"));
            if (pad.IsButtonDown(Buttons.A) && previous.IsButtonUp(Buttons.A)) RunTests();
            if (pad.IsButtonDown(Buttons.B) && previous.IsButtonUp(Buttons.B)) Exit();
            if (pad.IsButtonDown(Buttons.X) && previous.IsButtonUp(Buttons.X)) StoreNextValue();
            previous = pad;
            screen.Update(gameTime,IsActive);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            screen.Draw(gameTime);
            base.Draw(gameTime);
        }

        private void DrawContent(GraphicsDevice device)
        {
            var viewport = GraphicsDevice.Viewport;
            Matrix transform;
            if (!ProbeLayout.TryCreateTransform(viewport.Width, viewport.Height, out transform)) return;
            ObserveDisplay(viewport, transform);
            GraphicsDevice.Clear(new Color(16, 24, 39));
            // Recompute from the live viewport every frame: activation/resize events
            // can precede the framework's final back-buffer update.
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
            PixelText.Draw(batch, pixel, "FREESIMS COMMON PROBE", 48, 36, 4, Color.White);
            PixelText.Draw(batch, pixel, "COMMIT " + BuildInfo.Commit.Substring(0, 12), 48, 80, 2, Color.LightGray);
            bool passed = results.Count == 12 && results.All(x => x.StartsWith("PASS "));
            PixelText.Draw(batch, pixel, passed ? "12/12 PASS" : "TEST FAILURE - SEE LOG",
                48, 122, 3, passed ? Color.LimeGreen : Color.OrangeRed);
            for (int i = 0; i < results.Count; i++)
                PixelText.Draw(batch, pixel, results[i], 48, 162 + i * 24, 2,
                    results[i].StartsWith("PASS ") ? Color.LightGreen : Color.OrangeRed);
            PixelText.Draw(batch, pixel, "A RERUN - X STORE - B EXIT - RUN " + runs, 48, 550, 2, Color.White);
            PixelText.Draw(batch,pixel,storageStatus,48,474,2,storageFailed ? Color.OrangeRed : Color.LightGreen);
            PixelText.Draw(batch,pixel,provisionStatus,48,510,2,provisionStatus.EndsWith("PASS") ? Color.LightGreen : Color.Orange);
            PixelText.Draw(batch,pixel,"ENGINE LAYER FRAMES " + mainLayer.Draws,48,586,2,Color.LightGray);
            batch.Draw(pixel, new Rectangle(48 + (int)(mainLayer.Elapsed * 80 % 1120), 630, 32, 8), Color.CornflowerBlue);
            PixelText.Draw(batch, pixel, "VIEWPORT " + viewport.Width + "X" + viewport.Height +
                " - UI 1280X720", 48, 660, 2, Color.LightGray);
            batch.End();
            if (!firstFrame) { firstFrame = true; ProofLog.Write("FIRST FRAME"); }
        }

        private void StoreNextValue()
        {
            try
            {
                if (persistent == null) throw new InvalidOperationException("Persistent state unavailable.");
                int next = checked(persistent.Counter + 1);
                string marker = persistent.Marker;
                persistent.Store(next,marker);
                var reloaded = new ProbeSettings(paths.GetUserDataPath("common-probe.ini"));
                CommonCompatibilityTests.Require(reloaded.Counter == next && reloaded.Marker == marker,"Persistent write/readback mismatch.");
                persistent = reloaded;
                storageFailed = false;
                storageStatus = "PERSISTED VALUE " + persistent.Counter;
                ProofLog.Write("PERSISTENT STORED " + persistent.Counter + " marker=" + marker);
            }
            catch (Exception ex) { storageFailed = true; storageStatus = "STORAGE FAILED - SEE LOG"; ProofLog.Write("PERSISTENT WRITE FAILED " + ex); }
        }

        private void ObserveDisplay(Viewport viewport, Matrix transform)
        {
            var presentation = GraphicsDevice.PresentationParameters;
            var bounds = viewport.Bounds;
            var client = Window.ClientBounds;
            if (!layoutLogPending && bounds == lastViewportBounds && client == lastClientBounds &&
                presentation.BackBufferWidth == lastBackBufferWidth &&
                presentation.BackBufferHeight == lastBackBufferHeight) return;

            layoutLogPending = false;
            lastViewportBounds = bounds;
            lastClientBounds = client;
            lastBackBufferWidth = presentation.BackBufferWidth;
            lastBackBufferHeight = presentation.BackBufferHeight;
            ProofLog.Write("DISPLAY viewport=" + bounds +
                " backbuffer=" + lastBackBufferWidth + "x" + lastBackBufferHeight +
                " client=" + client + " ui=1280x720 scale=" +
                transform.M11.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " offset=" + transform.M41.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "," + transform.M42.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        protected override void UnloadContent()
        {
            if (pixel != null) pixel.Dispose();
            if (batch != null) batch.Dispose();
            base.UnloadContent();
        }
    }
}
