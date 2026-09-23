using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Client;
using FSO.Client.Platform;
using FSO.Client.UI;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Platform;
using FSO.Common.Platform.Uwp;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FreeSims.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TSO.HIT;

namespace FreeSims.Xbox.Proof
{
    public sealed class RuntimeProbeGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly UpdateState state = new UpdateState();
        private readonly ControllerPointer pointer = new ControllerPointer(new Vector2(230, 466));
        private GamePaths paths;
        private GlobalSettings settings;
        private UILayer ui;
        private UIButton button;
        private Texture2D pixel, buttonTexture;
        private RenderTarget2D canvas;
        private SpriteBatch overlay;
        private SpriteFont font;
        private GamePadState previous;
        private List<string> results = new List<string>();
        private bool testing, ready, displayPending = true;
        private int clicks, runs;
        private string status = "INITIALIZING";
        private Rectangle lastViewport;
        private static readonly Color ButtonColor = new Color(40, 90, 150);

        public RuntimeProbeGame()
        {
            graphics = new GraphicsDeviceManager(this) {
                PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720,
                GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = true
            };
            IsFixedTimeStep = true;
            Activated += (s,e) => { CancelInput(); displayPending = true; ProofLog.Write("ACTIVATED"); };
            Deactivated += (s,e) => { CancelInput(); ProofLog.Write("DEACTIVATED"); };
            Exiting += (s,e) => ProofLog.Write("EXIT REQUESTED");
            graphics.DeviceReset += (s,e) => { displayPending = true; ProofLog.Write("DEVICE RESET"); };
        }

        protected override void LoadContent()
        {
            overlay = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            try
            {
                paths = UwpGameStorage.CreatePaths();
                paths.ApplyToEnvironment(true); // Before any access to the client singleton.
                FSOEnvironment.Linux = false;
                FSOEnvironment.DPIScaleFactor = 1;
                settings = GlobalSettings.Default;
                settings.GraphicsWidth = 1280;
                settings.GraphicsHeight = 720;
                settings.DebugEnabled = false;
                settings.Save();
                status = "HINTS " + (settings.ShowHints ? "ON" : "OFF") + " - LOADED";
                ProofLog.Write("CLIENT SETTINGS " + paths.GetUserDataPath("config.ini") + " ShowHints=" + settings.ShowHints);
                PPXDepthEngine.InitGD(GraphicsDevice);
                canvas = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
                font = ProbeFont.Create(GraphicsDevice);
                GameFacade.MainFont = new FSO.Client.UI.Framework.Font();
                GameFacade.MainFont.AddSize(10,font);
                ui = new UILayer(this, font, font);
                GameFacade.Screens = ui;
                GameFacade.GraphicsDevice = GraphicsDevice;
                GameFacade.GraphicsDeviceManager = graphics;
                GameFacade.DirectX = true;
                buttonTexture = new Texture2D(GraphicsDevice, 256, 64);
                var colors = new[] { ButtonColor, Color.Goldenrod, Color.CornflowerBlue, Color.DimGray };
                var pixels = new Color[256 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 256; x++) pixels[y * 256 + x] = colors[x / 64];
                buttonTexture.SetData(pixels);
                button = new UIButton(buttonTexture) { X = 96, Y = 438, Width = 320, Caption = "TOGGLE HINTS" };
                button.OnButtonClick += OnClick;
                var root = new UIScreen();
                root.Add(button);
                ui.AddScreen(root);
                ready = true;
                RunTests();
            }
            catch (Exception ex)
            {
                ready = false;
                status = "INITIALIZATION FAILED - SEE LOG";
                ProofLog.Write("INITIALIZATION FAILED " + ex);
            }
        }

        private void OnClick(UIElement element)
        {
            clicks++;
            if (testing) return;
            try
            {
                settings.ShowHints = !settings.ShowHints;
                settings.Save();
                var loaded = new GlobalSettings(paths.GetUserDataPath("config.ini"));
                CommonCompatibilityTests.Require(loaded.ShowHints == settings.ShowHints, "Client settings readback mismatch.");
                status = "HINTS " + (settings.ShowHints ? "ON" : "OFF") + " - SAVED";
                ProofLog.Write("UI CLICK " + clicks + " ShowHints=" + settings.ShowHints + " READBACK PASS");
            }
            catch (Exception ex) { status = "SAVE FAILED - SEE LOG"; ProofLog.Write("SAVE FAILED " + ex); }
        }

        private void CancelInput()
        {
            pointer.Cancel();
            if (ui != null) ui.inputManager.CancelMouseCapture(state);
        }

