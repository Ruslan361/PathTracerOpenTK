using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace PathTracerOpenTK
{
    public class Shader
    {
        public int ProgramId { get; private set; }
        private bool disposed = false;

        public Shader(string vertexSource, string fragmentSource)
        {
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            ProgramId = GL.CreateProgram();
            
            GL.AttachShader(ProgramId, vertexShader);
            GL.AttachShader(ProgramId, fragmentShader);
            
            GL.LinkProgram(ProgramId);
            
            GL.GetProgram(ProgramId, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(ProgramId);
                Console.WriteLine($"ERROR::SHADER::PROGRAM::LINKING_FAILED\n{infoLog}");
            }

            // Detach and delete the shaders as they're no longer needed
            GL.DetachShader(ProgramId, vertexShader);
            GL.DetachShader(ProgramId, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"ERROR::SHADER::{type}::COMPILATION_FAILED\n{infoLog}");
            }

            return shader;
        }

        public void Use()
        {
            GL.UseProgram(ProgramId);
        }

        // Utility uniform functions
        public void SetInt(string name, int value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.Uniform1(location, value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.Uniform2(location, value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.Uniform3(location, value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.Uniform4(location, value);
        }

        public void SetMatrix4(string name, Matrix4 value)
        {
            int location = GL.GetUniformLocation(ProgramId, name);
            GL.UniformMatrix4(location, false, ref value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                GL.DeleteProgram(ProgramId);
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Shader()
        {
            Dispose(false);
        }
    }
}
