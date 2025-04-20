using OpenTK.Mathematics;
using System.Globalization;

namespace openGL
{
    public static class ObjLoader
    {
        public struct VertexData
        {
            public Vector3 Position;
            public Vector2 TexCoord;
            public Vector3 Normal;
        }

        public static bool LoadObj(
            string path,
            out List<VertexData> outVertices,
            out List<uint> outIndices)
        {
            outVertices = new List<VertexData>();
            outIndices = new List<uint>();

            var temp_positions = new List<Vector3>();
            var temp_texCoords = new List<Vector2>();
            var temp_normals = new List<Vector3>();
            var vertexCache = new Dictionary<string, uint>(); // Для индексации

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;

                        string type = parts[0];

                        try // Добавим try-catch для парсинга чисел
                        {
                            switch (type)
                            {
                                case "v": // Вершина
                                    if (parts.Length >= 4)
                                    {
                                        temp_positions.Add(new Vector3(
                                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                                            float.Parse(parts[2], CultureInfo.InvariantCulture),
                                            float.Parse(parts[3], CultureInfo.InvariantCulture)));
                                    }
                                    break;
                                case "vt": // Текстурная координата
                                    if (parts.Length >= 3)
                                    {
                                        temp_texCoords.Add(new Vector2(
                                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                                            float.Parse(parts[2], CultureInfo.InvariantCulture)));
                                    }
                                    break;
                                case "vn": // Нормаль
                                    if (parts.Length >= 4)
                                    {
                                        temp_normals.Add(new Vector3(
                                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                                            float.Parse(parts[2], CultureInfo.InvariantCulture),
                                            float.Parse(parts[3], CultureInfo.InvariantCulture)));
                                    }
                                    break;
                                case "f": // Грань (поддерживаем только v/vt/vn)
                                    if (parts.Length >= 4) // Треугольник
                                    {
                                        for (int i = 1; i <= 3; i++) // Обрабатываем 3 вершины треугольника
                                        {
                                            string vertexKey = parts[i];
                                            if (vertexCache.TryGetValue(vertexKey, out uint index))
                                            {
                                                outIndices.Add(index);
                                            }
                                            else
                                            {
                                                string[] indices = vertexKey.Split('/');
                                                if (indices.Length >= 3) // Убедимся, что есть все 3 индекса
                                                {
                                                    int posIndex = int.Parse(indices[0]) - 1; // OBJ нумерация с 1
                                                    int texIndex = int.Parse(indices[1]) - 1;
                                                    int normIndex = int.Parse(indices[2]) - 1;

                                                    // Проверки на выход за пределы массивов
                                                    if (posIndex < 0 || posIndex >= temp_positions.Count ||
                                                        texIndex < 0 || texIndex >= temp_texCoords.Count ||
                                                        normIndex < 0 || normIndex >= temp_normals.Count)
                                                    {
                                                        Console.WriteLine($"Warning: Invalid index in face definition: {line}");
                                                        continue; // Пропускаем эту вершину грани
                                                    }


                                                    VertexData vertex = new VertexData
                                                    {
                                                        Position = temp_positions[posIndex],
                                                        TexCoord = temp_texCoords[texIndex],
                                                        Normal = temp_normals[normIndex]
                                                    };

                                                    outVertices.Add(vertex);
                                                    uint newIndex = (uint)(outVertices.Count - 1);
                                                    outIndices.Add(newIndex);
                                                    vertexCache.Add(vertexKey, newIndex);
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"Warning: Face format not supported (expected v/vt/vn): {line}");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Warning: Only triangular faces supported: {line}");
                                    }
                                    break;
                            }
                        }
                        catch (FormatException ex)
                        {
                            Console.WriteLine($"Error parsing line: {line}. Reason: {ex.Message}");
                            return false; // Ошибка парсинга числа
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                            Console.WriteLine($"Error parsing line (index missing?): {line}. Reason: {ex.Message}");
                            return false;
                        }
                    }
                }
                Console.WriteLine($"Loaded model {path}: {outVertices.Count} vertices, {outIndices.Count / 3} triangles.");
                return true;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Error: Model file not found at {path}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading model {path}: {ex.Message}");
                return false;
            }
        }
    }
}