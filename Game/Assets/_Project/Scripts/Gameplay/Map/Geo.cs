using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Primitive-based geometry helpers used to assemble the placeholder map and props.</summary>
    public static class Geo
    {
        public static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        public static GameObject Group(Transform parent, string name, Vector3 position = default, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>Axis-aligned box given its centre and full size (local to parent).</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Box spanning from min to max corners (local to parent).</summary>
        public static GameObject Span(Transform parent, string name, Vector3 min, Vector3 max, Material mat, bool collider = true)
        {
            var size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z));
            return Box(parent, name, (min + max) * 0.5f, size, mat, collider);
        }

        public static GameObject Rotated(Transform parent, string name, Vector3 center, Vector3 size, Vector3 euler, Material mat, bool collider = true)
        {
            var go = Box(parent, name, center, size, mat, collider);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material mat, bool collider = true, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (!collider) Object.DestroyImmediate(col);
            return go;
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 center, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            return l;
        }

        /// <summary>
        /// A straight wall from a to b (XZ, local) with optional rectangular openings, each given as
        /// (distance along wall from a to opening centre, width, bottom, top).
        /// </summary>
        public static void Wall(Transform parent, string name, Vector2 a, Vector2 b, float baseY, float height, float thickness,
            Material mat, params Vector4[] openings)
        {
            var dir = b - a;
            float length = dir.magnitude;
            if (length < 0.01f) return;
            var unit = dir / length;
            float yaw = Mathf.Atan2(unit.x, unit.y) * Mathf.Rad2Deg;
            var root = Group(parent, name, new Vector3(a.x, baseY, a.y), yaw);

            // Openings sorted along the wall; segments are emitted between them.
            System.Array.Sort(openings, (p, q) => p.x.CompareTo(q.x));
            float cursor = 0f;
            foreach (var o in openings)
            {
                float start = Mathf.Clamp(o.x - o.y * 0.5f, 0f, length);
                float end = Mathf.Clamp(o.x + o.y * 0.5f, 0f, length);
                if (start > cursor) Seg(root.transform, cursor, start, 0f, height, thickness, mat);
                if (o.z > 0.01f) Seg(root.transform, start, end, 0f, o.z, thickness, mat);
                if (o.w < height - 0.01f) Seg(root.transform, start, end, o.w, height, thickness, mat);
                cursor = Mathf.Max(cursor, end);
            }
            if (cursor < length) Seg(root.transform, cursor, length, 0f, height, thickness, mat);
        }

        private static void Seg(Transform root, float from, float to, float y0, float y1, float thickness, Material mat)
        {
            Box(root, "seg", new Vector3(0f, (y0 + y1) * 0.5f, (from + to) * 0.5f), new Vector3(thickness, y1 - y0, to - from), mat);
        }

        public static Vector4 Door(float along, float width, float height = 2.3f) => new Vector4(along, width, 0f, height);

        public static Vector4 Window(float along, float width = 1.4f, float sill = 1.0f, float top = 2.2f) => new Vector4(along, width, sill, top);

        public static TextMesh Label(Transform parent, string text, Vector3 pos, float yaw, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            var font = BuiltinFont();
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = size;
            tm.fontSize = 64;
            tm.color = color;
            return tm;
        }

        private static Font cachedFont;

        public static Font BuiltinFont()
        {
            if (cachedFont != null) return cachedFont;
            // Unity 2022.2+ ships LegacyRuntime.ttf; older editors use Arial.ttf.
            try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (System.ArgumentException) { cachedFont = null; }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (System.ArgumentException) { cachedFont = null; }
            }
            return cachedFont;
        }
    }
}
