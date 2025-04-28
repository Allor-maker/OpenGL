using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;

namespace openGL
{
    internal class Game : GameWindow
    {
        private List<Fish> fishes = new();

        private Vector3 aquariumMinBounds;
        private Vector3 aquariumMaxBounds;

        private Shader fishShader = null!;
        private int fishTextureId = -1;
        private string fishModelPath = "../../../Models/finalfish.obj";
        private string fishTexturePath = "../../../Textures/fish.png";

        private int cubeVao = -1;
        private int cubeVbo = -1;
        private int cubeEbo = -1;
        private readonly int cubeIndexCount = 24;

        private Camera camera = null!;
        private bool isCameraAttached = true;
        private readonly Vector3 fixedCameraPosition = new Vector3(0, 0, 3);
        private readonly Vector3 fixedCameraTarget = Vector3.Zero;

        private Vector3 lightPos = new Vector3(0.0f, 2.0f, 2.0f);
        private Vector3 lightColor = new Vector3(1.0f, 1.0f, 1.0f);

        private Vector3 scarePointPosition = Vector3.Zero;

        public Game(int width, int height) : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            Size = new Vector2i(width, height),
            API = ContextAPI.OpenGL,
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            APIVersion = new Version(3, 3)
        })
        { }

        private int LoadTextureFromFile(string path)
        {
            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);
            try
            {
                string absolutePath = Path.GetFullPath(path);
                Console.WriteLine($"Attempting to load texture: {absolutePath}");
                if (!File.Exists(absolutePath))
                {
                    Console.WriteLine($"Texture file not found at: {absolutePath}");
                    GL.DeleteTexture(handle);
                    return -1;
                }
                StbImage.stbi_set_flip_vertically_on_load(1);
                using (Stream stream = File.OpenRead(absolutePath))
                {
                    ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
                }
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading texture {path}: {e.Message}");
                GL.DeleteTexture(handle);
                return -1;
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return handle;
        }

        private void SetupAquariumBoundsGraphics()
        {
            float minX = aquariumMinBounds.X;
            float minY = aquariumMinBounds.Y;
            float minZ = aquariumMinBounds.Z;
            float maxX = aquariumMaxBounds.X;
            float maxY = aquariumMaxBounds.Y;
            float maxZ = aquariumMaxBounds.Z;

            float[] vertices =
            {
                minX, minY, minZ,
                maxX, minY, minZ,
                maxX, maxY, minZ,
                minX, maxY, minZ,
                minX, minY, maxZ,
                maxX, minY, maxZ,
                maxX, maxY, maxZ,
                minX, maxY, maxZ
            };

            uint[] indices =
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };

            cubeVao = GL.GenVertexArray();
            cubeVbo = GL.GenBuffer();
            cubeEbo = GL.GenBuffer();

            GL.BindVertexArray(cubeVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, cubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, cubeEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }
        private Vector3 GetWorldRayDirection(Vector2 screenPos)
        {
            float x = (2.0f * screenPos.X) / Size.X - 1.0f;
            float y = 1.0f - (2.0f * screenPos.Y) / Size.Y;
            Vector4 ray_clip = new Vector4(x, y, -1.0f, 1.0f);


            Matrix4 projInv = camera.GetProjectionMatrix().Inverted();
            Vector4 ray_eye = ray_clip * projInv;
            ray_eye.Z = -1.0f;
            ray_eye.W = 0.0f;

            Matrix4 viewInv = camera.GetViewMatrix().Inverted();
            Vector4 ray_wor_4 = ray_eye * viewInv;
            return ray_wor_4.Xyz.Normalized();
        }
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.05f, 0.1f, 0.2f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            fishShader = new Shader();
            fishShader.LoadShader("shader.vert", "shader.frag");

            fishTextureId = LoadTextureFromFile(fishTexturePath);

            camera = new Camera(new Vector3(0, 0, 3), (float)Size.X / Size.Y);
            CursorState = CursorState.Grabbed;

            float aquariumSize = 2.0f;
            aquariumMinBounds = new Vector3(-aquariumSize, -aquariumSize, -aquariumSize);
            aquariumMaxBounds = new Vector3(aquariumSize, aquariumSize, aquariumSize);
            SetupAquariumBoundsGraphics();

            string absoluteModelPath = Path.GetFullPath(fishModelPath);
            Fish.InitializeGraphics(fishShader, fishTextureId, absoluteModelPath);


            var rand = new Random();
            fishes.Clear();
            float widthX = aquariumMaxBounds.X - aquariumMinBounds.X;
            float widthY = aquariumMaxBounds.Y - aquariumMinBounds.Y;
            float widthZ = aquariumMaxBounds.Z - aquariumMinBounds.Z;
            for (int i = 0; i < 15; i++)
            {
                Vector3 pos = new Vector3(
                    (float)(rand.NextDouble() * widthX + aquariumMinBounds.X),
                    (float)(rand.NextDouble() * widthY + aquariumMinBounds.Y),
                    (float)(rand.NextDouble() * widthZ + aquariumMinBounds.Z)
                );

                float theta = (float)(rand.NextDouble() * Math.PI * 2);
                float phi = MathF.Acos(1 - 2 * (float)rand.NextDouble());
                Vector3 vel = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Sin(phi) * MathF.Sin(theta),
                    MathF.Cos(phi)
                );
                var fish = new Fish(pos, vel);
                fishes.Add(fish);
            }

            camera.ResetMouse(MouseState.Position);
        }

        protected override void OnUnload()
        {
            Fish.CleanupGraphics();
            if (cubeVao != -1) GL.DeleteVertexArray(cubeVao);
            if (cubeVbo != -1) GL.DeleteBuffer(cubeVbo);
            if (cubeEbo != -1) GL.DeleteBuffer(cubeEbo);
            cubeVao = cubeVbo = cubeEbo = -1; // Сбрасываем ID
            fishShader?.Delete();
            if (fishTextureId != -1) GL.DeleteTexture(fishTextureId);
            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            float dt = (float)args.Time;

            if (KeyboardState.IsKeyPressed(Keys.Space))
            {
                if (WindowState == WindowState.Fullscreen)
                    WindowState = WindowState.Normal;
                else
                    WindowState = WindowState.Fullscreen;
            }

            if (KeyboardState.IsKeyDown(Keys.Escape)) Close();

            HandleCameraToggleInput();

            Vector3 rayOrigin = Vector3.Zero;
            Vector3 rayDir = Vector3.Zero;
            bool scareModeActive = false;

            if (isCameraAttached)
            {
                camera.HandleInput(KeyboardState, MouseState, dt);
                scareModeActive = false;
            }
            else
            {
                scareModeActive = true;
                rayOrigin = camera.Position;
                rayDir = GetWorldRayDirection(MouseState.Position);
            }

            foreach (var fish in fishes)
            {
                fish.Update(dt, aquariumMinBounds, aquariumMaxBounds, scareModeActive, rayOrigin, rayDir);
            }
        }
        private void HandleCameraToggleInput()
        {
            if (KeyboardState.IsKeyPressed(Keys.Tab))
            {
                isCameraAttached = !isCameraAttached;

                if (isCameraAttached)
                {
                    CursorState = CursorState.Grabbed;
                    camera.ResetMouse(MouseState.Position);
                }
                else
                {
                    CursorState = CursorState.Normal;
                    camera.SetPositionAndLookAt(fixedCameraPosition, fixedCameraTarget);
                }
            }
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            fishShader.Use();

            int viewLoc = GL.GetUniformLocation(fishShader.shader_handle, "view");
            int projLoc = GL.GetUniformLocation(fishShader.shader_handle, "projection");
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);

            int lightPosLoc = GL.GetUniformLocation(fishShader.shader_handle, "lightPos");
            int lightColorLoc = GL.GetUniformLocation(fishShader.shader_handle, "lightColor");
            int viewPosLoc = GL.GetUniformLocation(fishShader.shader_handle, "viewPos");
            if (lightPosLoc != -1) GL.Uniform3(lightPosLoc, lightPos);
            if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, lightColor);
            if (viewPosLoc != -1) GL.Uniform3(viewPosLoc, camera.Position);


            if (cubeVao != -1)
            {
                int modelLoc = GL.GetUniformLocation(fishShader.shader_handle, "model");
                Matrix4 modelIdentity = Matrix4.Identity;
                if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref modelIdentity);

                Vector3 blackColor = Vector3.Zero;
                if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, blackColor);

                GL.BindVertexArray(cubeVao);

                GL.DisableVertexAttribArray(1);
                GL.DisableVertexAttribArray(2);

                GL.DrawElements(PrimitiveType.Lines, cubeIndexCount, DrawElementsType.UnsignedInt, 0);

                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);

                GL.BindVertexArray(0);

                if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, lightColor);
            }

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, fishTextureId);
            int texLoc = GL.GetUniformLocation(fishShader.shader_handle, "texture0");
            if (texLoc != -1) GL.Uniform1(texLoc, 0);

            foreach (var fish in fishes)
            {
                fish.Draw(view, projection, lightPos, lightColor, camera.Position);
            }

            GL.UseProgram(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            if (e.Width == 0 || e.Height == 0) return;
            GL.Viewport(0, 0, e.Width, e.Height);
            if (camera != null)
            {
                camera.AspectRatio = (float)e.Width / e.Height;
            }
        }

        protected override void OnFocusedChanged(FocusedChangedEventArgs e)
        {
            base.OnFocusedChanged(e);
            if (camera != null)
            {
                if (e.IsFocused)
                {
                    if (isCameraAttached)
                    {
                        CursorState = CursorState.Grabbed;
                        camera.ResetMouse(MouseState.Position);
                    }
                }
                else
                {
                    CursorState = CursorState.Normal;
                }
            }
        }
    }
}