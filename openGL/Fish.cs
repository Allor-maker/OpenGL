using OpenTK.Graphics.OpenGL4; // Нужен для GL, VAO, VBO, EBO, Uniforms, Textures и т.д.
using OpenTK.Mathematics;     // Нужен для Vector2, Vector3, Matrix4
using System.Drawing;

namespace openGL
{
    public class Fish
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size = 0.1f; // размер квадрата
                                  

        private float normalSpeed; // Нормальная скорость этой рыбы
        // общие для всех рыб поля
        private static int s_vao = -1;          // Vertex Array Object для квадрата
        private static int s_vbo = -1;          // Vertex Buffer Object для вершин/текстур
        private static int s_ebo = -1;          // Element Buffer Object для индексов
        private static Shader? s_shader = null; // Ссылка на ваш объект Shader
        private static int s_textureId = -1;    // ID текстуры рыбы

        // Локации Uniform-переменных
        private static int s_modelLoc = -1;
        private static int s_viewLoc = -1;
        private static int s_projectionLoc = -1;
        private static int s_texture0Loc = -1;

        private static bool s_graphicsInitialized = false;

        // Вершины квадрата: Позиция(x,y,z) + Текстурные Координаты(s,t)
        // Центр квадрата в (0,0)
        private static readonly float[] s_quadVertices =
        {
            // Позиция          Текстурные коорд.
            -0.5f,  0.5f, 0.0f,  0.0f, 1.0f, // Верхний левый угол
             0.5f,  0.5f, 0.0f,  1.0f, 1.0f, // Верхний правый угол
             0.5f, -0.5f, 0.0f,  1.0f, 0.0f, // Нижний правый угол
            -0.5f, -0.5f, 0.0f,  0.0f, 0.0f  // Нижний левый угол
        };

        // Индексы для отрисовки квадрата двумя треугольниками
        private static readonly uint[] s_quadIndices =
        {
            0, 1, 2, // Первый треугольник
            0, 2, 3  // Второй треугольник
        };


        // --- Добавляем константы для поведения убегания ---
        private static readonly float FleeRadius = 0.4f; // рвдиус обнаружения курсора
        private static readonly float FleeRadiusSq = FleeRadius * FleeRadius;//используем для вычисления растояния от Postion до курсора
        private static readonly float FleeStrength = 2.5f; 
        private static readonly float MaxSpeed = 1.2f;     // Максимальная скорость при убегании
        private static readonly float MaxSpeedSq = MaxSpeed * MaxSpeed; // Не используется, но оставим для справки
        private static readonly float NormalSpeedMin = 0.2f;
        private static readonly float NormalSpeedMax = 0.5f;

        private static readonly Random s_random = new Random();

        public Fish(Vector2 position, Vector2 velocity)
        {
            Position = position;
            normalSpeed = (float)(s_random.NextDouble() * (NormalSpeedMax - NormalSpeedMin) + NormalSpeedMin);//выбираем случайную скорость для рыбки

            if (velocity.LengthSquared > 0.001f)//скорость задана->используем ее вектор
            {
                Velocity = velocity.Normalized() * normalSpeed;
            }
            else
            {   
                //генерируем случайный угол от 0 до 2Pi
                float angle = (float)(s_random.NextDouble() * Math.PI * 2);
                //создаем вектор скорости на основе этого угла и нормальной скорости
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * normalSpeed;
            }
        }

