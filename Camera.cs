using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace PathTracerOpenTK
{
    public class Camera
    {
        // Camera properties
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Right { get; set; }
        public float Fov { get; set; }
        public float AspectRatio { get; set; }
        public float MoveSpeed { get; set; }
        public float RotateSpeed { get; set; }

        // Euler angles
        public float Yaw { get; set; } = -90.0f;
        public float Pitch { get; set; } = 0.0f;

        private Vector3 worldUp = Vector3.UnitY;

        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            Direction = -Vector3.UnitZ;
            Up = Vector3.UnitY;
            Right = Vector3.Normalize(Vector3.Cross(Direction, Up));
            Fov = 60.0f;
            MoveSpeed = 5.0f;
            RotateSpeed = 5.0f;
        }

        public void UpdateDirectionVectors()
        {
            // Calculate the direction vector from yaw and pitch
            Direction = new Vector3(
                (float)Math.Cos(MathHelper.DegreesToRadians(Yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(Pitch)),
                (float)Math.Sin(MathHelper.DegreesToRadians(Pitch)),
                (float)Math.Sin(MathHelper.DegreesToRadians(Yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(Pitch))
            );
            
            Direction = Vector3.Normalize(Direction);
            
            // Re-calculate the Right and Up vector
            Right = Vector3.Normalize(Vector3.Cross(Direction, worldUp));
            Up = Vector3.Normalize(Vector3.Cross(Right, Direction));
        }

        public float GetHorizontalAngle()
        {
            return Yaw;
        }

        public float GetVerticalAngle()
        {
            return Pitch;
        }

        // Get the direction the camera is looking
        public Vector3 GetDirection()
        {
            return Direction;
        }

        // Get the up vector of the camera
        public Vector3 GetDirectionUp()
        {
            return Up;
        }
    }
}