        private void Feed(int x, int y, bool down)
        {
            state.Time = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.0 / 60));
            state.MouseState = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            state.Update();
            ui.Update(state);
        }

        private void RunTests()
        {
            runs++;
            testing = true;
            ProofLog.Write("TEST RUN " + runs + " BEGIN");
            try
            {
                results = ClientSettingsTests.Run(paths.GetUserDataPath("ClientSettingsTests"), ProofLog.Write);
                results.AddRange(ControllerPointerTests.Run(ProofLog.Write));
                Check("CLIENT UI CLICK ROUTE", () => {
                    CancelInput(); clicks = 0;
                    Feed(200,466,false); Feed(200,466,false);
                    Feed(200,466,true); Feed(200,466,true); Feed(200,466,false);
                    Require(clicks == 1, "Expected exactly one shared UIButton click.");
                });
                Check("CLIENT UI CANCEL ROUTE", () => {
                    CancelInput(); clicks = 0;
                    Feed(200,466,false); Feed(200,466,false); Feed(200,466,true);
                    CancelInput(); Feed(200,466,false);
                    Require(clicks == 0 && button.CurrentFrame != 64, "Canceled press clicked or stayed down.");
                });
                Check("CLIENT UI DRAW READBACK", () => {
                    CancelInput(); Feed(0,0,false); Feed(0,0,false);
                    GraphicsDevice.SetRenderTarget(canvas);
                    GraphicsDevice.Clear(Color.Black);
                    ui.PreDraw(GraphicsDevice);
                    ui.Draw(GraphicsDevice);
                    GraphicsDevice.SetRenderTarget(null);
                    var sample = new Color[1];
                    canvas.GetData(0, new Rectangle(110,450,1,1), sample, 0, 1);
                    Require(sample[0] == ButtonColor, "Shared UI renderer pixel mismatch.");
                });
                Check("OFFLINE UI BOOT", () => {
                    Require(FSO.Content.Content.Get() == null && HITVM.Get() == null &&
                        ClientPlatform.ShowVmDebugger == null, "Legacy content/audio or desktop debugger initialized.");
                });
                ProofLog.Write("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/10 PASS");
            }
            catch (Exception ex) { results.Add("FAIL TEST INITIALIZATION"); ProofLog.Write("TEST FAILURE " + ex); }
            finally { GraphicsDevice.SetRenderTarget(null); testing = false; clicks = 0; CancelInput(); }
            ProofLog.Write("TEST RUN " + runs + " END");
        }

        private void Check(string name, Action action) { CommonCompatibilityTests.Check(results, ProofLog.Write, name, action); }
        private static void Require(bool condition, string message) { CommonCompatibilityTests.Require(condition, message); }

        protected override void Update(GameTime gameTime)
        {
            var pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None);
            if (previous.IsConnected && !pad.IsConnected) { CancelInput(); ProofLog.Write("CONTROLLER DISCONNECTED"); }
            if (!previous.IsConnected && pad.IsConnected) ProofLog.Write("CONTROLLER CONNECTED");
            if (IsActive && pad.IsButtonDown(Buttons.Start) && previous.IsButtonUp(Buttons.Start)) Exit();
            if (ready)
            {
                if (IsActive && pad.IsButtonDown(Buttons.B) && previous.IsButtonUp(Buttons.B)) CancelInput();
                if (IsActive && pad.IsButtonDown(Buttons.Y) && previous.IsButtonUp(Buttons.Y)) RunTests();
                state.Time = gameTime;
                state.MouseState = pointer.Update(pad.ThumbSticks.Left, pad.IsButtonDown(Buttons.A),
                    IsActive && pad.IsConnected, gameTime.ElapsedGameTime.TotalSeconds, 1280, 720);
                state.SharedData.Clear();
                state.Update();
                ui.Update(state);
                GameFacade.LastUpdateState = state;
            }
            previous = pad;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            Matrix transform;
            if (!ProbeLayout.TryCreateTransform(viewport.Width, viewport.Height, out transform)) return;
            if (displayPending || lastViewport != viewport.Bounds)
            {
                ProofLog.Write("DISPLAY viewport=" + viewport.Bounds + " client=" + Window.ClientBounds + " UI=1280x720 scale=" + transform.M11);
                lastViewport = viewport.Bounds; displayPending = false;
            }
            if (ready)
            {
                GraphicsDevice.SetRenderTarget(canvas);
                GraphicsDevice.Clear(new Color(16,24,39));
                ui.PreDraw(GraphicsDevice);
                ui.Draw(GraphicsDevice);
                GraphicsDevice.SetRenderTarget(null);
            }
            GraphicsDevice.Clear(Color.Black);
            overlay.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
            if (ready) overlay.Draw(canvas, Vector2.Zero, Color.White);
            PixelText.Draw(overlay,pixel,"FREESIMS RUNTIME PROBE",48,30,4,Color.White);
            PixelText.Draw(overlay,pixel,"COMMIT " + BuildInfo.Commit.Substring(0,12),48,76,2,Color.LightGray);
            bool passed = results.Count == 10 && results.All(x => x.StartsWith("PASS "));
            PixelText.Draw(overlay,pixel,passed ? "10/10 PASS" : "TESTS FAILED - SEE LOG",48,114,3,passed ? Color.LimeGreen : Color.OrangeRed);
            for (int i = 0; i < results.Count; i++)
                PixelText.Draw(overlay,pixel,results[i],48,156 + i*24,2,results[i].StartsWith("PASS ") ? Color.LightGreen : Color.OrangeRed);

            PixelText.Draw(overlay,pixel,status,48,528,2,Color.White);
            PixelText.Draw(overlay,pixel,"LEFT STICK MOVE - A CLICK - B CANCEL",48,566,2,Color.LightGray);
            PixelText.Draw(overlay,pixel,"Y RERUN - MENU EXIT - CLICKS " + clicks,48,598,2,Color.LightGray);
            PixelText.Draw(overlay,pixel,"CLIENT UI ONLY - GAME LOADING IS NEXT",48,650,2,Color.LightGray);
            if (ready)
            {
                int x = (int)pointer.Position.X, y = (int)pointer.Position.Y;
                overlay.Draw(pixel,new Rectangle(x-7,y,15,2),Color.White);
                overlay.Draw(pixel,new Rectangle(x,y-7,2,15),Color.White);
            }
            overlay.End();
            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            if (ui != null) ui.SpriteBatch.Dispose();
            if (canvas != null) canvas.Dispose();
            if (font != null) font.Texture.Dispose();
            if (buttonTexture != null) buttonTexture.Dispose();
            if (pixel != null) pixel.Dispose();
            if (overlay != null) overlay.Dispose();
            base.UnloadContent();
        }
    }
}
