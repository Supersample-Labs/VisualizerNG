using OpenTK.Graphics.OpenGL4;

namespace Visualizer.App.Rendering;

/// <summary>Screen-space CRT overlay shared by every visualizer.</summary>
public sealed class CrtEffects : IDisposable
{
    private int _program;
    private int _vao;
    private int _snowLocation;
    private int _scanLocation;
    private int _timeLocation;
    private int _heightLocation;
    private double _time;

    public void Render(ShaderManager shaders, float deltaTime, int height, float snow, float scan)
    {
        _time = (_time + Math.Clamp(deltaTime, 0, 0.1f)) % 3600;
        snow = float.IsFinite(snow) ? Math.Clamp(snow, 0, 1) : 0;
        scan = float.IsFinite(scan) ? Math.Clamp(scan, 0, 1) : 0;
        if (snow == 0 && scan == 0) return;
        if (_program == 0)
        {
            _program = shaders.CreateProgram(VertexShader, FragmentShader);
            _vao = GL.GenVertexArray();
            _snowLocation = GL.GetUniformLocation(_program, "uSnow");
            _scanLocation = GL.GetUniformLocation(_program, "uScan");
            _timeLocation = GL.GetUniformLocation(_program, "uTime");
            _heightLocation = GL.GetUniformLocation(_program, "uHeight");
        }
        GL.UseProgram(_program);
        GL.Uniform1(_snowLocation, snow);
        GL.Uniform1(_scanLocation, scan);
        GL.Uniform1(_timeLocation, (float)_time);
        GL.Uniform1(_heightLocation, (float)height);
        GL.BindVertexArray(_vao);
        // Draw into the currently bound framebuffer (including GLWpfControl's FBO).
        // Standard alpha blending composes static and dark scan lines over the scene.
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    public void Dispose()
    {
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        _vao = 0;
        // ShaderManager owns the program.
    }

    private const string VertexShader = """
        #version 330 core
        void main()
        {
            vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        uniform float uSnow;
        uniform float uScan;
        uniform float uTime;
        uniform float uHeight;
        out vec4 FragColor;

        float noise(uvec3 seed)
        {
            uint h = seed.x * 1597334677u ^ seed.y * 3812015801u ^ seed.z * 2798796415u;
            h = (h ^ (h >> 16)) * 2246822519u;
            h = (h ^ (h >> 13)) * 3266489917u;
            return float(h ^ (h >> 16)) / 4294967295.0;
        }

        void main()
        {
            float field = floor(uTime * 60.0);
            float grain = noise(uvec3(uvec2(floor(gl_FragCoord.xy)), uint(field)));
            float snowAlpha = uSnow * 0.65;
            // Two-pixel rows alternate at 60 fields/second, independent of FPS.
            float line = mod(floor(gl_FragCoord.y / 2.0) + mod(field, 2.0), 2.0);
            float y = gl_FragCoord.y / uHeight;
            // Repeat three equally spaced bands; increasing phase moves them down.
            float distanceToBand = abs(fract((y + uTime * 0.18) * 3.0) - 0.5) / 3.0;
            float band = 1.0 - smoothstep(0.015, 0.09, distanceToBand);
            float dim = uScan * (0.55 * line + 0.2 * band);
            float alpha = 1.0 - (1.0 - snowAlpha) * (1.0 - dim);
            // A faint phosphor glow makes the scan bands visible over black,
            // even with snow disabled. Keep the original dark interlace lines.
            float glow = uScan * band * 0.11;
            vec3 premultiplied = vec3(grain * snowAlpha * (1.0 - dim));
            premultiplied = premultiplied * (1.0 - glow) + vec3(0.45, 0.65, 0.72) * glow;
            alpha = 1.0 - (1.0 - alpha) * (1.0 - glow);
            vec3 color = premultiplied / max(alpha, 0.00001);
            FragColor = vec4(color, alpha);
        }
        """;
}
