
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using StbImageSharp;
using System.Drawing;


namespace openGL
{
    internal class Game : GameWindow
    {
        private int width, height;
        private List<Fish> fishes = new();
        private RectangleF bounds; // Границы для физики

        // Ресурсы для рендеринга рыб
        private Shader fishShader = null!; // Инициализируется в OnLoad
        private int fishTextureId = -1;

        // Матрицы
        private Matrix4 viewMatrix;
        private Matrix4 projectionMatrix;

        public Game(int width, int height) : base
        (GameWindowSettings.Default, new NativeWindowSettings
        {
            Size = new Vector2i(width, height), // Используем переданные размеры
            API = ContextAPI.OpenGL,        // Можно оставить, OpenTK обычно определяет сам
            Profile = ContextProfile.Core,  // !!! Запрашиваем Core профиль !!!
            Flags = ContextFlags.ForwardCompatible, // Рекомендуется для Core профиля
            APIVersion = new Version(3, 3)  // !!! Версия, соответствующая шейдерам (330) !!!
        })
        {
            this.CenterWindow(new Vector2i(width, height)); // Центрируем окно
            this.width = width; // Сохраняем начальные размеры
            this.height = height;
            // Инициализация bounds перенесена в OnResize, т.к. зависит от projectionMatrix
        }

        private int LoadTextureFromFile(string path)
        {
            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);

