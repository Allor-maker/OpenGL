using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace openGL
{
    public class Fish
    {
        public Vector3 Position; // 3D
        public Vector3 Velocity; // 3D
        public float Size = 0.05f;

        // Параметры поведения (без изменений)
        private static readonly float FleeRadius = 0.8f;
        private static readonly float FleeRadiusSq = FleeRadius * FleeRadius;
        private static readonly float FleeStrength = 2.5f;
        private static readonly float MaxSpeed = 1.2f;
        private static readonly float NormalSpeedMin = 0.2f;
        private static readonly float NormalSpeedMax = 0.5f;
        private float normalSpeed;

        // Графические ресурсы (без изменений)
        private static int s_vao = -1, s_vbo_pos = -1, s_vbo_tex = -1, s_vbo_norm = -1, s_ebo = -1;
        private static int s_indexCount = 0;
        private static Shader? s_shader = null;
        private static int s_textureId = -1;
        private static bool s_graphicsInitialized = false;
        private static int s_modelLoc = -1, s_viewLoc = -1, s_projectionLoc = -1, s_texture0Loc = -1;
        private static int s_lightPosLoc = -1, s_lightColorLoc = -1, s_viewPosLoc = -1;

        private static readonly Random s_random = new Random();

        // Конструктор (без изменений)
        public Fish(Vector3 position, Vector3 velocity)
        {
            Position = position;
            normalSpeed = (float)(s_random.NextDouble() * (NormalSpeedMax - NormalSpeedMin) + NormalSpeedMin);
            if (velocity.LengthSquared > 0.001f)
            {
                Velocity = velocity.Normalized() * normalSpeed;
            }
            else
            {
                float theta = (float)(s_random.NextDouble() * Math.PI * 2);
                float phi = MathF.Acos(1 - 2 * (float)s_random.NextDouble());
                Velocity = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Sin(phi) * MathF.Sin(theta),
                    MathF.Cos(phi)
                ) * normalSpeed;
            }
        }

        // --- Обновление физики и поведения с новыми границами ---
        // Сигнатура метода изменена: принимает minBounds и maxBounds вместо BoundingBox
        public void Update(float deltaTime, Vector3 minBounds, Vector3 maxBounds,
                   bool scareModeActive, Vector3 rayOrigin, Vector3 rayDir)
        {
            bool isFleeing = false;
            Vector3 fleeAcceleration = Vector3.Zero;

            if (scareModeActive && rayDir.LengthSquared > 0.0001f) // Убегаем только если режим активен и луч валидный
            {
                // Вычисляем ближайшую точку на луче к рыбе
                Vector3 vectorToFish = Position - rayOrigin; // Вектор от начала луча к рыбе
                float t = Vector3.Dot(vectorToFish, rayDir); // Проекция на направление луча

                Vector3 closestPointOnRay;
                if (t < 0) // Ближайшая точка на ЛИНИИ находится позади камеры
                {
                    // Ближайшая точка на самом ЛУЧЕ - это его начало (позиция камеры)
                    closestPointOnRay = rayOrigin;
                }
                else // Ближайшая точка на ЛИНИИ находится на луче
                {
                    closestPointOnRay = rayOrigin + rayDir * t;
                }

                // Вычисляем вектор и дистанцию от рыбы до этой ближайшей точки на луче
                Vector3 diff = Position - closestPointOnRay;
                float distSq = diff.LengthSquared;

                // Проверяем, находится ли рыба в радиусе действия луча
                if (distSq < FleeRadiusSq && distSq > 0.0001f)
                {
                    isFleeing = true;
                    // Ускорение направлено ОТ ближайшей точки на луче К рыбе (вдоль diff)
                    if (diff.LengthSquared > 0.0001f)
                        fleeAcceleration = diff.Normalized() * FleeStrength;
                }
            }

            // Ускорение и скорость (без изменений)
            Velocity += fleeAcceleration * deltaTime;

            float targetSpeed = isFleeing ? MaxSpeed : normalSpeed;
            if (Velocity.LengthSquared > 0.0001f)
            {
                if (isFleeing && Velocity.LengthSquared > MaxSpeed * MaxSpeed)
                    Velocity = Velocity.Normalized() * MaxSpeed;
                else
                    Velocity = Velocity.Normalized() * targetSpeed;
            }
            else if (!isFleeing)
            {
                float theta = (float)(s_random.NextDouble() * Math.PI * 2);
                float phi = MathF.Acos(1 - 2 * (float)s_random.NextDouble());
                Velocity = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Sin(phi) * MathF.Sin(theta),
                    MathF.Cos(phi)
                ) * normalSpeed;
            }

            // --- Отскок от стенок: используем minBounds и maxBounds ---
            bool bounced = false;
            if (Position.X < minBounds.X || Position.X > maxBounds.X) { Velocity.X = -Velocity.X; bounced = true; }
            if (Position.Y < minBounds.Y || Position.Y > maxBounds.Y) { Velocity.Y = -Velocity.Y; bounced = true; }
            if (Position.Z < minBounds.Z || Position.Z > maxBounds.Z) { Velocity.Z = -Velocity.Z; bounced = true; }
            // --- Конец отскока ---

            // --- "Заталкиваем" рыбу обратно: используем minBounds и maxBounds ---
            // (Важно! Добавил проверку, чтобы рыба не застряла, если случайно Velocity станет 0 на границе)
            if (Position.X <= minBounds.X && Velocity.X < 0) Position.X = minBounds.X + 0.001f;
            if (Position.X >= maxBounds.X && Velocity.X > 0) Position.X = maxBounds.X - 0.001f;
            if (Position.Y <= minBounds.Y && Velocity.Y < 0) Position.Y = minBounds.Y + 0.001f;
            if (Position.Y >= maxBounds.Y && Velocity.Y > 0) Position.Y = maxBounds.Y - 0.001f;
            if (Position.Z <= minBounds.Z && Velocity.Z < 0) Position.Z = minBounds.Z + 0.001f;
            if (Position.Z >= maxBounds.Z && Velocity.Z > 0) Position.Z = maxBounds.Z - 0.001f;
            // Старый вариант с ComponentMax/Min тоже рабочий, но этот чуть надежнее от застревания:
            // Position = Vector3.ComponentMax(minBounds + new Vector3(0.01f), Position);
            // Position = Vector3.ComponentMin(maxBounds - new Vector3(0.01f), Position);
            // --- Конец заталкивания ---


            // Случайные повороты (без изменений)
            float turnStrength = 0.5f;
            if (!bounced && s_random.NextDouble() < 0.05)
            {
                Vector3 randomDir = new Vector3(
                    (float)s_random.NextDouble() - 0.5f,
                    (float)s_random.NextDouble() - 0.5f,
                    (float)s_random.NextDouble() - 0.5f);
                if (randomDir.LengthSquared > 0.001f)
                {
                    Velocity += randomDir.Normalized() * turnStrength * deltaTime;
                    if (Velocity.LengthSquared > 0.0001f)
                        Velocity = Velocity.Normalized() * targetSpeed;
                }
            }

            // Обновление позиции (без изменений)
            Position += Velocity * deltaTime;
        }

        // Инициализация графики (без изменений)
        public static void InitializeGraphics(Shader fishShader, int fishTextureId, string modelPath)
        {
            if (s_graphicsInitialized) return;
            if (fishShader == null) throw new ArgumentNullException(nameof(fishShader));
            if (fishTextureId < 0) throw new ArgumentException("Invalid texture ID.", nameof(fishTextureId));
            if (string.IsNullOrEmpty(modelPath)) throw new ArgumentNullException(nameof(modelPath));

            s_shader = fishShader;
            s_textureId = fishTextureId;

            if (!ObjLoader.LoadObj(modelPath, out var vertices, out var indices))
            { Console.WriteLine($"Failed to load model {modelPath}"); return; }
            if (vertices.Count == 0 || indices.Count == 0)
            { Console.WriteLine($"Model {modelPath} loaded empty."); return; }

            s_indexCount = indices.Count;
            List<Vector3> positions = vertices.Select(v => v.Position).ToList();
            List<Vector2> texCoords = vertices.Select(v => v.TexCoord).ToList();
            List<Vector3> normals = vertices.Select(v => v.Normal).ToList();

            s_vao = GL.GenVertexArray();
            s_vbo_pos = GL.GenBuffer(); s_vbo_tex = GL.GenBuffer(); s_vbo_norm = GL.GenBuffer();
            s_ebo = GL.GenBuffer();
            GL.BindVertexArray(s_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo_pos);
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Count * Vector3.SizeInBytes, positions.ToArray(), BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo_tex);
            GL.BufferData(BufferTarget.ArrayBuffer, texCoords.Count * Vector2.SizeInBytes, texCoords.ToArray(), BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo_norm);
            GL.BufferData(BufferTarget.ArrayBuffer, normals.Count * Vector3.SizeInBytes, normals.ToArray(), BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 0, 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, s_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

            s_modelLoc = GL.GetUniformLocation(s_shader.shader_handle, "model");
            s_viewLoc = GL.GetUniformLocation(s_shader.shader_handle, "view");
            s_projectionLoc = GL.GetUniformLocation(s_shader.shader_handle, "projection");
            s_texture0Loc = GL.GetUniformLocation(s_shader.shader_handle, "texture0");
            s_lightPosLoc = GL.GetUniformLocation(s_shader.shader_handle, "lightPos");
            s_lightColorLoc = GL.GetUniformLocation(s_shader.shader_handle, "lightColor");
            s_viewPosLoc = GL.GetUniformLocation(s_shader.shader_handle, "viewPos");

            if (s_modelLoc == -1 || s_viewLoc == -1 || s_projectionLoc == -1 || s_texture0Loc == -1 ||
                s_lightPosLoc == -1 || s_lightColorLoc == -1 || s_viewPosLoc == -1)
            { Console.WriteLine("Warning: Missing uniform locations."); }

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            s_graphicsInitialized = true;
            Console.WriteLine("Fish graphics initialized successfully with 3D model.");
        }

        // Очистка графики (без изменений)
        public static void CleanupGraphics()
        {
            if (!s_graphicsInitialized) return;
            GL.DeleteBuffer(s_vbo_pos); GL.DeleteBuffer(s_vbo_tex); GL.DeleteBuffer(s_vbo_norm);
            GL.DeleteBuffer(s_ebo); GL.DeleteVertexArray(s_vao);
            s_vbo_pos = s_vbo_tex = s_vbo_norm = s_ebo = s_vao = -1;
            s_shader = null; s_textureId = -1; s_indexCount = 0;
            s_graphicsInitialized = false;
            Console.WriteLine("Fish graphics cleaned up.");
        }

        // Отрисовка (без изменений)
        public void Draw(Matrix4 view, Matrix4 projection, Vector3 lightPos, Vector3 lightColor, Vector3 viewPos)
        {
            // Проверки оставляем
            if (!s_graphicsInitialized || s_shader == null || s_indexCount == 0) return;

            // 1. Вычисляем Model матрицу (как и было)
            Matrix4 model = Matrix4.Identity;
            model *= Matrix4.CreateScale(Size);
            if (Velocity.LengthSquared > 0.0001f)
            {
                Vector3 targetDir = Velocity.Normalized();
                Vector3 rotAxis = Vector3.Cross(Vector3.UnitZ, targetDir);
                float angle = MathF.Acos(Vector3.Dot(Vector3.UnitZ, targetDir));
                if (rotAxis.LengthSquared > 0.0001f)
                    model *= Matrix4.CreateFromAxisAngle(rotAxis.Normalized(), angle);
                else if (targetDir.Z < 0)
                    model *= Matrix4.CreateRotationY(MathHelper.Pi);
            }
            model *= Matrix4.CreateTranslation(Position);

            // 2. Устанавливаем ТОЛЬКО model матрицу (view/proj/light/viewPos устанавливаются в Game.OnRenderFrame)
            // Локации s_modelLoc и т.д. должны быть получены заранее в InitializeGraphics
            if (s_modelLoc != -1) GL.UniformMatrix4(s_modelLoc, false, ref model);
            // Остальные uniforms (view, proj, light, viewPos, texture0) устанавливаются снаружи

            // 3. Привязываем VAO рыбы
            GL.BindVertexArray(s_vao);

            // 4. Рисуем модель рыбы
            GL.DrawElements(PrimitiveType.Triangles, s_indexCount, DrawElementsType.UnsignedInt, 0);

            // 5. Отвязываем VAO рыбы
            GL.BindVertexArray(0);
        }
    }
}