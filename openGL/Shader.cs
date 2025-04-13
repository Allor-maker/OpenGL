using OpenTK.Graphics.OpenGL4; // Нужен для GL функций работы с шейдерами
using System;                   // Нужен для Console, Exception
using System.IO;                // Нужен для File.ReadAllText

namespace openGL
{
    public class Shader
    {
        public int shader_handle;

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

}
