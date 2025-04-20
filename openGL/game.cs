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

        private Vector3 aquariumMinBounds; // Минимальные координаты аквариума (левый нижний ближний угол)
        private Vector3 aquariumMaxBounds; // Максимальные координаты аквариума (правый верхний дальний угол)

        private Shader fishShader = null!;
        private int fishTextureId = -1;
        private string fishModelPath = "../../../Models/finalfish.obj"; // Путь к модели
        private string fishTexturePath = "../../../Textures/fish.png"; // Путь к текстуре

        private int cubeVao = -1;
        private int cubeVbo = -1;
        private int cubeEbo = -1;
        private readonly int cubeIndexCount = 24; // 12 ребер * 2 вершины на ребро = 24 индекса для лини

        private Camera camera = null!;
        private bool isCameraAttached = true;
        private readonly Vector3 fixedCameraPosition = new Vector3(0, 0, 3); // Начальная позиция
        private readonly Vector3 fixedCameraTarget = Vector3.Zero;      // Куда смотрит камера в фикс. режиме

        private Vector3 lightPos = new Vector3(0.0f, 2.0f, 2.0f); // Позиция источника света
        private Vector3 lightColor = new Vector3(1.0f, 1.0f, 1.0f); // Белый свет

        private Vector3 scarePointPosition = Vector3.Zero;

        public Game(int width, int height) : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            Size = new Vector2i(width, height),
            API = ContextAPI.OpenGL,
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            APIVersion = new Version(3, 3)
        })
        {
            // Конструктор остается без изменений
        }

        // Загрузка текстуры (без изменений)
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
            // Вершины куба (8 углов) - используем текущие min/max границы
            float minX = aquariumMinBounds.X; float minY = aquariumMinBounds.Y; float minZ = aquariumMinBounds.Z;
            float maxX = aquariumMaxBounds.X; float maxY = aquariumMaxBounds.Y; float maxZ = aquariumMaxBounds.Z;

            float[] vertices =
            {
        minX, minY, minZ, // 0: Левый нижний ближний
        maxX, minY, minZ, // 1: Правый нижний ближний
        maxX, maxY, minZ, // 2: Правый верхний ближний
        minX, maxY, minZ, // 3: Левый верхний ближний
        minX, minY, maxZ, // 4: Левый нижний дальний
        maxX, minY, maxZ, // 5: Правый нижний дальний
        maxX, maxY, maxZ, // 6: Правый верхний дальний
        minX, maxY, maxZ  // 7: Левый верхний дальний
    };

            // Индексы для рисования 12 ребер линиями
            uint[] indices =
            {
        0, 1, 1, 2, 2, 3, 3, 0, // Нижний квадрат (ближний)
        4, 5, 5, 6, 6, 7, 7, 4, // Верхний квадрат (дальний)
        0, 4, 1, 5, 2, 6, 3, 7  // Соединяющие ребра
    };

            // Проверяем, что шейдер рыб уже загружен
            if (fishShader == null || fishShader.shader_handle <= 0)
            {
                Console.WriteLine("Error: fishShader not ready for bounds graphics setup.");
                return;
            }

            // --- Настройка VAO/VBO/EBO ---
            cubeVao = GL.GenVertexArray();
            cubeVbo = GL.GenBuffer();
            cubeEbo = GL.GenBuffer();

            GL.BindVertexArray(cubeVao); // Привязываем VAO куба

            // VBO с вершинами
            GL.BindBuffer(BufferTarget.ArrayBuffer, cubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // EBO с индексами
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, cubeEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Атрибут позиции (location = 0) - такой же, как у рыб
            GL.EnableVertexAttribArray(0);
            // Указываем, что читать 3 float'а на вершину, без шага и смещения (т.к. данные плотно упакованы)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // Отвязываем VAO, чтобы не изменить его случайно
            GL.BindVertexArray(0);
            // Можно отвязать и буферы
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

            Console.WriteLine("Aquarium bounds graphics initialized (using fish shader).");
        }
        private Vector3 GetWorldRayDirection(Vector2 screenPos)
        {
            if (camera == null) return Vector3.Zero;

            // 1. NDC [-1, 1]
            float x = (2.0f * screenPos.X) / Size.X - 1.0f;
            float y = 1.0f - (2.0f * screenPos.Y) / Size.Y;
            Vector4 ray_clip = new Vector4(x, y, -1.0f, 1.0f); // Z = -1 (ближняя плоскость)

            // 2. Eye space (пространство камеры)
            Matrix4 projInv = camera.GetProjectionMatrix().Inverted();
            Vector4 ray_eye = ray_clip * projInv;
            ray_eye.Z = -1.0f; // Направление вперед от камеры
            ray_eye.W = 0.0f;   // Это вектор направления

            // 3. World space direction
            Matrix4 viewInv = camera.GetViewMatrix().Inverted();
            Vector4 ray_wor_4 = ray_eye * viewInv;
            return ray_wor_4.Xyz.Normalized(); // Возвращаем нормализованное направление
        }
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.05f, 0.1f, 0.2f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            fishShader = new Shader();
            fishShader.LoadShader("shader.vert", "shader.frag");
            if (fishShader.shader_handle <= 0) { Console.WriteLine("Failed shader load."); Close(); return; }

            fishTextureId = LoadTextureFromFile(fishTexturePath);
            if (fishTextureId == -1) { Console.WriteLine("Failed texture load."); Close(); return; }

            camera = new Camera(new Vector3(0, 0, 3), (float)Size.X / Size.Y);
            CursorState = CursorState.Grabbed;

            float aquariumSize = 2.0f; // Размер полустороны куба аквариума
            aquariumMinBounds = new Vector3(-aquariumSize, -aquariumSize, -aquariumSize);
            aquariumMaxBounds = new Vector3(aquariumSize, aquariumSize, aquariumSize);

            SetupAquariumBoundsGraphics();
            try
            {
                string absoluteModelPath = Path.GetFullPath(fishModelPath);
                Console.WriteLine($"Attempting to load model: {absoluteModelPath}");
                if (!File.Exists(absoluteModelPath))
                {
                    Console.WriteLine($"Model file not found at: {absoluteModelPath}");
                    Close(); return;
                }
                Fish.InitializeGraphics(fishShader, fishTextureId, absoluteModelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing fish graphics: {ex.Message}");
                Close(); return;
            }

            var rand = new Random();
            fishes.Clear();
            for (int i = 0; i < 15; i++)
            {
                Vector3 pos = new Vector3(
                    (float)(rand.NextDouble() * (aquariumMaxBounds.X - aquariumMinBounds.X) + aquariumMinBounds.X),
                    (float)(rand.NextDouble() * (aquariumMaxBounds.Y - aquariumMinBounds.Y) + aquariumMinBounds.Y),
                    (float)(rand.NextDouble() * (aquariumMaxBounds.Z - aquariumMinBounds.Z) + aquariumMinBounds.Z)
                );

                float theta = (float)(rand.NextDouble() * Math.PI * 2);
                float phi = MathF.Acos(1 - 2 * (float)rand.NextDouble());
                Vector3 vel = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Sin(phi) * MathF.Sin(theta),
                    MathF.Cos(phi)
                );
                var fish = new Fish(pos, vel) { Size = (float)(rand.NextDouble() * 0.03 + 0.04) };
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

        // ЗАМЕНИ ВЕСЬ СУЩЕСТВУЮЩИЙ МЕТОД OnUpdateFrame В Game.cs НА ЭТОТ:

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            float dt = (float)args.Time;

            // Проверка фокуса окна
            if (!IsFocused)
            {
                // Если окно теряет фокус и камера была привязана, освобождаем курсор
                if (isCameraAttached)
                {
                    CursorState = CursorState.Normal;
                    // Сбрасываем позицию мыши, чтобы не было скачка при возвращении фокуса
                    camera.ResetMouse(MouseState.Position);
                }
                return; // Не обрабатываем ввод, если окно не в фокусе
            }

            // Переключение полноэкранного режима по F11
            if (KeyboardState.IsKeyPressed(Keys.Space))
            {
                if (WindowState == WindowState.Fullscreen)
                {
                    WindowState = WindowState.Normal;
                    Console.WriteLine("Exiting fullscreen mode.");
                }
                else
                {
                    WindowState = WindowState.Fullscreen;
                    Console.WriteLine("Entering fullscreen mode.");
                }
            }

            // Выход по Escape
            if (KeyboardState.IsKeyDown(Keys.Escape)) Close();

            // Переключение режимов камеры (Tab)
            // Этот метод определяет isCameraAttached и перемещает/фиксирует камеру
            HandleCameraToggleInput();

            // --- Подготовка данных для обновления рыб ---
            Vector3 rayOrigin = Vector3.Zero; // Начало луча
            Vector3 rayDir = Vector3.Zero;    // Направление луча
            bool scareModeActive = false;     // Флаг, активен ли режим пугания

            if (isCameraAttached) // Режим СВОБОДНОЙ камеры
            {
                // Управляем камерой как обычно
                camera.HandleInput(KeyboardState, MouseState, dt);
                scareModeActive = false; // Пугание не активно
                                         // rayOrigin и rayDir остаются нулевыми, т.к. не используются
            }
            else // Режим ФИКСИРОВАННОЙ камеры (пугания)
            {
                // Камера НЕ управляется вводом (она зафиксирована методом HandleCameraToggleInput)
                scareModeActive = true; // Пугание активно
                rayOrigin = camera.Position; // Начало луча - текущая (фиксированная) позиция камеры
                rayDir = GetWorldRayDirection(MouseState.Position); // Направление луча из положения курсора
            }
            // --- Конец подготовки ---

            // --- Обновление рыб ---
            // Передаем режим, начало и направление луча (если scareModeActive = true)
            foreach (var fish in fishes)
            {
                fish.Update(dt, aquariumMinBounds, aquariumMaxBounds, scareModeActive, rayOrigin, rayDir);
            }
            // --- Конец обновления ---
        }
        private void HandleCameraToggleInput()
        {
            if (KeyboardState.IsKeyPressed(Keys.Tab))
            {
                isCameraAttached = !isCameraAttached; // Инвертируем флаг

                if (isCameraAttached) // Переход в режим СВОБОДНОЙ камеры
                {
                    CursorState = CursorState.Grabbed; // Захватываем курсор
                    camera.ResetMouse(MouseState.Position); // Сбрасываем дельту мыши
                    Console.WriteLine("Camera Attached (Free Look)");
                }
                else // Переход в режим ФИКСИРОВАННОЙ камеры (пугания)
                {
                    CursorState = CursorState.Normal; // Освобождаем курсор
                                                      // Устанавливаем камеру в фиксированную позицию и ориентацию
                    camera.SetPositionAndLookAt(fixedCameraPosition, fixedCameraTarget);
                    Console.WriteLine("Camera Detached (Fixed Scare Mode)");
                }
            }
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            // Используем шейдер рыб для всего
            fishShader.Use();

            // Устанавливаем матрицы вида и проекции один раз для всех
            int viewLoc = GL.GetUniformLocation(fishShader.shader_handle, "view");
            int projLoc = GL.GetUniformLocation(fishShader.shader_handle, "projection");
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);

            // Устанавливаем параметры света и камеры один раз (шейдер один)
            int lightPosLoc = GL.GetUniformLocation(fishShader.shader_handle, "lightPos");
            int lightColorLoc = GL.GetUniformLocation(fishShader.shader_handle, "lightColor");
            int viewPosLoc = GL.GetUniformLocation(fishShader.shader_handle, "viewPos");
            if (lightPosLoc != -1) GL.Uniform3(lightPosLoc, lightPos);
            if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, lightColor);
            if (viewPosLoc != -1) GL.Uniform3(viewPosLoc, camera.Position);

            // --- Рисуем границы аквариума ---
            if (cubeVao != -1) // Проверка, что ресурсы созданы
            {
                // Устанавливаем model матрицу для куба (единичная)
                int modelLoc = GL.GetUniformLocation(fishShader.shader_handle, "model");
                Matrix4 modelIdentity = Matrix4.Identity;
                if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref modelIdentity);

                // --- "Выключаем" текстуру и свет (условно) для линий ---
                // Установим цвет света в 0, чтобы убрать диффузный и зеркальный вклад
                // Фоновый (ambient) все еще может дать слабый цвет линиям
                Vector3 blackColor = Vector3.Zero;
                if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, blackColor);
                // Примечание: линии все равно могут быть окрашены текселем из текстуры рыбы,
                // если текстурный юнит 0 остался активным. Цвет будет непредсказуемым.
                // Чтобы гарантировать цвет линий, нужна модификация шейдера.

                GL.BindVertexArray(cubeVao); // Привязываем VAO куба

                // --- Трюк: Отключаем атрибуты текстуры и нормалей (locations 1 и 2) ---
                GL.DisableVertexAttribArray(1);
                GL.DisableVertexAttribArray(2);

                // Рисуем линии
                GL.DrawElements(PrimitiveType.Lines, cubeIndexCount, DrawElementsType.UnsignedInt, 0);

                // --- Включаем атрибуты обратно для рыб! ---
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);

                GL.BindVertexArray(0); // Отвязываем VAO куба

                // --- Восстанавливаем цвет света для рыб ---
                if (lightColorLoc != -1) GL.Uniform3(lightColorLoc, lightColor);
            }
            // --- Конец отрисовки границ ---

            // --- Рисуем всех рыб ---
            // Шейдер уже активен, view/proj/light/viewPos уже установлены
            // Активируем текстурный юнит и привязываем текстуру рыбы
            GL.ActiveTexture(TextureUnit.Texture0); // Важно делать перед циклом рыб
            GL.BindTexture(TextureTarget.Texture2D, fishTextureId); // Привязываем текстуру
            int texLoc = GL.GetUniformLocation(fishShader.shader_handle, "texture0");
            if (texLoc != -1) GL.Uniform1(texLoc, 0); // Сообщаем шейдеру использовать юнит 0

            foreach (var fish in fishes)
            {
                // Вызываем Draw, который теперь ТОЛЬКО устанавливает model матрицу
                // и вызывает GL.DrawElements для VAO рыбы
                fish.Draw(view, projection, lightPos, lightColor, camera.Position);
            }

            // Отвязываем шейдер и текстуру после отрисовки всего
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
                    // При получении фокуса, привязываем курсор ТОЛЬКО ЕСЛИ камера должна быть привязана
                    if (isCameraAttached)
                    {
                        CursorState = CursorState.Grabbed;
                        camera.ResetMouse(MouseState.Position);
                    }
                }
                else // Если потеряло фокус, ВСЕГДА освобождаем курсор
                {
                    CursorState = CursorState.Normal;
                }
            }
        }
    }
}