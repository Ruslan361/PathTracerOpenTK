using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PathTracerOpenTK
{    public class PathTracingApplication : GameWindow
    {
        private Shader? rayTracingShader;
        private Shader? postProcessShader;
        private Vector3 oldCameraPosition = Vector3.Zero;
        private Vector3 oldCameraDirection = Vector3.Zero;
        private float oldFOV = 0.0f;
        private int accumulationFrames = 0;
        private int accumulationTexture;
        private int frameBuffer;
        private int renderTexture;
        private int vao, vbo;

        private Camera? camera;
        private bool firstMove = true;
        private Vector2 lastMousePos;
        private const float CameraMoveSpeed = 5.0f;
        private const float CameraRotateSpeed = 5.0f;

        private Stopwatch timer = new Stopwatch();

        public PathTracingApplication(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            timer.Start();
            oldCameraPosition = Vector3.Zero;
            oldCameraDirection = Vector3.Zero;
            oldFOV = 0.0f;
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);

            // Initialize camera
            camera = new Camera(new Vector3(0.0f, 0.0f, 0.0f), Size.X / (float)Size.Y);
            camera.Direction = -Vector3.UnitZ;
            camera.MoveSpeed = 5.0f;
            camera.RotateSpeed = 5.0f;
            camera.Fov = 60.0f;            // Create shaders
            rayTracingShader = CreateShader("Resources/path_tracing.glsl");
            postProcessShader = CreateShader("Resources/post_process.glsl");

            // Create full-screen quad
            CreateFullScreenQuad();

            // Create accumulation texture
            CreateAccumulationTexture(Size.X, Size.Y);
            
            // Create frame buffer for rendering
            CreateFrameBuffer(Size.X, Size.Y);

            CursorState = CursorState.Grabbed;
        }

        private void CreateFullScreenQuad()
        {
            float[] vertices = {
                -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
                -1.0f,  1.0f, 0.0f, 0.0f, 1.0f
            };

            uint[] indices = {
                0, 1, 2,
                2, 3, 0
            };

            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            
            int ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            
            // Texture coordinates attribute
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }
        private Shader CreateShader(string shaderPath)
        {
            Console.WriteLine($"Loading shader from: {shaderPath}");
            string fullPath = Path.Combine(Environment.CurrentDirectory, shaderPath);
            if (!File.Exists(fullPath))
            {
                string altPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, shaderPath);
                if (File.Exists(altPath))
                    fullPath = altPath;
                else
                    Console.WriteLine($"SHADER ERROR: File not found: {fullPath}");
            }
            string fragmentSource = File.ReadAllText(fullPath);
            string vertexSource = @"
                #version 450 core
                layout (location = 0) in vec3 aPos;
                layout (location = 1) in vec2 aTexCoord;

                out vec2 TexCoord;

                void main()
                {
                    gl_Position = vec4(aPos, 1.0);
                    TexCoord = aTexCoord;
                }
            ";
            return new Shader(vertexSource, fragmentSource);
        }// These methods are now handled by the Shader class

        private void CreateFrameBuffer(int width, int height)
        {
            frameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
            
            // Create color attachment texture
            renderTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, renderTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, width, height, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, renderTexture, 0);
            
            // Check if framebuffer is complete
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine("ERROR::FRAMEBUFFER:: Framebuffer is not complete!");
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void CreateAccumulationTexture(int width, int height)
        {
            accumulationTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, accumulationTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, width, height, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            if (!IsFocused || camera == null)
                return;

            var input = KeyboardState;

            if (input.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            // Camera movement
            Vector3 moveDirection = Vector3.Zero;

            if (input.IsKeyDown(Keys.W))
                moveDirection += camera.Direction;
            if (input.IsKeyDown(Keys.S))
                moveDirection -= camera.Direction;
            if (input.IsKeyDown(Keys.A))
                moveDirection -= Vector3.Normalize(Vector3.Cross(camera.Direction, camera.Up));
            if (input.IsKeyDown(Keys.D))
                moveDirection += Vector3.Normalize(Vector3.Cross(camera.Direction, camera.Up));
            if (input.IsKeyDown(Keys.Space))
                moveDirection += camera.Up;
            if (input.IsKeyDown(Keys.LeftShift))
                moveDirection -= camera.Up;

            if (moveDirection != Vector3.Zero)
            {
                moveDirection = Vector3.Normalize(moveDirection);
                camera.Position += moveDirection * CameraMoveSpeed * (float)e.Time;
            }

            // Camera rotation
            var mouse = MouseState;

            if (firstMove)
            {
                lastMousePos = new Vector2(mouse.X, mouse.Y);
                firstMove = false;
            }

            var deltaX = mouse.X - lastMousePos.X;
            var deltaY = mouse.Y - lastMousePos.Y;
            lastMousePos = new Vector2(mouse.X, mouse.Y);

            camera.Yaw += deltaX * CameraRotateSpeed * 0.01f;
            camera.Pitch -= deltaY * CameraRotateSpeed * 0.01f;
            
            // Clamp pitch to avoid gimbal lock
            if (camera.Pitch > 89.0f)
                camera.Pitch = 89.0f;
            if (camera.Pitch < -89.0f)
                camera.Pitch = -89.0f;

            // Update camera direction vectors
            camera.UpdateDirectionVectors();
        }        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
            // Make sure we have the camera and shaders initialized
            if (camera == null || rayTracingShader == null || postProcessShader == null)
            {
                return;
            }
            
            bool accumulateImage = oldCameraPosition == camera.Position &&
                oldCameraDirection == camera.Direction &&
                oldFOV == camera.Fov;

            int raySamples = accumulateImage ? 16 : 4;
            
            // Render to accumulation texture using ray tracing shader
            rayTracingShader.Use();
            rayTracingShader.SetInt("uSamples", raySamples);
            rayTracingShader.SetFloat("uTime", (float)timer.Elapsed.TotalSeconds);
            rayTracingShader.SetVector2("uViewportSize", new Vector2(Size.X, Size.Y));
            rayTracingShader.SetVector3("uPosition", camera.Position);
            rayTracingShader.SetVector3("uDirection", camera.Direction);
            rayTracingShader.SetVector3("uUp", camera.Up);
            rayTracingShader.SetFloat("uFOV", MathHelper.DegreesToRadians(camera.Fov));

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, accumulationTexture, 0);

            if (accumulateImage)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                // Don't clear the buffer to accumulate samples
                accumulationFrames++;
            }
            else
            {
                GL.Disable(EnableCap.Blend);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                accumulationFrames = 1;
            }

            RenderQuad();
            
            // Generate mipmaps
            GL.BindTexture(TextureTarget.Texture2D, accumulationTexture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);            // Post-process to final render texture
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, renderTexture, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            postProcessShader.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, accumulationTexture);
            postProcessShader.SetInt("uImage", 0);
            postProcessShader.SetInt("uImageSamples", accumulationFrames);
            
            RenderQuad();
            
            // Generate mipmaps
            GL.BindTexture(TextureTarget.Texture2D, renderTexture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);            // Render the final texture to the screen
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            postProcessShader.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderTexture);
            postProcessShader.SetInt("uImage", 0);
            postProcessShader.SetInt("uImageSamples", 1);
            
            RenderQuad();

            // Store current camera state for next frame
            oldCameraPosition = camera.Position;
            oldCameraDirection = camera.Direction;
            oldFOV = camera.Fov;

            SwapBuffers();
        }

        private void RenderQuad()
        {
            GL.BindVertexArray(vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, e.Width, e.Height);
            if (camera != null)
                camera.AspectRatio = e.Width / (float)e.Height;

            // Recreate frame buffer and textures with the new size
            GL.DeleteFramebuffer(frameBuffer);
            GL.DeleteTexture(renderTexture);
            GL.DeleteTexture(accumulationTexture);
            
            CreateFrameBuffer(e.Width, e.Height);
            CreateAccumulationTexture(e.Width, e.Height);
            
            accumulationFrames = 0;
        }        protected override void OnUnload()
        {
            base.OnUnload();

            // Clean up shaders
            if (rayTracingShader != null)
            {
                rayTracingShader.Dispose();
            }
            
            if (postProcessShader != null)
            {
                postProcessShader.Dispose();
            }

            // Clean up OpenGL resources
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteTexture(renderTexture);
            GL.DeleteTexture(accumulationTexture);
            GL.DeleteFramebuffer(frameBuffer);
        }
    }
}
