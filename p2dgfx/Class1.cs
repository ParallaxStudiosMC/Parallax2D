using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace p2dgfx
{
    public enum RenderMode
    {
        GDI
    }

    public static class Input
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static bool[] currentState = new bool[256];
        private static bool[] previousState = new bool[256];

        public static void Update()
        {
            for (int i = 0; i < 256; i++)
            {
                previousState[i] = currentState[i];
                currentState[i] = (GetAsyncKeyState(i) & 0x8000) != 0;
            }
        }

        public static bool GetKey(Keys key) => currentState[(int)key];
        public static bool GetKeyDown(Keys key) => currentState[(int)key] && !previousState[(int)key];
        public static bool GetKeyUp(Keys key) => !currentState[(int)key] && previousState[(int)key];
    }

    public static class Engine
    {
        private static GameWindow window;
        private static Dictionary<int, Bitmap> sprites = new Dictionary<int, Bitmap>();
        private static int nextSpriteId = 1;

        private static Action<float> updateCallback;
        internal static Action drawCallback;

        private static Point mousePosition;
        private static MouseButtons mouseButtons;
        private static MouseButtons mouseButtonsPrevious;

        private static float deltaTime;
        private static float timeScale = 1f;
        private static DateTime lastTime = DateTime.Now;
        private static float targetFrameRate = 0;
        private static System.Windows.Forms.Timer gameTimer;

        internal static float shakeIntensity = 0f;
        internal static float shakeDuration = 0f;
        internal static Random shakeRandom = new Random();
        internal static float distortionIntensity = 0f;
        internal static float distortionSpeed = 0f;
        internal static float distortionTime = 0f;
        internal static bool useSpotlight = false;
        internal static float spotlightRadius = 100f;
        internal static PointF spotlightCenter;
        private static bool additiveBlending = false;

        public static float RawDeltaTime => deltaTime;
        public static float DeltaTime => deltaTime * timeScale;
        public static float TimeScale { get => timeScale; set => timeScale = value; }

        public static int WindowWidth => window?.ClientSize.Width ?? 800;
        public static int WindowHeight => window?.ClientSize.Height ?? 600;
        public static bool Fullscreen { get; private set; }

        // Audio with volume control
        private static Dictionary<string, SoundPlayerWrapper> soundPlayers = new Dictionary<string, SoundPlayerWrapper>();
        private static float masterVolume = 1f;
        private static float soundVolume = 1f;
        private static float musicVolume = 1f;

        private static Random random = new Random();

        private static bool vsyncEnabled = true;
        public static void SetVSync(bool enable) => vsyncEnabled = enable;

        // Volume control methods
        public static void SetMasterVolume(float volume) { masterVolume = Math.Clamp(volume, 0f, 1f); }
        public static void SetSoundVolume(float volume) { soundVolume = Math.Clamp(volume, 0f, 1f); }
        public static void SetMusicVolume(float volume) { musicVolume = Math.Clamp(volume, 0f, 1f); }

        private static float GetEffectiveVolume(string filePath)
        {
            // Assume music if filename contains "music" or "bg_", otherwise sound
            bool isMusic = filePath.ToLower().Contains("music") || filePath.ToLower().Contains("bg_");
            return masterVolume * (isMusic ? musicVolume : soundVolume);
        }

        // Original Init with 5 parameters
        public static void Init(int width, int height, string title,
                                Action<float> update, Action draw)
        {
            Init(width, height, title, update, draw, RenderMode.GDI);
        }

        // 6‑parameter Init (ignores render mode, always GDI)
        public static void Init(int width, int height, string title,
                                Action<float> update, Action draw,
                                RenderMode mode)
        {
            updateCallback = update;
            drawCallback = draw;

            window = new GameWindow();
            window.ClientSize = new Size(width, height);
            window.Text = title;

            window.MouseMove += (s, e) => mousePosition = e.Location;
            window.MouseDown += (s, e) => mouseButtons |= e.Button;
            window.MouseUp += (s, e) => mouseButtons &= ~e.Button;

            gameTimer = new System.Windows.Forms.Timer();
            gameTimer.Interval = 1;
            gameTimer.Tick += GameLoopTick;
            gameTimer.Start();

            Application.Run(window);
        }

        private static void GameLoopTick(object sender, EventArgs e)
        {
            Input.Update();

            DateTime now = DateTime.Now;
            deltaTime = Math.Max(0.0001f, (float)(now - lastTime).TotalSeconds);
            lastTime = now;

            mouseButtonsPrevious = mouseButtons;

            if (shakeDuration > 0)
            {
                shakeDuration -= deltaTime;
                if (shakeDuration <= 0)
                    shakeIntensity = 0;
            }
            if (distortionIntensity > 0)
                distortionTime += deltaTime * distortionSpeed;

            updateCallback?.Invoke(deltaTime * timeScale);
            window.Invalidate();

            if (targetFrameRate > 0)
            {
                float targetFrameTime = 1f / targetFrameRate;
                float elapsed = (float)(DateTime.Now - now).TotalSeconds;
                if (elapsed < targetFrameTime)
                {
                    int sleepMs = (int)((targetFrameTime - elapsed) * 1000);
                    if (sleepMs > 0) System.Threading.Thread.Sleep(sleepMs);
                }
            }
        }

        // ----- Effects -----
        public static void Shake(float intensity, float duration)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
        }

        public static void SetDistortion(float intensity, float speed)
        {
            distortionIntensity = intensity;
            distortionSpeed = speed;
            distortionTime = 0;
        }

        public static void DisableDistortion() => distortionIntensity = 0;

        public static void EnableSpotlight(float radius, PointF center)
        {
            useSpotlight = true;
            spotlightRadius = radius;
            spotlightCenter = center;
        }

        public static void DisableSpotlight() => useSpotlight = false;

        public static void SetAdditiveBlending(bool enable) => additiveBlending = enable;

        // ----- Mouse input -----
        public static Point GetMousePosition() => mousePosition;
        public static bool GetMouseButton(MouseButtons button) => (mouseButtons & button) != 0;
        public static bool GetMouseButtonDown(MouseButtons button) => (mouseButtons & button) != 0 && (mouseButtonsPrevious & button) == 0;
        public static bool GetMouseButtonUp(MouseButtons button) => (mouseButtons & button) == 0 && (mouseButtonsPrevious & button) != 0;

        // ----- Sprites (unchanged) -----
        public static int LoadSprite(string filePath)
        {
            try
            {
                Bitmap bmp = new Bitmap(filePath);
                int id = nextSpriteId++;
                sprites[id] = bmp;
                return id;
            }
            catch { return -1; }
        }

        public static int CreateSprite(int width, int height, Color color, bool circle = false)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(color))
                {
                    if (circle)
                        g.FillEllipse(brush, 0, 0, width, height);
                    else
                        g.FillRectangle(brush, 0, 0, width, height);
                }
            }
            int id = nextSpriteId++;
            sprites[id] = bmp;
            return id;
        }

        public static void UnloadSprite(int spriteId)
        {
            if (sprites.TryGetValue(spriteId, out Bitmap bmp))
            {
                bmp.Dispose();
                sprites.Remove(spriteId);
            }
        }

        public static void DrawSprite(int spriteId, int x, int y)
        {
            if (sprites.TryGetValue(spriteId, out Bitmap bmp))
                window.AddDrawAction(g => g.DrawImage(bmp, x, y));
        }

        public static void DrawSprite(int spriteId, int x, int y, int width, int height)
        {
            if (sprites.TryGetValue(spriteId, out Bitmap bmp))
                window.AddDrawAction(g => g.DrawImage(bmp, new Rectangle(x, y, width, height)));
        }

        public static void DrawSpriteEx(int spriteId, float x, float y, float originX, float originY, float scaleX, float scaleY, float angleDegrees)
        {
            if (!sprites.TryGetValue(spriteId, out Bitmap bmp)) return;
            window.AddDrawAction(g =>
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(angleDegrees);
                g.ScaleTransform(scaleX, scaleY);
                g.DrawImage(bmp, -originX, -originY);
                g.ResetTransform();
            });
        }

        public static void DrawPolygon(PointF[] points, Color color, bool filled = true, float thickness = 1)
        {
            if (points.Length < 3) return;
            window.AddDrawAction(g =>
            {
                if (additiveBlending)
                {
                    Color col = Color.FromArgb(color.A, color);
                    using (Brush brush = new SolidBrush(col))
                        g.FillPolygon(brush, points);
                }
                else
                {
                    if (filled)
                    {
                        using (Brush brush = new SolidBrush(color))
                            g.FillPolygon(brush, points);
                    }
                    else
                    {
                        using (Pen pen = new Pen(color, thickness))
                            g.DrawPolygon(pen, points);
                    }
                }
            });
        }

        public static void Clear(Color color) => window.AddDrawAction(g => g.Clear(color));

        public static void DrawText(string text, int x, int y, Color color, int fontSize = 12, bool center = false)
        {
            window.AddDrawAction(g =>
            {
                using (Font font = new Font("Arial", fontSize))
                using (Brush brush = new SolidBrush(color))
                {
                    if (center)
                    {
                        SizeF size = g.MeasureString(text, font);
                        g.DrawString(text, font, brush, x - size.Width / 2, y - size.Height / 2);
                    }
                    else
                    {
                        g.DrawString(text, font, brush, x, y);
                    }
                }
            });
        }

        public static void DrawTextCentered(string text, int x, int y, Color color, int fontSize = 12)
        {
            DrawText(text, x, y, color, fontSize, true);
        }

        public static void DrawRect(int x, int y, int width, int height, Color color, bool filled = true)
        {
            window.AddDrawAction(g =>
            {
                if (additiveBlending)
                {
                    Color col = Color.FromArgb(color.A, color);
                    using (Brush brush = new SolidBrush(col))
                        g.FillRectangle(brush, x, y, width, height);
                }
                else
                {
                    if (filled)
                    {
                        using (Brush brush = new SolidBrush(color))
                            g.FillRectangle(brush, x, y, width, height);
                    }
                    else
                    {
                        using (Pen pen = new Pen(color))
                            g.DrawRectangle(pen, x, y, width, height);
                    }
                }
            });
        }

        public static void DrawCircle(int x, int y, int radius, Color color, bool filled = true)
        {
            window.AddDrawAction(g =>
            {
                if (additiveBlending)
                {
                    Color col = Color.FromArgb(color.A, color);
                    using (Brush brush = new SolidBrush(col))
                        g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
                }
                else
                {
                    if (filled)
                    {
                        using (Brush brush = new SolidBrush(color))
                            g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
                    }
                    else
                    {
                        using (Pen pen = new Pen(color))
                            g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
                    }
                }
            });
        }

        // ----- Audio with volume control -----
        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        private class SoundPlayerWrapper
        {
            public SoundPlayer Player { get; set; }
            public string FilePath { get; set; }
            public bool IsLooping { get; set; }
        }

        public static void PlaySound(string filePath, bool loop = false)
        {
            try
            {
                if (!soundPlayers.ContainsKey(filePath))
                {
                    var player = new SoundPlayer(filePath);
                    soundPlayers[filePath] = new SoundPlayerWrapper { Player = player, FilePath = filePath };
                }
                var wrapper = soundPlayers[filePath];
                wrapper.IsLooping = loop;

                // Set volume before playing
                SetSoundVolumeInternal(filePath);

                if (loop)
                    wrapper.Player.PlayLooping();
                else
                    wrapper.Player.Play();
            }
            catch { }
        }

        private static void SetSoundVolumeInternal(string filePath)
        {
            // For SoundPlayer, volume control is tricky; we use waveOutSetVolume as a workaround
            float vol = GetEffectiveVolume(filePath);
            uint volume = (uint)(vol * 0xFFFF);
            uint bothChannels = (volume << 16) | volume;
            waveOutSetVolume(IntPtr.Zero, bothChannels);
        }

        public static void StopSound(string filePath)
        {
            if (soundPlayers.TryGetValue(filePath, out var wrapper))
            {
                wrapper.Player.Stop();
            }
        }

        public static void StopAllSounds()
        {
            foreach (var wrapper in soundPlayers.Values)
                wrapper.Player.Stop();
        }

        // ----- Window control -----
        public static void SetWindowSize(int width, int height)
        {
            if (window != null && !Fullscreen)
                window.ClientSize = new Size(width, height);
        }

        public static void ToggleFullscreen()
        {
            if (window == null) return;
            Fullscreen = !Fullscreen;
            if (Fullscreen)
            {
                window.FormBorderStyle = FormBorderStyle.None;
                window.WindowState = FormWindowState.Maximized;
            }
            else
            {
                window.FormBorderStyle = FormBorderStyle.Sizable;
                window.WindowState = FormWindowState.Normal;
                window.ClientSize = new Size(800, 600);
            }
        }

        public static void SetIcon(string iconPath)
        {
            try { window.Icon = new Icon(iconPath); } catch { }
        }

        // ----- Utilities -----
        public static void SetTargetFrameRate(float fps) => targetFrameRate = fps;
        public static float RandomFloat() => (float)random.NextDouble();
        public static float RandomFloat(float min, float max) => (float)(random.NextDouble() * (max - min) + min);
        public static int RandomInt(int min, int max) => random.Next(min, max);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }

    internal class GameWindow : Form
    {
        private System.Collections.Concurrent.ConcurrentQueue<Action<Graphics>> drawActions = new System.Collections.Concurrent.ConcurrentQueue<Action<Graphics>>();

        public GameWindow()
        {
            DoubleBuffered = true;
            Paint += GameWindow_Paint;
            FormClosing += (s, e) => Application.Exit();
            KeyPreview = true;
        }

        public void AddDrawAction(Action<Graphics> action)
        {
            drawActions.Enqueue(action);
        }

        private void GameWindow_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            if (Engine.shakeDuration > 0)
            {
                float offsetX = (float)(Engine.shakeRandom.NextDouble() * 2 - 1) * Engine.shakeIntensity;
                float offsetY = (float)(Engine.shakeRandom.NextDouble() * 2 - 1) * Engine.shakeIntensity;
                g.TranslateTransform(offsetX, offsetY);
            }

            if (Engine.distortionIntensity > 0)
            {
                float t = Engine.distortionTime;
                float intensity = Engine.distortionIntensity;
                float warpX = (float)(Math.Sin(t * 2.0) * 15 + Math.Sin(t * 1.3) * 10) * intensity / 5f;
                float warpY = (float)(Math.Cos(t * 1.7) * 15 + Math.Sin(t * 2.5) * 10) * intensity / 5f;
                float rotateWarp = (float)Math.Sin(t * 1.2) * 3 * intensity / 5f;
                float scale = 1f + (float)Math.Sin(t * 3.0) * 0.05f * intensity / 5f;
                g.TranslateTransform(warpX, warpY);
                g.RotateTransform(rotateWarp);
                g.ScaleTransform(scale, scale);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            Engine.drawCallback?.Invoke();

            while (drawActions.TryDequeue(out var action))
                action(g);

            if (Engine.useSpotlight)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddRectangle(new Rectangle(0, 0, Engine.WindowWidth, Engine.WindowHeight));
                    path.AddEllipse(
                        Engine.spotlightCenter.X - Engine.spotlightRadius,
                        Engine.spotlightCenter.Y - Engine.spotlightRadius,
                        Engine.spotlightRadius * 2,
                        Engine.spotlightRadius * 2);
                    using (Region region = new Region(path))
                    {
                        region.Complement(path);
                        g.Clip = region;
                        using (Brush brush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                            g.FillRectangle(brush, 0, 0, Engine.WindowWidth, Engine.WindowHeight);
                        g.ResetClip();
                    }
                }
            }
        }
    }
}