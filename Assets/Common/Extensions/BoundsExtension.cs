using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Common.Core.Numerics;
using Common.Core.Shapes;

namespace UnityEngine
{

    public static class BoundsExtension 
    {

        public static Box2f ToBox2f(this Bounds bounds)
        {
            return new Box2f(bounds.min.ToPoint2f(), bounds.max.ToPoint2f());
        }

        public static Box3f ToBox3f(this Bounds bounds)
        {
            return new Box3f(bounds.min.ToPoint3f(), bounds.max.ToPoint3f());
        }

        public static Bounds ToBounds(this Box2f box)
        {
            var bounds = new Bounds();
            bounds.min = box.Min.ToVector2();
            bounds.max = box.Max.ToVector2();
            return bounds;
        }

        public static Bounds ToBounds(this Box3f box)
        {
            var bounds = new Bounds();
            bounds.min = box.Min.ToVector3();
            bounds.max = box.Max.ToVector3();
            return bounds;
        }

    }

}
