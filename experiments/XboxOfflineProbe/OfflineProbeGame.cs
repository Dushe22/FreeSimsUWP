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
        private Texture2D pixel, thumbnail;
        private List<string> results = new List<string>();
        private GamePadState previous;
        private int runs;
        private double elapsed;
        private bool firstFrame;
        private bool layoutLogPending = true;
        private Rectangle lastViewportBounds;
        private Rectangle lastClientBounds;
        private int lastBackBufferWidth;
        private int lastBackBufferHeight;

        public OfflineProbeGame()
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
            RunTests();
        }

        private void RunTests()
        {
            runs++;
            ProofLog.Write("TEST RUN " + runs + " BEGIN");
            try {
                var paths = UwpGameStorage.CreatePaths();
                results = OfflineTests.Run(paths, ProofLog.Write);
                if (thumbnail != null) { thumbnail.Dispose(); thumbnail = null; }
                try {
                    var neighborhood = new FSO.Content.TS1.TS1NeighborhoodProvider(paths,0);
                    var bmp = neighborhood.GetHouseThumb(1);
                    if (bmp == null) throw new InvalidOperationException("House 1 has no BMP 512 thumbnail.");
                    thumbnail = bmp.GetTexture(GraphicsDevice);
                    var data = new Color[thumbnail.Width * thumbnail.Height];
                    thumbnail.GetData(data);
                    if (thumbnail.Width < 2 || thumbnail.Height < 2 || data.Distinct().Take(2).Count() < 2)
                        throw new InvalidOperationException("Thumbnail is empty or uniform.");
                    results.Add("PASS HOUSE THUMBNAIL GPU READBACK");
                    ProofLog.Write("PASS HOUSE THUMBNAIL GPU READBACK " + thumbnail.Width + "x" + thumbnail.Height);
                } catch (Exception ex) { results.Add("FAIL HOUSE THUMBNAIL GPU READBACK"); ProofLog.Write(ex.ToString()); }
                ProofLog.Write("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/9 PASS");
            }
            catch (Exception ex) { results = new List<string> { "FAIL STORAGE INITIALIZATION" }; ProofLog.Write(ex.ToString()); }
            ProofLog.Write("TEST RUN " + runs + " END");
        }

        protected override void Update(GameTime gameTime)
        {
            elapsed += gameTime.ElapsedGameTime.TotalSeconds;
            var pad = GamePad.GetState(PlayerIndex.One);
            if (pad.IsConnected != previous.IsConnected)
                ProofLog.Write("CONTROLLER " + (pad.IsConnected ? "CONNECTED" : "DISCONNECTED"));
            if (IsActive && pad.IsButtonDown(Buttons.A) && previous.IsButtonUp(Buttons.A)) RunTests();
            if (IsActive && pad.IsButtonDown(Buttons.B) && previous.IsButtonUp(Buttons.B)) Exit();
            previous = pad;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            Matrix transform;
            if (!ProbeLayout.TryCreateTransform(viewport.Width, viewport.Height, out transform)) return;
            ObserveDisplay(viewport, transform);
            GraphicsDevice.Clear(new Color(16, 24, 39));
            // Recompute from the live viewport every frame: activation/resize events
            // can precede the framework's final back-buffer update.
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
            PixelText.Draw(batch, pixel, "FREESIMS OFFLINE PROBE", 48, 36, 4, Color.White);
            PixelText.Draw(batch, pixel, "COMMIT " + BuildInfo.Commit.Substring(0, 12), 48, 80, 2, Color.LightGray);
            bool passed = results.Count == 9 && results.All(x => x.StartsWith("PASS "));
            PixelText.Draw(batch, pixel, passed ? "9/9 PASS" : "TEST FAILURE - SEE LOG",
                48, 122, 3, passed ? Color.LimeGreen : Color.OrangeRed);
            for (int i = 0; i < results.Count; i++)
                PixelText.Draw(batch, pixel, results[i], 48, 175 + i * 32, 2,
                    results[i].StartsWith("PASS ") ? Color.LightGreen : Color.OrangeRed);
            PixelText.Draw(batch, pixel, "A RERUN - B EXIT - RUN " + runs, 48, 534, 2, Color.White);
            PixelText.Draw(batch, pixel, "EMPTY VM LOT - HOUSE PREVIEW ONLY", 48, 574, 2, Color.LightGray);
            batch.Draw(pixel, new Rectangle(48 + (int)(elapsed * 80 % 1120), 630, 32, 8), Color.CornflowerBlue);
            PixelText.Draw(batch, pixel, "VIEWPORT " + viewport.Width + "X" + viewport.Height +
                " - UI 1280X720", 48, 660, 2, Color.LightGray);
            if (thumbnail != null) {
                float scale = Math.Min(300f/thumbnail.Width, 180f/thumbnail.Height);
                batch.Draw(thumbnail,new Rectangle(900,455,(int)(thumbnail.Width*scale),(int)(thumbnail.Height*scale)),Color.White);
            }
            batch.End();
            if (!firstFrame) { firstFrame = true; ProofLog.Write("FIRST FRAME"); }
            base.Draw(gameTime);
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
            if (thumbnail != null) thumbnail.Dispose();
            if (pixel != null) pixel.Dispose();
            if (batch != null) batch.Dispose();
            base.UnloadContent();
        }
    }
}