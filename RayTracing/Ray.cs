using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace RayTracing
{
    public class Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public Vector3 At(float t)
        {
            return Origin + Direction * t;
        }
    }
}
