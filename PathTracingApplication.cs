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
            camera = new Camera(Vector3.UnitZ * 25.0f, Size.X / (float)Size.Y);
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
        }        private Shader CreateShader(string shaderPath)
        {
            try
            {
                Console.WriteLine($"Loading shader from: {shaderPath}");
                Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
                
                string fullPath = Path.Combine(Environment.CurrentDirectory, shaderPath);
                Console.WriteLine($"Full path: {fullPath}");
                
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"SHADER ERROR: File not found: {fullPath}");
                    
                    // Try alternative locations
                    string altPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, shaderPath);
                    Console.WriteLine($"Trying alternative path: {altPath}");
                    
                    if (File.Exists(altPath))
                    {
                        fullPath = altPath;
                        Console.WriteLine($"Found shader at alternative path: {fullPath}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to locate shader file. Check paths.");
                    }
                }
                
                string originalShaderSource = File.ReadAllText(fullPath);
                
                // For path_tracing shader, use our simplified version
                if (Path.GetFileName(fullPath).Contains("path_tracing"))
                {
                    string fragmentShaderSource = CreatePathTracingShaderSource();
                    
                    // Standard vertex shader for screen-space rendering
                    string vertexShaderSource = @"
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
                    
                    return new Shader(vertexShaderSource, fragmentShaderSource);
                }
                else if (Path.GetFileName(fullPath).Contains("post_process"))
                {
                    string fragmentShaderSource = @"
                        #version 450 core
                        
                        in vec2 TexCoord;
                        out vec4 OutColor;
                        
                        uniform sampler2D uImage;
                        uniform int uImageSamples;
                        
                        void main()
                        {
                            vec3 color = texture(uImage, TexCoord).rgb;
                            color /= float(uImageSamples);
                            color = color / (color + vec3(1.0));
                            color = pow(color, vec3(1.0 / 2.2));
                            OutColor = vec4(color, 1.0);
                        }
                    ";
                    
                    // Standard vertex shader for screen-space rendering
                    string vertexShaderSource = @"
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
                    
                    return new Shader(vertexShaderSource, fragmentShaderSource);
                }
                else
                {
                    // Generic handling for other shaders
                    string fragmentShaderSource = "#version 450 core\n" + originalShaderSource.Substring(originalShaderSource.IndexOf('\n') + 1);
                    
                    // Standard vertex shader for screen-space rendering
                    string vertexShaderSource = @"
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
                    return new Shader(vertexShaderSource, fragmentShaderSource);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating shader: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }
        
        private string CreatePathTracingShaderSource()
        {
            // A simplified path tracing shader that works with OpenGL 4.x and OpenTK
            return @"
                #version 450 core
                
                in vec2 TexCoord;
                out vec4 OutColor;
                
                struct Material {
                    vec3 emmitance;
                    vec3 reflectance;
                    float roughness;
                    float opacity;
                };
                
                struct Sphere {
                    Material material;
                    vec3 position;
                    float radius;
                };
                
                struct Box {
                    Material material;
                    vec3 halfSize;
                    mat3 rotation;
                    vec3 position;
                };
                
                uniform vec2 uViewportSize;
                uniform vec3 uPosition;
                uniform vec3 uDirection;
                uniform vec3 uUp;
                uniform float uFOV;
                uniform float uTime;
                uniform int uSamples;
                
                #define PI 3.1415926535
                #define HALF_PI (PI / 2.0)
                #define FAR_DISTANCE 1000000.0
                #define MAX_DEPTH 8
                #define SPHERE_COUNT 3
                #define BOX_COUNT 8
                #define N_IN 0.99
                #define N_OUT 1.0
                
                Sphere spheres[SPHERE_COUNT];
                Box boxes[BOX_COUNT];
                
                float rand(vec2 co) {
                    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
                }
                
                float randomValue(float seed) {
                    return fract(sin(seed) * 43758.5453);
                }
                
                vec3 randomDirection(float seed) {
                    float a = randomValue(seed) * 2.0 * PI;
                    float z = randomValue(seed + 0.1) * 2.0 - 1.0;
                    float r = sqrt(1.0 - z * z);
                    return vec3(r * cos(a), r * sin(a), z);
                }
                
                vec3 randomHemisphereDirection(vec3 normal, float seed) {
                    vec3 dir = randomDirection(seed);
                    return dot(dir, normal) < 0.0 ? -dir : dir;
                }
                
                // Initialize scene objects
                void initScene() {
                    // Initialize spheres
                    spheres[0].position = vec3(2.5, 1.5, -1.5);
                    spheres[1].position = vec3(-2.5, 2.5, -1.0);
                    spheres[2].position = vec3(0.5, -4.0, 3.0);
                    
                    spheres[0].radius = 1.5;
                    spheres[1].radius = 1.0;
                    spheres[2].radius = 1.0;
                    
                    spheres[0].material.roughness = 1.0;
                    spheres[1].material.roughness = 0.8;
                    spheres[2].material.roughness = 1.0;
                    
                    spheres[0].material.opacity = 0.0;
                    spheres[1].material.opacity = 0.0;
                    spheres[2].material.opacity = 0.8;
                    
                    spheres[0].material.reflectance = vec3(1.0, 0.0, 0.0);
                    spheres[1].material.reflectance = vec3(1.0, 0.4, 0.0);
                    spheres[2].material.reflectance = vec3(1.0, 1.0, 1.0);
                    
                    spheres[0].material.emmitance = vec3(0.0);
                    spheres[1].material.emmitance = vec3(0.0);
                    spheres[2].material.emmitance = vec3(0.0);
                
                    // Initialize boxes (walls and objects)
                    // Top wall
                    boxes[0].material.roughness = 0.0;
                    boxes[0].material.opacity = 0.0;
                    boxes[0].material.emmitance = vec3(0.0);
                    boxes[0].material.reflectance = vec3(1.0, 1.0, 1.0);
                    boxes[0].halfSize = vec3(5.0, 0.5, 5.0);
                    boxes[0].position = vec3(0.0, 5.5, 0.0);
                    boxes[0].rotation = mat3(
                        1.0, 0.0, 0.0,
                        0.0, 1.0, 0.0,
                        0.0, 0.0, 1.0
                    );
                    
                    // Bottom wall
                    boxes[1].material.roughness = 0.3;
                    boxes[1].material.opacity = 0.0;
                    boxes[1].material.emmitance = vec3(0.0);
                    boxes[1].material.reflectance = vec3(1.0, 1.0, 1.0);
                    boxes[1].halfSize = vec3(5.0, 0.5, 5.0);
                    boxes[1].position = vec3(0.0, -5.5, 0.0);
                    boxes[1].rotation = mat3(
                        1.0, 0.0, 0.0,
                        0.0, 1.0, 0.0,
                        0.0, 0.0, 1.0
                    );
                    
                    // Right wall - green
                    boxes[2].material.roughness = 0.0;
                    boxes[2].material.opacity = 0.0;
                    boxes[2].material.emmitance = vec3(0.0);
                    boxes[2].material.reflectance = vec3(0.0, 1.0, 0.0);
                    boxes[2].halfSize = vec3(5.0, 0.5, 5.0);
                    boxes[2].position = vec3(5.5, 0.0, 0.0);
                    boxes[2].rotation = mat3(
                        0.0, 1.0, 0.0,
                        -1.0, 0.0, 0.0,
                        0.0, 0.0, 1.0
                    );
                    
                    // Left wall - red
                    boxes[3].material.roughness = 0.0;
                    boxes[3].material.opacity = 0.0;
                    boxes[3].material.emmitance = vec3(0.0);
                    boxes[3].material.reflectance = vec3(1.0, 0.0, 0.0);
                    boxes[3].halfSize = vec3(5.0, 0.5, 5.0);
                    boxes[3].position = vec3(-5.5, 0.0, 0.0);
                    boxes[3].rotation = mat3(
                        0.0, 1.0, 0.0,
                        -1.0, 0.0, 0.0,
                        0.0, 0.0, 1.0
                    );
                    
                    // Back wall
                    boxes[4].material.roughness = 0.0;
                    boxes[4].material.opacity = 0.0;
                    boxes[4].material.emmitance = vec3(0.0);
                    boxes[4].material.reflectance = vec3(1.0, 1.0, 1.0);
                    boxes[4].halfSize = vec3(5.0, 0.5, 5.0);
                    boxes[4].position = vec3(0.0, 0.0, -5.5);
                    boxes[4].rotation = mat3(
                        1.0, 0.0, 0.0,
                        0.0, 0.0, 1.0,
                        0.0, 1.0, 0.0
                    );
                    
                    // Light source
                    boxes[5].material.roughness = 0.0;
                    boxes[5].material.opacity = 0.0;
                    boxes[5].material.emmitance = vec3(6.0);
                    boxes[5].material.reflectance = vec3(1.0);
                    boxes[5].halfSize = vec3(2.5, 0.2, 2.5);
                    boxes[5].position = vec3(0.0, 4.8, 0.0);
                    boxes[5].rotation = mat3(
                        1.0, 0.0, 0.0,
                        0.0, 1.0, 0.0,
                        0.0, 0.0, 1.0
                    );
                    
                    // Box 1
                    boxes[6].material.roughness = 0.0;
                    boxes[6].material.opacity = 0.0;
                    boxes[6].material.emmitance = vec3(0.0);
                    boxes[6].material.reflectance = vec3(1.0);
                    boxes[6].halfSize = vec3(1.5, 3.0, 1.5);
                    boxes[6].position = vec3(-2.0, -2.0, -0.0);
                    boxes[6].rotation = mat3(
                        0.7, 0.0, 0.7,
                        0.0, 1.0, 0.0,
                        -0.7, 0.0, 0.7
                    );
                    
                    // Box 2
                    boxes[7].material.roughness = 0.0;
                    boxes[7].material.opacity = 0.0;
                    boxes[7].material.emmitance = vec3(0.0);
                    boxes[7].material.reflectance = vec3(1.0);
                    boxes[7].halfSize = vec3(1.0, 1.5, 1.0);
                    boxes[7].position = vec3(2.5, -3.5, -0.0);
                    boxes[7].rotation = mat3(
                        0.7, 0.0, 0.7,
                        0.0, 1.0, 0.0,
                        -0.7, 0.0, 0.7
                    );
                }
                
                // Ray-sphere intersection test
                float intersectSphere(vec3 origin, vec3 direction, Sphere sphere) {
                    vec3 oc = origin - sphere.position;
                    float b = dot(oc, direction);
                    float c = dot(oc, oc) - sphere.radius * sphere.radius;
                    float h = b * b - c;
                    
                    if (h < 0.0) return -1.0;
                    h = sqrt(h);
                    float t = -b - h;
                    
                    return (t > 0.0) ? t : -1.0;
                }
                
                // Ray-box intersection test
                float intersectBox(vec3 origin, vec3 direction, Box box) {
                    vec3 ro = (origin - box.position) * box.rotation;
                    vec3 rd = direction * box.rotation;
                    
                    vec3 m = 1.0 / rd;
                    vec3 s = vec3(
                        (rd.x < 0.0) ? 1.0 : -1.0,
                        (rd.y < 0.0) ? 1.0 : -1.0,
                        (rd.z < 0.0) ? 1.0 : -1.0
                    );
                    
                    vec3 t1 = m * (-ro + s * box.halfSize);
                    vec3 t2 = m * (-ro - s * box.halfSize);
                    
                    float tN = max(max(t1.x, t1.y), t1.z);
                    float tF = min(min(t2.x, t2.y), t2.z);
                    
                    if (tN > tF || tF < 0.0) return -1.0;
                    
                    return tN > 0.0 ? tN : tF;
                }
                
                vec3 getNormalSphere(vec3 hitPoint, Sphere sphere) {
                    return normalize(hitPoint - sphere.position);
                }
                
                vec3 getNormalBox(vec3 hitPoint, Box box) {
                    vec3 localPoint = (hitPoint - box.position) * box.rotation;
                    vec3 absPoint = abs(localPoint / box.halfSize);
                    float maxComp = max(max(absPoint.x, absPoint.y), absPoint.z);
                    
                    if (absPoint.x == maxComp) {
                        return box.rotation * vec3(sign(localPoint.x), 0, 0);
                    } else if (absPoint.y == maxComp) {
                        return box.rotation * vec3(0, sign(localPoint.y), 0);
                    } else {
                        return box.rotation * vec3(0, 0, sign(localPoint.z));
                    }
                }
                
                // Trace a ray through the scene
                vec3 traceRay(vec3 origin, vec3 direction, float seed) {
                    vec3 accumulatedLight = vec3(0.0);
                    vec3 rayColor = vec3(1.0);
                    
                    for (int depth = 0; depth < MAX_DEPTH; depth++) {
                        float closestT = FAR_DISTANCE;
                        int hitType = -1;  // -1 = miss, 0 = sphere, 1 = box
                        int hitIndex = -1;
                        
                        // Check all spheres
                        for (int i = 0; i < SPHERE_COUNT; i++) {
                            float t = intersectSphere(origin, direction, spheres[i]);
                            if (t > 0.0 && t < closestT) {
                                closestT = t;
                                hitType = 0;
                                hitIndex = i;
                            }
                        }
                        
                        // Check all boxes
                        for (int i = 0; i < BOX_COUNT; i++) {
                            float t = intersectBox(origin, direction, boxes[i]);
                            if (t > 0.0 && t < closestT) {
                                closestT = t;
                                hitType = 1;
                                hitIndex = i;
                            }
                        }
                        
                        // If we didn't hit anything, break
                        if (hitType == -1) {
                            break;
                        }
                        
                        // Calculate the hit point
                        vec3 hitPoint = origin + direction * closestT;
                        vec3 normal;
                        Material material;
                        
                        if (hitType == 0) {  // Hit a sphere
                            normal = getNormalSphere(hitPoint, spheres[hitIndex]);
                            material = spheres[hitIndex].material;
                        } else {  // Hit a box
                            normal = getNormalBox(hitPoint, boxes[hitIndex]);
                            material = boxes[hitIndex].material;
                        }
                        
                        // Add emission
                        accumulatedLight += rayColor * material.emmitance;
                        
                        // Update ray color based on material reflectance
                        rayColor *= material.reflectance;
                        
                        // Bounce the ray
                        vec3 newDir;
                        if (material.roughness > 0.0) {
                            // Generate a random direction in the hemisphere and blend with reflection
                            vec3 randomDir = randomHemisphereDirection(normal, seed + float(depth) * 2.34);
                            vec3 reflectDir = reflect(direction, normal);
                            newDir = normalize(mix(reflectDir, randomDir, material.roughness));
                        } else {
                            // Perfect reflection
                            newDir = reflect(direction, normal);
                        }
                        
                        // Update the ray for the next iteration
                        origin = hitPoint + normal * 0.001; // Offset to avoid self-intersection
                        direction = newDir;
                        
                        // Update random seed
                        seed = seed * 1.61803398875 + 0.1;
                    }
                    
                    return accumulatedLight;
                }
                
                // Generate a camera ray
                vec3 getCameraRay(vec2 uv) {
                    // Calculate right and up vectors from camera
                    vec3 viewDir = normalize(uDirection);
                    vec3 rightDir = normalize(cross(viewDir, uUp));
                    vec3 upDir = normalize(cross(rightDir, viewDir));
                    
                    // Calculate ray direction using lens model
                    float focalLength = 1.0;
                    float aspectRatio = uViewportSize.x / uViewportSize.y;
                    float scale = tan(uFOV * 0.5);
                    
                    vec2 xy = uv * 2.0 - 1.0;  // Transform to -1 to +1 range
                    xy.x *= aspectRatio;
                    
                    vec3 rayDir = normalize(viewDir * focalLength + rightDir * (xy.x * scale) + upDir * (xy.y * scale));
                    
                    return rayDir;
                }
                
                void main() {
                    // Initialize the scene
                    initScene();
                    
                    vec3 finalColor = vec3(0.0);
                    float seed = uTime + TexCoord.x * 1000.0 + TexCoord.y * 100.0;
                    
                    // Sample multiple rays for anti-aliasing and soft-effects
                    for (int i = 0; i < uSamples; i++) {
                        // Jitter the pixel position slightly for anti-aliasing
                        vec2 jitteredCoord = TexCoord + vec2(
                            rand(vec2(seed, TexCoord.x)) / uViewportSize.x,
                            rand(vec2(TexCoord.y, seed)) / uViewportSize.y
                        ) * 0.5;
                        
                        // Get ray direction from camera
                        vec3 rayDir = getCameraRay(jitteredCoord);
                        
                        // Trace the ray and accumulate color
                        finalColor += traceRay(uPosition, rayDir, seed + float(i));
                        
                        // Update seed
                        seed = seed * 1.61803398875 + 0.1;
                    }
                    
                    // Average the samples
                    finalColor /= float(uSamples);
                    
                    // Output final color
                    OutColor = vec4(finalColor, 1.0);
                }
            ";
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
