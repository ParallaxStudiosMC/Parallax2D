# Parallax2D

A lightweight yet powerful 2D game engine for .NET 8.0, designed for developers who want total control and power without the bloat of traditional frameworks. Built with C# and the Windows API (Win32) for direct hardware input and GDI+ for flexible rendering, Parallax2D gives you the tools to create anything from simple prototypes to fun games with advanced visual effects.

* Note! As of version v1.1.1, Parallax2D utilizes Windows' default GDI+ Software Rendering, which provides an easier way of downloading the engine since all you need is the DLL and Visual Studio 2022 or 2026, although GDI+ rendering tends to slow down performance.

---

## Features

- **Hardware‑accelerated input** – Uses Win32 `GetAsyncKeyState` for instant, responsive keyboard input (no event lag, works even when window is unfocused).
- **Sprite loading** – Load PNG, JPEG, BMP, or GIF images; draw them scaled, rotated, or as sprite sheets.
- **Primitive drawing** – Draw rectangles, circles, polygons, lines, and text with optional additive blending.
- **Advanced effects** – Built‑in screen shake, distortion (nausea) effects, and spotlight (blind) mode.
- **Particle system** – Create explosions, trails, and dynamic effects with additive blending support.
- **Object pooling** – Built‑in support for reusing bullets and particles to reduce garbage collection.
- **No external dependencies** – Just pure C# and the .NET base class libraries.

---

## Getting Started

### 1. Create a new Windows Forms project

- Open Visual Studio and create a new project.
- Choose **Windows Forms App (C#)** (make sure it targets **.NET 8.0**).
- Give it a name (e.g., `SampleGame`).

### 2. Add the Parallax2D engine

Step 1:
- Download the compiled `p2dgfx.dll` from the [releases page](github.com/ParallaxStudiosMC/Parallax2D/releases/latest) and add a reference to your project.

To add a reference:
- Right‑click **Dependencies** → **Add Project Reference** → **Browse** → select the DLL.

### 3. Your first game loop

Replace the contents of `Form1.cs` (or `Program.cs`) with the following minimal example:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using p2dgfx;

namespace SampleGame
{
    static class Program
    {
        static float x = 400, y = 300;
        static float speed = 300f;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Initialize the engine with window size, title, update and draw callbacks
            Engine.Init(800, 600, "My First Parallax2D Game", Update, Draw);
        }

        static void Update(float dt)
        {
            // Move with arrow keys
            if (Input.GetKey(Keys.Left) || Input.GetKey(Keys.A)) x -= speed * dt;
            if (Input.GetKey(Keys.Right) || Input.GetKey(Keys.D)) x += speed * dt;
            if (Input.GetKey(Keys.Up) || Input.GetKey(Keys.W)) y -= speed * dt;
            if (Input.GetKey(Keys.Down) || Input.GetKey(Keys.S)) y += speed * dt;

            // Keep inside window
            x = Math.Clamp(x, 0, Engine.WindowWidth - 30);
            y = Math.Clamp(y, 0, Engine.WindowHeight - 30);
        }

        static void Draw()
        {
            Engine.Clear(Color.Black);
            Engine.DrawRect((int)x, (int)y, 30, 30, Color.LimeGreen, true);
            Engine.DrawText($"FPS: {1f/Engine.RawDeltaTime:F0}", 10, 10, Color.White, 20);
        }
    }
}