        public void Update(float deltaTime, RectangleF bounds, Vector2 cursorWorldPos)
        {
            bool isFleeing = false;//убегает?
            Vector2 fleeAcceleration = Vector2.Zero; // Ускорение от курсора

            //Проверяем, нужно ли убегать
            Vector2 diff = Position - cursorWorldPos;//вектор от курсора к рыбке
            float distSq = diff.LengthSquared; //расстояние между position и курсором
            if (distSq < FleeRadiusSq && distSq > 0.0001f)//убегаем
            {
                isFleeing = true;
                Vector2 fleeDirection = diff.Normalized();//нормализуем вектор, чтобы получить только направление
                fleeAcceleration = fleeDirection * FleeStrength;//вектор ускорения как направление от курсора к рыбке, домноженное на силу
            }

            // Применяем ускорение от курсора (если есть)
            Velocity += fleeAcceleration * deltaTime;//v= v0 +at

            // Определяем целевую скорость
            float targetSpeed = isFleeing ? MaxSpeed : normalSpeed;

            // Устанавливаем скорость равной целевой, сохраняя направление
            //    (если текущая скорость не нулевая)
            if (Velocity.LengthSquared > 0.0001f) // Избегаем нормализации нулевого вектора
            {
                Velocity = Velocity.Normalized() * targetSpeed; //устанавливаем вычисленную скорость
            }
            else if (!isFleeing) // Если скорость была нулевой и не убегаем
            {
                // Можно задать случайное направление с нормальной скоростью,
                // чтобы рыба не "застревала" если ее скорость обнулилась
                float angle = (float)(s_random.NextDouble() * Math.PI * 2);
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * normalSpeed;
            }


            // Отскок от стенок
            bool bounced = false;
            float halfSize = Size / 2.0f;
            if (Position.X - halfSize < bounds.Left || Position.X + halfSize > bounds.Right)
            {
                Velocity.X = -Velocity.X;
                Position.X = Math.Clamp(Position.X, bounds.Left + halfSize + 0.001f, bounds.Right - halfSize - 0.001f);
                bounced = true;
            }
            if (Position.Y - halfSize < bounds.Top || Position.Y + halfSize > bounds.Bottom)
            {
                Velocity.Y = -Velocity.Y;
                Position.Y = Math.Clamp(Position.Y, bounds.Top + halfSize + 0.001f, bounds.Bottom - halfSize - 0.001f);
                bounced = true;
            }

            // Случайные повороты 
            // Поворачиваем вектор скорости, а не добавляем к нему
            if (!bounced && s_random.NextDouble() < 0.02)
            {
                float turnAngle = (float)(s_random.NextDouble() - 0.5) * 1.0f * deltaTime; // Маленький угол поворота
                float cos = MathF.Cos(turnAngle);
                float sin = MathF.Sin(turnAngle);
                float newX = Velocity.X * cos - Velocity.Y * sin;
                float newY = Velocity.X * sin + Velocity.Y * cos;
                Velocity = new Vector2(newX, newY);

                // После поворота скорость могла чуть измениться, вернем ее к целевой
                if (Velocity.LengthSquared > 0.0001f)
                {
                    float speedAfterTurn = isFleeing ? MaxSpeed : normalSpeed;
                    // Ограничиваем скорость только если убегаем И она превысила MaxSpeed
                    if (isFleeing && Velocity.LengthSquared > speedAfterTurn * speedAfterTurn)
                    {
                        Velocity = Velocity.Normalized() * speedAfterTurn;
                    }
                    else if (!isFleeing) // Если не убегаем, всегда ставим normalSpeed
                    {
                        Velocity = Velocity.Normalized() * speedAfterTurn;
                    }
                }
            }

            // Обновление позиции
            Position += Velocity * deltaTime;
        }

        // Принимает уже созданный шейдер и ID загруженной текстуры
        public static void InitializeGraphics(Shader fishShader, int fishTextureId)
        {
            if (s_graphicsInitialized) return;
            if (fishShader == null) throw new ArgumentNullException(nameof(fishShader));
            if (fishTextureId < 0) throw new ArgumentException("Invalid texture ID provided.", nameof(fishTextureId));

            s_shader = fishShader;
            s_textureId = fishTextureId;

            // 1. Создание VAO, VBO, EBO для квадрата
            s_vao = GL.GenVertexArray();
            s_vbo = GL.GenBuffer();
            s_ebo = GL.GenBuffer();

            GL.BindVertexArray(s_vao);

            // Загрузка данных вершин в VBO
            GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, s_quadVertices.Length * sizeof(float), s_quadVertices, BufferUsageHint.StaticDraw);

