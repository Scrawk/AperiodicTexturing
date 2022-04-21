using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Common.Core.Numerics;

namespace UnityEngine
{

    public static class PointExtension2
    {
        public static Point2i ToPoint2i(this Vector2 v)
        {
            return new Point2i((int)v.x, (int)v.y);
        }

        public static Point2f ToPoint2f(this Vector2 v)
        {
            return new Point2f(v.x, v.y);
        }

        public static Point2d ToPoint2d(this Vector2 v)
        {
            return new Point2d(v.x, v.y);
        }

        public static Point3f ToPoint3f(this Vector2 v)
        {
            return new Point3f(v.x, v.y, 0);
        }

        public static Point3d ToPoint3d(this Vector2 v)
        {
            return new Point3d(v.x, v.y, 0);
        }

        public static Point4f ToPoint4f(this Vector2 v)
        {
            return new Point4f(v.x, v.y, 0, 1);
        }

        public static Point4d ToPoint4d(this Vector2 v)
        {
            return new Point4d(v.x, v.y, 0, 1);
        }

        public static Point2i ToPoint2i(this Vector2Int v)
        {
            return new Point2i((int)v.x, (int)v.y);
        }

        public static Point2f ToPoint2f(this Vector2Int v)
        {
            return new Point2f(v.x, v.y);
        }

        public static Point2d ToPoint2d(this Vector2Int v)
        {
            return new Point2d(v.x, v.y);
        }

        public static Point3f ToPoint3f(this Vector2Int v)
        {
            return new Point3f(v.x, v.y, 0);
        }

        public static Point3d ToPoint3d(this Vector2Int v)
        {
            return new Point3d(v.x, v.y, 0);
        }

        public static Point4f ToPoint4f(this Vector2Int v)
        {
            return new Point4f(v.x, v.y, 0, 1);
        }

        public static Point4d ToPoint4d(this Vector2Int v)
        {
            return new Point4d(v.x, v.y, 0, 1);
        }

        public static Vector2Int ToVector2Int(this Point2i v)
        {
            return new Vector2Int(v.x, v.y);
        }

        public static Vector2 ToVector2(this Point2i v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector3 ToVector3(this Point2i v)
        {
            return new Vector3(v.x, v.y, 0);
        }

        public static Vector4 ToVector4(this Point2i v)
        {
            return new Vector4(v.x, v.y, 0, 1);
        }

        public static Vector2 ToVector2(this Point2f v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector3 ToVector3(this Point2f v)
        {
            return new Vector3(v.x, v.y, 0);
        }

        public static Vector4 ToVector4(this Point2f v)
        {
            return new Vector4(v.x, v.y, 0, 1);
        }

        public static Vector2 ToVector2(this Point2d v)
        {
            return new Vector2((float)v.x, (float)v.y);
        }

        public static Vector3 ToVector3(this Point2d v)
        {
            return new Vector3((float)v.x, (float)v.y, 0);
        }

        public static Vector4 ToVector4(this Point2d v)
        {
            return new Vector4((float)v.x, (float)v.y, 0, 1);
        }

        public static Vector4 xy01(this Vector2 v)
        {
            return new Vector4(v.x, v.y, 0, 1);
        }

        public static Vector4 x0y1(this Vector2 v)
        {
            return new Vector4(v.x, 0, v.y, 1);
        }

    }

}
