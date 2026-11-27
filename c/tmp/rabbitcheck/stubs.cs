// Minimal UnityEngine surface for out-of-Unity verification harness.
// Reproduces Unity semantics for identity-rotation/identity-scale transforms
// (position = cumulative localPosition; InverseTransformPoint = world - pos).
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public static class Mathf
    {
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
    }

    public static class Debug
    {
        public static void Log(object msg) => Console.WriteLine("[LOG] " + msg);
        public static void LogWarning(object msg) => Console.WriteLine("[WARN] " + msg);
    }

    public class Transform : Component, IEnumerable<Transform>
    {
        public new string name;
        public new GameObject gameObject;
        internal Transform _parent;
        internal List<Transform> _children = new List<Transform>();
        public Vector3 localPosition;

        public Transform parent => _parent;
        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];

        // Identity rotation/scale chain: world position = sum of local positions up the chain.
        public Vector3 position
        {
            get
            {
                var v = localPosition;
                var p = _parent;
                while (p != null) { v += p.localPosition; p = p._parent; }
                return v;
            }
        }

        public void SetParent(Transform parent, bool worldPositionStays)
        {
            _parent?._children.Remove(this);
            _parent = parent;
            parent?._children.Add(this);
        }

        public Vector3 InverseTransformPoint(Vector3 world) => world - position;

        public IEnumerator<Transform> GetEnumerator() => _children.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();
    }

    public class Component
    {
        public GameObject gameObject;
        public Transform transform;
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var list = new List<T>();
            Collect(gameObject.transform, list);
            return list.ToArray();
        }
        static void Collect<T>(Transform t, List<T> list) where T : Component
        {
            list.Add((T)(object)t);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), list);
        }
    }

    public class Animator : Component { }

    public class GameObject
    {
        public string name;
        public Transform transform;
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { name = name, gameObject = this };
            transform.gameObject = this;
        }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this, transform = transform };
            return c;
        }
    }
}