using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PathTracerOpenTK
{
    class Program
    {        static void Main(string[] args)
        {
            var nativeWindowSettings = new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280, 720),
                Title = "PathTracer OpenTK",
                Flags = ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 6)
            };

            using (var game = new PathTracingApplication(GameWindowSettings.Default, nativeWindowSettings))
            {
                System.Environment.SetEnvironmentVariable("CUDA_VISIBLE_DEVICES", "0");
                game.Run();

            }
        }
    }
}