            // Загрузка данных индексов в EBO
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, s_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, s_quadIndices.Length * sizeof(uint), s_quadIndices, BufferUsageHint.StaticDraw);

            // 2. Настройка атрибутов вершин (согласно вашему вершинному шейдеру)
            int stride = 5 * sizeof(float); // 3 float для позиции + 2 float для текстурных коорд.

            // layout (location = 0) in vec3 aPosition;
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0); // 3 компонента, смещение 0

            // layout (location = 1) in vec2 aTexCoord;
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float)); // 2 компонента, смещение 3*float

            // 3. Получение локаций uniform-переменных из ВАШЕГО шейдера
            s_modelLoc = GL.GetUniformLocation(s_shader.shader_handle, "model");
            s_viewLoc = GL.GetUniformLocation(s_shader.shader_handle, "view");
            s_projectionLoc = GL.GetUniformLocation(s_shader.shader_handle, "projection");
            s_texture0Loc = GL.GetUniformLocation(s_shader.shader_handle, "texture0");

            if (s_modelLoc == -1 || s_viewLoc == -1 || s_projectionLoc == -1 || s_texture0Loc == -1)
            {
                Console.WriteLine("Warning: Could not find one or more uniform locations in the fish shader.");
                // Можно добавить более строгую проверку или выбросить исключение
            }

            // Отвязываем VAO (VBO и EBO остаются связанными с VAO)
            GL.BindVertexArray(0);
            // Отвязывать ArrayBuffer и ElementArrayBuffer после отвязки VAO необязательно, но можно для чистоты
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);


            s_graphicsInitialized = true;
            Console.WriteLine("Fish graphics initialized successfully using provided shader.");
        }

        public static void CleanupGraphics()
        {
            if (!s_graphicsInitialized) return;

            GL.DeleteBuffer(s_vbo);
            GL.DeleteBuffer(s_ebo);
            GL.DeleteVertexArray(s_vao);

            s_vbo = -1;
            s_ebo = -1;
            s_vao = -1;
            s_shader = null; // Обнуляем ссылку, но не удаляем сам шейдер здесь
            s_textureId = -1;
            s_graphicsInitialized = false;
            Console.WriteLine("Fish graphics cleaned up.");
        }

        // --- Метод отрисовки экземпляра рыбы ---
        // Принимает матрицы вида и проекции
        public void Draw(Matrix4 view, Matrix4 projection)
        {
            if (!s_graphicsInitialized || s_shader == null)
            {
                Console.WriteLine("Error: Fish graphics not initialized or shader missing.");
                return;
            }

            // 1. Вычисляем Model матрицу для этой рыбы
            Matrix4 model = Matrix4.Identity;
            model *= Matrix4.CreateScale(Size); // Масштабируем квадрат до нужного размера
            // model *= Matrix4.CreateRotationZ(Rotation); // Если нужно вращение
            model *= Matrix4.CreateTranslation(Position.X, Position.Y, 0.0f); // Перемещаем в позицию рыбы

            // 2. Активируем текстурный юнит 0 и привязываем текстуру рыбы
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, s_textureId);

            // 3. Используем шейдерную программу
            s_shader.Use(); // Используем ваш метод Use()

            // 4. Устанавливаем значения uniform-переменных
            GL.UniformMatrix4(s_modelLoc, false, ref model);
            GL.UniformMatrix4(s_viewLoc, false, ref view);
            GL.UniformMatrix4(s_projectionLoc, false, ref projection);
            GL.Uniform1(s_texture0Loc, 0); // Сообщаем шейдеру использовать текстурный юнит 0 для texture0

            // 5. Привязываем VAO (он помнит VBO, EBO и настройки атрибутов)
            GL.BindVertexArray(s_vao);

            // 6. Рисуем квадрат, используя индексы из EBO
            GL.DrawElements(PrimitiveType.Triangles, s_quadIndices.Length, DrawElementsType.UnsignedInt, 0);

            // 7. Отвязываем VAO (хорошая практика)
            GL.BindVertexArray(0);

            // 8. Отвязываем шейдер и текстуру (опционально, но может помочь избежать конфликтов)
            GL.UseProgram(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }

}
