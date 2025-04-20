#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec3 aNormal;

out vec2 vTexCoord;
out vec3 vNormal;
out vec3 vFragPos;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    vFragPos = vec3(model * vec4(aPosition, 1.0));
    vNormal = normalize(mat3(transpose(inverse(model))) * aNormal);
    vTexCoord = aTexCoord;
    gl_Position = projection * view * vec4(vFragPos, 1.0);
}