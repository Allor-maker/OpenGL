
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using StbImageSharp;

namespace openGL
{
    public class Shader
    {
        private int shader_handle;

        //public int Handle => shader_handle;

        public void LoadShader(string vertexPath, string fragmentPath)
        {
            int vertex_shader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertex_shader, LoadShaderSource(vertexPath));
            GL.CompileShader(vertex_shader);
            GL.GetShader(vertex_shader, ShaderParameter.CompileStatus, out int success1);
            if (success1 == 0)
            {
                Console.WriteLine(GL.GetShaderInfoLog(vertex_shader));
            }

            int fragment_shader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragment_shader, LoadShaderSource(fragmentPath));
            GL.CompileShader(fragment_shader);
            GL.GetShader(fragment_shader, ShaderParameter.CompileStatus, out int success2);
            if (success2 == 0)
            {
                Console.WriteLine(GL.GetShaderInfoLog(fragment_shader));
            }

            shader_handle = GL.CreateProgram();
            GL.AttachShader(shader_handle, vertex_shader);
            GL.AttachShader(shader_handle, fragment_shader);
            GL.LinkProgram(shader_handle);

            // Проверка линковки
            GL.GetProgram(shader_handle, GetProgramParameterName.LinkStatus, out int linkSuccess);
            if (linkSuccess == 0)
            {
                Console.WriteLine(GL.GetProgramInfoLog(shader_handle));
            }

            // Удаляем шейдеры после линковки
            GL.DeleteShader(vertex_shader);
            GL.DeleteShader(fragment_shader);
        }

        public void Use()
        {
            GL.UseProgram(shader_handle);
        }

        public void Delete()
        {
            GL.DeleteProgram(shader_handle);
        }

        private static string LoadShaderSource(string filepath)
        {
            try
            {
                return File.ReadAllText("../../../Shaders/" + filepath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load shader source file: " + e.Message);
                return "";
            }
        }
    }
    internal class Game : GameWindow
    {
        int width, height;
        public Game(int width, int height): base
        (GameWindowSettings.Default,NativeWindowSettings.Default)
        {
            this.CenterWindow(new Vector2i(width, height));
            this.width = width;
            this.height = height;
        }
        //вершины треугольника
        /*float[] vertices = {
            0f,0.5f,0f,
            -0.5f,-0.5f,0f,
            0.5f,-0.5f,0f
        };*/
        float[] vertices = {
            // x,    y,   z
            -0.5f,  0.5f, 0f,  // Top-left     (0)
            0.5f, 0.5f, 0f,  // Bottom-left  (1)
             0.5f, -0.5f, 0f,  // Bottom-right (2)
             -0.5f,  -0.5f, 0f   // Top-right    (3)
        };
        uint[] indices =
        {
            0,1,2,
            2,3,0
        };
        float[] texCoords =
        {
            0f,1f,
            1f,1f,
            1f,0f,
            0f,0f
        };
        int VAO;
        int VBO;
        int EBO;//дескриптор квадрата
        int TextureID;
        int TextureVBO;
        Shader shader_program;

        protected override void OnLoad()
        {
            base.OnLoad();

            // Генерация и настройка VAO
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            // Генерация и настройка VBO (вершины)
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Генерация и настройка EBO
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Генерация и настройка VBO (текстурные координаты)
            TextureVBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, TextureVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, texCoords.Length * sizeof(float), texCoords, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);

            // Загрузка шейдеров
            shader_program = new Shader();
            shader_program.LoadShader("shader.vert", "shader.frag");

            // Генерация и загрузка текстуры
            TextureID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, TextureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult boxTexture = ImageResult.FromStream(File.OpenRead("../../../Textures/419.jpg"), ColorComponents.RedGreenBlueAlpha);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, boxTexture.Width, boxTexture.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, boxTexture.Data);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Отключение привязок
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }


        protected override void OnUnload()
        {
            shader_program.Delete();
            GL.DeleteBuffer(VBO);
            GL.DeleteVertexArray(VAO);
            GL.DeleteBuffer(EBO);
            GL.DeleteTexture(TextureID);
            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.ClearColor(0.3f, 0.3f, 1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            shader_program.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureID);

            GL.BindVertexArray(VAO);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);

            Context.SwapBuffers();
        }


        protected override void OnUpdateFrame(FrameEventArgs args)//также вызывается каждый кадр и обновляет окно, когда оно готово
        {
            base.OnUpdateFrame(args); 
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }
        }
        protected override void OnResize(ResizeEventArgs e)//при изменении размеров окна сообщаем OpenGL, что мы изменили размеры
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
            this.width = e.Width; 
            this.height = e.Height;
        }

    }
}
