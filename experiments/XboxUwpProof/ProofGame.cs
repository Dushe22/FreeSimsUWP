using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FreeSims.Xbox.Proof
{
    public sealed class ProofGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch batch;
        private Texture2D pixel;
        private SoundEffect tone;
        private GamePadState previous;
        private Vector2 pointer = new Vector2(640, 400);
        private bool paused;
        private bool alternate;
        private bool drewFrame;
        private float phase;
        private double elapsed;
        private string audioStatus = "NOT INITIALIZED";
        private int clicks;

        public ProofGame()
        {
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720,
                GraphicsProfile = GraphicsProfile.HiDef,
                SynchronizeWithVerticalRetrace = true
            };
            IsFixedTimeStep = true;
            Activated += (sender, args) => ProofLog.Write("ACTIVATED");
            Deactivated += (sender, args) => ProofLog.Write("DEACTIVATED");
            graphics.DeviceReset += (sender, args) => ProofLog.Write("GRAPHICS DEVICE RESET");
            ProofLog.Write("GAME CONSTRUCTED");
        }

        protected override void LoadContent()
        {
            batch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            ProofLog.Write("GRAPHICS READY profile=" + GraphicsDevice.GraphicsProfile +
                " viewport=" + GraphicsDevice.Viewport.Width + "x" + GraphicsDevice.Viewport.Height);
            try
            {
                const int rate = 44100;
                const int samples = rate / 4;
                var pcm = new byte[samples * 2];
                for (int i = 0; i < samples; i++)
                {
                    // Generated 440 Hz PCM with short fades; no external audio asset.
                    double envelope = Math.Min(1.0, Math.Min(i / 220.0, (samples - 1 - i) / 220.0));
                    short value = (short)(Math.Sin(i * Math.PI * 2 * 440 / rate) * 5000 * envelope);
                    pcm[i * 2] = (byte)(value & 255);
                    pcm[i * 2 + 1] = (byte)((value >> 8) & 255);
                }
                tone = new SoundEffect(pcm, rate, AudioChannels.Mono);
                audioStatus = "READY - PRESS X";
                ProofLog.Write("AUDIO INITIALIZED");
            }
            catch (Exception ex)
            {
                audioStatus = "INIT FAILED - SEE LOG";
                ProofLog.Write("AUDIO INIT FAILED " + ex);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);
            if (pad.IsConnected != previous.IsConnected)
                ProofLog.Write("CONTROLLER " + (pad.IsConnected ? "CONNECTED" : "DISCONNECTED"));
            float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.1f);
            pointer += new Vector2(pad.ThumbSticks.Left.X, -pad.ThumbSticks.Left.Y) * 500 * dt;
            pointer.X = MathHelper.Clamp(pointer.X, 24, 1256);
            pointer.Y = MathHelper.Clamp(pointer.Y, 24, 696);
            if (Pressed(pad, Buttons.A))
            {
                alternate = !alternate;
                clicks++;
                ProofLog.Write("A SELECT count=" + clicks);
            }
            if (Pressed(pad, Buttons.B))
            {
                pointer = new Vector2(640, 400);
                ProofLog.Write("B RESET POINTER");
            }
            if (Pressed(pad, Buttons.Start))
            {
                paused = !paused;
                ProofLog.Write("MENU PAUSE=" + paused);
            }
            if (Pressed(pad, Buttons.X))
            {
                try
                {
                    bool submitted = tone != null && tone.Play();
                    audioStatus = submitted ? "TONE SUBMITTED - VERIFY SOUND" : "PLAY FAILED - SEE LOG";
                    ProofLog.Write("X AUDIO PLAY submitted=" + submitted);
                }
                catch (Exception ex)
                {
                    audioStatus = "PLAY FAILED - SEE LOG";
                    ProofLog.Write("AUDIO PLAY FAILED " + ex);
                }
            }
            if (!paused) phase += dt;
            elapsed += dt;
            if (elapsed >= 30)
            {
                elapsed = 0;
                ProofLog.Write("HEARTBEAT controller=" + pad.IsConnected + " paused=" + paused);
            }
            previous = pad;
            base.Update(gameTime);
        }

        private bool Pressed(GamePadState current, Buttons button)
        {
            return current.IsButtonDown(button) && previous.IsButtonUp(button);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(16, 24, 39));
            var scale = Matrix.CreateScale(GraphicsDevice.Viewport.Width / 1280f,
                GraphicsDevice.Viewport.Height / 720f, 1);
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: scale);
            Text("FREESIMS XBOX UWP PROOF", 64, 54, 4, Color.White);
            Text("MONOGAME 3.8.1 / DIRECTX / X64 / RELEASE", 64, 104, 2, Color.LightGray);
            Text("COMMIT " + BuildInfo.Commit.Substring(0, 12), 64, 132, 2, Color.LightGray);
            Text(previous.IsConnected ? "CONTROLLER CONNECTED" : "CONNECT CONTROLLER 1", 64, 182, 3,
                previous.IsConnected ? Color.LimeGreen : Color.Orange);
            Text("LEFT STICK MOVE POINTER", 64, 235, 2, Color.White);
            Text("A CHANGE COLOR / B RESET POINTER", 64, 266, 2, Color.White);
            Text("X PLAY TONE / MENU PAUSE ANIMATION", 64, 297, 2, Color.White);
            Text("AUDIO " + audioStatus, 64, 340, 2, Color.LightGray);
            Text("SELECT COUNT " + clicks, 64, 371, 2, Color.LightGray);
            Text(paused ? "ANIMATION PAUSED" : "ANIMATION RUNNING", 64, 402, 2, Color.LightGray);
            batch.Draw(pixel, new Rectangle(64 + (int)((Math.Sin(phase) + 1) * 390), 490, 110, 70),
                alternate ? Color.Orange : Color.CornflowerBlue);
            batch.Draw(pixel, new Rectangle((int)pointer.X - 12, (int)pointer.Y - 2, 24, 4), Color.White);
            batch.Draw(pixel, new Rectangle((int)pointer.X - 2, (int)pointer.Y - 12, 4, 24), Color.White);
            Text("NO SIMS DATA LOADED - TOOLCHAIN TEST ONLY", 64, 625, 2, Color.LightGray);
            Text("LOG LOCALSTATE/PROOF.LOG", 64, 656, 2, Color.LightGray);
            batch.End();
            if (!drewFrame)
            {
                drewFrame = true;
                ProofLog.Write("FIRST DRAW SUBMITTED");
            }
            base.Draw(gameTime);
        }

        private void Text(string value, int x, int y, int size, Color color)
        {
            PixelText.Draw(batch, pixel, value, x, y, size, color);
        }

        protected override void UnloadContent()
        {
            tone?.Dispose();
            pixel?.Dispose();
            batch?.Dispose();
            ProofLog.Write("CONTENT UNLOADED");
            base.UnloadContent();
        }
    }
}
