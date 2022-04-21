using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Common.Core.Numerics;

namespace UnityEngine
{

    public static class PointExtension3
    {

        public static Point2f ToPoint2f(this Vector3 v)
        {
            return new Point2f(v.x, v.y);
        }

        public static Point2d ToPoint2d(this Vector3 v)
        {
            return new Point2d(v.x, v.y);
        }

        public static Point3f ToPoint3f(this Vector3 v)
        {
            return new Point3f(v.x, v.y, v.z);
        }

        public static Point3d ToPoint3d(this Vector3 v)
        {
            return new Point3d(v.x, v.y, v.z);
        }

        public static Vector2 ToVector2(this Point3f v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector3 ToVector3(this Point3f v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector4 ToVector4(this Point3f v)
        {
            return new Vector4(v.x, v.y, v.z, 1);
        }

        public static Vector2 ToVector2(this Point3d v)
        {
            return new Vector2((float)v.x, (float)v.y);
        }

        public static Vector3 ToVector3(this Point3d v)
        {
            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }

        public static Vector4 ToVector4(this Point3d v)
        {
            return new Vector4((float)v.x, (float)v.y, (float)v.z, 1);
        }

    }

}