            try
            {
                StbImage.stbi_set_flip_vertically_on_load(1);

                using (Stream stream = File.OpenRead(path))
                {
                    ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
                }

                // Настройка параметров текстуры (фильтрация, повторение)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat); // Или ClampToEdge
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat); // Или ClampToEdge
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear); // Для лучшего качества при уменьшении
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear); // При увеличении

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); // Генерация мипмапов
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading texture {path}: {e.Message}");
                GL.DeleteTexture(handle); // Удаляем текстуру, если загрузка не удалась
                return -1;
            }

            GL.BindTexture(TextureTarget.Texture2D, 0); // Отвязываем текстуру
            return handle;
        }
        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            // Нормализуем экранные координаты (0,0 вверху слева -> 0..1)
            float normX = screenPos.X / Size.X;
            // Инвертируем Y, т.к. в OpenGL Y растет вверх, а в экранных координатах - вниз
            float normY = 1.0f - screenPos.Y / Size.Y;

            // Преобразуем в мировые координаты на основе текущей ортографической проекции
            // Используем границы из bounds, которые соответствуют проекции
            float worldX = bounds.Left + normX * bounds.Width;
            float worldY = bounds.Top + normY * bounds.Height; // Используем Top и Height

            return new Vector2(worldX, worldY);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(Color4.CornflowerBlue);

            // Загрузка шейдера
            fishShader = new Shader();
            // --- Убедитесь, что путь к шейдерам правильный относительно исполняемого файла ---
            // Возможно, стоит использовать абсолютный путь или копировать шейдеры в папку сборки
            fishShader.LoadShader("shader.vert", "shader.frag");
            if (fishShader.shader_handle <= 0) // Простая проверка, что шейдер слинковался
            {
                Console.WriteLine("Failed to load/link shader program.");
                Close();
                return;
            }

            // Загрузка текстуры
            // --- Убедитесь, что путь к текстуре правильный ---
            fishTextureId = LoadTextureFromFile("../../../Textures/15.png");
            if (fishTextureId == -1)
            {
                Console.WriteLine("Failed to load fish texture.");
                Close();
                return;
            }

            // Настройка матриц (вынесено в OnResize для инициализации и обновления)
            // Вызываем OnResize вручную для первоначальной настройки проекции и bounds
            OnResize(new ResizeEventArgs(Size)); // Используем текущий размер окна

            // Инициализация графики для рыб с вашим шейдером и текстурой
            try
            {
                Fish.InitializeGraphics(fishShader, fishTextureId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing fish graphics: {ex.Message}");
                Close();
                return;
            }

            // Создание рыб
            var rand = new Random();
            fishes.Clear(); // Очищаем список на всякий случай (если OnLoad вызовется повторно)
            for (int i = 0; i < 10; i++) // Или сколько рыб вы хотите
            {
                // Генерируем позицию в пределах bounds
                // bounds задается в OnResize и должен быть актуален к этому моменту
                float posX = (float)(rand.NextDouble() * bounds.Width + bounds.Left);
                float posY = (float)(rand.NextDouble() * bounds.Height + bounds.Top); // Используем Bottom и Height для Y
                var pos = new Vector2(posX, posY);

                // Генерируем случайное начальное направление и скорость
                var vel = new Vector2((float)(rand.NextDouble() - 0.5f), (float)(rand.NextDouble() - 0.5f));
                vel.Normalize(); // Делаем вектор единичной длины
                vel *= (float)(rand.NextDouble() * 0.3 + 0.1); // Задаем случайную скорость (от 0.1 до 0.4)

                var fish = new Fish(pos, vel) { Size = 0.15f };
                fishes.Add(fish);
            }

            // Если текстура с прозрачностью, включить блендинг
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Включение теста глубины не обязательно для 2D, но если будете добавлять 3D - раскомментируйте
            // GL.Enable(EnableCap.DepthTest);
        }




        protected override void OnUnload()
        {
            // Освобождаем VAO/VBO/EBO рыб
            Fish.CleanupGraphics();

            // Удаляем шейдер
            fishShader?.Delete(); // Используем безопасный вызов

            // Удаляем текстуру
            if (fishTextureId != -1)
            {
                GL.DeleteTexture(fishTextureId);
                fishTextureId = -1; // Сбрасываем ID
            }

            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            // Очищаем цвет и глубину
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Здесь можно обновлять viewMatrix, если камера движется

            // !!! Рисуем каждую рыбу, передавая матрицы !!!
            foreach (var fish in fishes)
            {
                fish.Draw(viewMatrix, projectionMatrix);
            }

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            // Получаем текущее состояние мыши
            MouseState mouseState = MouseState;
            // Получаем позицию курсора в экранных координатах
            Vector2 mouseScreenPos = mouseState.Position;
            // Конвертируем в мировые координаты
            Vector2 mouseWorldPos = ScreenToWorld(mouseScreenPos);

            // Обновляем позицию и проверяем столкновения для каждой рыбы,
            // передавая мировые координаты курсора
            foreach (var fish in fishes)
            {
                fish.Update((float)args.Time, bounds, mouseWorldPos); // !!! Добавлен mouseWorldPos !!!
            }

            // Проверка нажатия Escape для выхода (пример)
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            if (e.Width == 0 || e.Height == 0) return; // Избегаем деления на ноль

            GL.Viewport(0, 0, e.Width, e.Height);
            this.width = e.Width;
            this.height = e.Height;

            // Обновляем матрицу проекции для сохранения соотношения сторон
            float aspectRatio = (float)e.Width / e.Height;
            // Определяем желаемую высоту видимой области в мировых координатах
            // Например, если хотим, чтобы по вертикали всегда было видно от -1 до 1
            float orthoHeight = 1.0f;
            float orthoWidth = orthoHeight * aspectRatio;

            // Ортографическая проекция
            projectionMatrix = Matrix4.CreateOrthographicOffCenter(-orthoWidth, orthoWidth, -orthoHeight, orthoHeight, 0.1f, 100.0f);

            // Обновляем view матрицу (простая, смотрит прямо)
            // Позиция камеры чуть дальше от плоскости XY, смотрит на начало координат
            viewMatrix = Matrix4.LookAt(new Vector3(0, 0, 1), Vector3.Zero, Vector3.UnitY);

            // Обновляем границы для физики рыб
            // !!! Важно: System.Drawing.RectangleF(float x, float y, float width, float height)
            // x, y - координаты ЛЕВОГО НИЖНЕГО угла (если Y растет вверх)
            bounds = new RectangleF(-orthoWidth, -orthoHeight, orthoWidth * 2, orthoHeight * 2);
        }

    }

}
