using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public abstract class VisualizerBase : IVisualizer
{
    protected int Program;
    protected int Vao;
    protected int Vbo;
    protected int ColorLocation;

    public abstract string Name { get; }
    public VisualizerPalette Palette { get; set; } = VisualizerPalette.All[0];

    public virtual void Initialize(ShaderManager shaders)
    {
        if (Program != 0)
        {
            return;
        }

        Program = shaders.CreateProgram(VertexShader, FragmentShader);
        ColorLocation = GL.GetUniformLocation(Program, "uColor");
        Vao = GL.GenVertexArray();
        Vbo = GL.GenBuffer();
        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
    }

    public void SetRotation(float degrees, int width, int height, bool threeD = false, float tiltDegrees = 0, float turnDegrees = 0)
    {
        GL.UseProgram(Program);
        GL.Uniform1(GL.GetUniformLocation(Program, "uFit"), this is CircularOscilloscopeVisualizer or RadialSpectrumVisualizer ? 0 : 1);
        GL.Uniform1(GL.GetUniformLocation(Program, "uAngle"), -degrees * MathF.PI / 180f);
        GL.Uniform1(GL.GetUniformLocation(Program, "uThreeD"), threeD ? 1 : 0);
        GL.Uniform2(GL.GetUniformLocation(Program, "uTiltTurn"), tiltDegrees * MathF.PI / 180f, turnDegrees * MathF.PI / 180f);
        GL.Uniform2(GL.GetUniformLocation(Program, "uViewport"), (float)width, (float)height);
    }

    public abstract void Update(AudioFeatures audio, float deltaTime);
    public abstract void Render(RenderContext context);
    public virtual void Reset() { }

    protected void DrawPrimary(float[] vertices, int floatCount, PrimitiveType primitiveType, float alpha = 0.95f)
        => DrawVertices(vertices, floatCount, primitiveType, Palette.Primary.X, Palette.Primary.Y, Palette.Primary.Z, alpha);

    protected void DrawAccent(float[] vertices, int floatCount, PrimitiveType primitiveType, float alpha = 0.95f)
        => DrawVertices(vertices, floatCount, primitiveType, Palette.Accent.X, Palette.Accent.Y, Palette.Accent.Z, alpha);

    protected void DrawVertices(float[] vertices, int floatCount, PrimitiveType primitiveType, float r, float g, float b, float a)
    {
        GL.UseProgram(Program);
        GL.Uniform4(ColorLocation, r, g, b, a);
        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, floatCount * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        GL.DrawArrays(primitiveType, 0, floatCount / 2);
    }

    public virtual void Dispose()
    {
        if (Vbo != 0)
        {
            GL.DeleteBuffer(Vbo);
        }

        if (Vao != 0)
        {
            GL.DeleteVertexArray(Vao);
        }
    }

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        uniform float uAngle;
        uniform int uThreeD;
        uniform vec2 uTiltTurn;
        uniform int uFit;
        uniform vec2 uViewport;
        void main()
        {
            float c = cos(uAngle), s = sin(uAngle);
            if (uThreeD != 0)
            {
                // Rotate in physical viewport units, then project from a camera
                // four radii away. Bound the shape inside a sphere to avoid clipping.
                float size = min(uViewport.x, uViewport.y);
                float radius = uFit == 0 ? size : length(uViewport);
                vec3 q = vec3(aPosition * uViewport / radius, 0.0);
                float cx = cos(uTiltTurn.x), sx = sin(uTiltTurn.x);
                float cy = cos(uTiltTurn.y), sy = sin(uTiltTurn.y);
                q = vec3(q.x, cx * q.y - sx * q.z, sx * q.y + cx * q.z);
                q = vec3(cy * q.x + sy * q.z, q.y, -sy * q.x + cy * q.z);
                q.xy = vec2(c * q.x - s * q.y, s * q.x + c * q.y);
                float w = 1.0 - q.z / 4.0;
                gl_Position = vec4(q.xy * size / uViewport * 0.94, 0.0, w);
                return;
            }
            vec2 p = aPosition * uViewport;
            p = vec2(c * p.x - s * p.y, s * p.x + c * p.y);
            // Fit the rotated viewport so linear modes remain visible too.
            vec2 bounds = vec2(abs(c) * uViewport.x + abs(s) * uViewport.y,
                               abs(s) * uViewport.x + abs(c) * uViewport.y);
            float fit = uFit == 0 ? 1.0 : min(1.0, min(uViewport.x / bounds.x, uViewport.y / bounds.y));
            gl_Position = vec4(p / uViewport * fit, 0.0, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        uniform vec4 uColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = uColor;
        }
        """;
}
