using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{

    public static class GameObjectExtension
    {
        public static void FindObjectsOfAnyType<T>(this GameObject go, List<T> list) where T : class
        {
            foreach (var monoBehaviour in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (monoBehaviour is T)
                    list.Add(monoBehaviour as T);
            }
        }

        public static T FindObjectOfAnyType<T>(this GameObject go) where T : class
        {
            foreach (var monoBehaviour in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (monoBehaviour is T)
                    return monoBehaviour as T;
            }

            return null;
        }

        public static bool TryFindWorldBounds(this GameObject go, out Bounds bounds)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }
                
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
                return true;
            }

            bounds = new Bounds();
            return false;
        }
    }

}
