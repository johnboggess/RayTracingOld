using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
namespace RayTracing
{
    public class ProjectionMatrix
    {
        Matrix4x4 projection;
        Matrix4x4 inverse;

        public Matrix4x4 Projection
        {
            get { return projection; }
            set
            {
                projection = value;
                Matrix4x4.Invert(projection, out inverse);
            }
        }

        public Vector2 Project(Vector3 viewSpacePos)
        {
            Vector4 result = new Vector4(viewSpacePos, 1);
            result = Vector4.Transform(result, projection);
            result /= result.W;
            return new Vector2(result.X, result.Y);
        }

        public Vector3 ToViewSpace(Vector2 screenPos)
        {
            Vector4 result = new Vector4(screenPos.X, screenPos.Y, 0, 1);
            result = Vector4.Transform(result, inverse);
            result /= result.W;
            return new Vector3(result.X, result.Y, result.Z);
        }
    }
}
