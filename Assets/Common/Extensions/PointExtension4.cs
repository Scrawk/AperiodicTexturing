using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Common.Core.Numerics;

namespace UnityEngine
{

    public static class PointExtension4
    {
        public static Point2f ToPoint2f(this Vector4 v)
        {
            return new Point2f(v.x, v.y);
        }

        public static Point2d ToPoint2d(this Vector4 v)
        {
            return new Point2d(v.x, v.y);
        }

        public static Point3f ToPoint3f(this Vector4 v)
        {
            return new Point3f(v.x, v.y, v.z);
        }

        public static Point3d ToPoint3d(this Vector4 v)
        {
            return new Point3d(v.x, v.y, v.z);
        }

        public static Point4f ToPoint4f(this Vector4 v)
        {
            return new Point4f(v.x, v.y, v.z, v.w);
        }

        public static Point4d ToPoint4d(this Vector4 v)
        {
            return new Point4d(v.x, v.y, v.z, v.w);
        }

        public static Vector2 ToVector2(this Point4f v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector3 ToVector3(this Point4f v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector4 ToVector4(this Point4f v)
        {
            return new Vector4(v.x, v.y, v.z, v.w);
        }

        public static Vector2 ToVector2(this Point4d v)
        {
            return new Vector2((float)v.x, (float)v.y);
        }

        public static Vector3 ToVector3(this Point4d v)
        {
            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }

        public static Vector4 ToVector4(this Point4d v)
        {
            return new Vector4((float)v.x, (float)v.y, (float)v.z, (float)v.w);
        }

    }

}
