using OpenTK.Graphics.OpenGL4;

namespace Visualizer.App.Rendering;

public sealed class ShaderManager : IDisposable
{
    private readonly List<int> _programs = [];

    public int CreateProgram(string vertexSource, string fragmentSource)
    {
        var vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        var fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);
        var program = GL.CreateProgram();
        GL.AttachShader(program, vertex);
        GL.AttachShader(program, fragment);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var status);
        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);

        if (status == 0)
        {
            var log = GL.GetProgramInfoLog(program);
            GL.DeleteProgram(program);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        _programs.Add(program);
        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        var shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);
        if (status != 0)
        {
            return shader;
        }

        var log = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException($"{type} compile failed: {log}");
    }

    public void Dispose()
    {
        foreach (var program in _programs)
        {
            GL.DeleteProgram(program);
        }

        _programs.Clear();
    }
}
