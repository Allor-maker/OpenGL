using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework; // Для Keys

namespace openGL
{
    public class Camera
    {
        public Vector3 Position;
        public Vector3 Front = -Vector3.UnitZ;
        public Vector3 Up = Vector3.UnitY;
        public Vector3 Right = Vector3.UnitX;

        // Углы Эйлера
        public float Pitch;
        public float Yaw = -MathHelper.PiOver2; // Смотрим вдоль -Z

        // Параметры камеры
        public float AspectRatio = 1.0f;
        public float Fov = MathHelper.PiOver2; // 90 градусов

        public float Sensitivity = 0.002f;
        public float Speed = 1.5f;

        private bool _firstMove = true;
        private Vector2 _lastMousePos;

        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            UpdateVectors();
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(Fov, AspectRatio, 0.1f, 100.0f);
        }

        private void UpdateVectors()
        {
            // Вычисляем новый вектор Front
            Front.X = MathF.Cos(Pitch) * MathF.Cos(Yaw);
            Front.Y = MathF.Sin(Pitch);
            Front.Z = MathF.Cos(Pitch) * MathF.Sin(Yaw);
            Front = Vector3.Normalize(Front);

            // Пересчитываем Right и Up
            Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY)); // Используем глобальный Vector3.UnitY для избежания крена
            Up = Vector3.Normalize(Vector3.Cross(Right, Front));
        }

        public void HandleKeyboardInputOnly(KeyboardState keyboard, float deltaTime)
        {
            float velocity = Speed * deltaTime;
            if (keyboard.IsKeyDown(Keys.W)) Position += Front * velocity;
            if (keyboard.IsKeyDown(Keys.S)) Position -= Front * velocity;
            if (keyboard.IsKeyDown(Keys.A)) Position -= Right * velocity;
            if (keyboard.IsKeyDown(Keys.D)) Position += Right * velocity;
            if (keyboard.IsKeyDown(Keys.LeftShift)) Position += Up * velocity; // Или Vector3.UnitY * velocity
            if (keyboard.IsKeyDown(Keys.LeftControl)) Position -= Up * velocity; // Или Vector3.UnitY * velocity
        }
        public void HandleInput(KeyboardState keyboard, MouseState mouse, float deltaTime)
        {
            // Движение WASD
            float velocity = Speed * deltaTime;
            if (keyboard.IsKeyDown(Keys.W)) Position += Front * velocity;
            if (keyboard.IsKeyDown(Keys.S)) Position -= Front * velocity;
            if (keyboard.IsKeyDown(Keys.A)) Position -= Right * velocity;
            if (keyboard.IsKeyDown(Keys.D)) Position += Right * velocity;
            if (keyboard.IsKeyDown(Keys.LeftShift)) Position += Up * velocity;
            if (keyboard.IsKeyDown(Keys.LeftControl)) Position -= Up * velocity;

            // Вращение мышью
            if (_firstMove) // Если это первое движение после ResetMouse или старта
            {
                _lastMousePos = mouse.Position;
                _firstMove = false;
            }
            else
            {
                float deltaX = mouse.X - _lastMousePos.X;
                float deltaY = mouse.Y - _lastMousePos.Y;
                // Обновляем _lastMousePos *перед* использованием дельты
                _lastMousePos = mouse.Position;

                Yaw += deltaX * Sensitivity;
                Pitch -= deltaY * Sensitivity; // Y инвертирован

                Pitch = MathHelper.Clamp(Pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

                UpdateVectors();
            }
        }
        public void SetPositionAndLookAt(Vector3 position, Vector3 target)
        {
            Position = position;
            Front = Vector3.Normalize(target - position);
            // Пересчитываем углы Yaw и Pitch на основе нового вектора Front
            // Это важно, чтобы UpdateVectors() работал корректно, если мы захотим
            // в будущем плавно переходить между режимами, а не телепортироваться.
            Pitch = MathF.Asin(Front.Y);
            Yaw = MathF.Atan2(Front.Z, Front.X); // Atan2(y, x)

            UpdateVectors(); // Пересчитать Right и Up на основе нового Front
            ResetMouse(Vector2.Zero); // Сбросить состояние мыши для предотвращения скачков
        }
        // Вызывается при смене фокуса окна или при первом запуске, чтобы сбросить delta
        public void ResetMouse(Vector2 currentMousePos)
        {
            _firstMove = true;
            _lastMousePos = currentMousePos;
        }
    }
}