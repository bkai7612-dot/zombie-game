using System.Collections.Generic;
using UnityEngine;

namespace UZS.UI
{
    /// <summary>Runtime-built IMGUI styles (dark, high-contrast, readable at night).</summary>
    public static class UISkin
    {
        public const float RefHeight = 1080f;

        public static readonly Color Accent = new Color(0.86f, 0.18f, 0.14f);
        public static readonly Color Alien = new Color(0.35f, 1f, 0.6f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        public static readonly Color Dim = new Color(0.75f, 0.75f, 0.75f);

        private static readonly Dictionary<Color, Texture2D> Textures = new Dictionary<Color, Texture2D>();
        private static bool built;

        public static GUIStyle Title, Subtitle, Label, Small, Button, BigButton, Panel, Field, Center, Prompt, Huge, RoundNumber, Toggle;

        public static float Scale => Mathf.Max(0.5f, Screen.height / RefHeight);
        public static float VirtualWidth => Screen.width / Scale;
        public static float VirtualHeight => RefHeight;

        public static Texture2D Tex(Color c)
        {
            if (Textures.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            Textures[c] = t;
            return t;
        }

        public static void Begin()
        {
            if (!built) Build();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        }

        private static void Build()
        {
            built = true;
            Title = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Title.normal.textColor = Accent;
            Subtitle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            Subtitle.normal.textColor = Color.white;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, richText = true };
            Label.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            Small = new GUIStyle(Label) { fontSize = 19 };
            Small.normal.textColor = Dim;
            Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Prompt = new GUIStyle(Label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Huge = new GUIStyle(Label) { fontSize = 64, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            RoundNumber = new GUIStyle(Label) { fontSize = 80, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            RoundNumber.normal.textColor = Accent;

            Button = new GUIStyle(GUI.skin.button) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Button.normal.background = Tex(new Color(0.12f, 0.12f, 0.13f, 0.92f));
            Button.hover.background = Tex(new Color(0.45f, 0.08f, 0.06f, 0.95f));
            Button.active.background = Tex(new Color(0.65f, 0.12f, 0.08f, 1f));
            Button.normal.textColor = Color.white;
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
            BigButton = new GUIStyle(Button) { fontSize = 32 };

            Toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 22 };
            Toggle.normal.textColor = Color.white;
            Toggle.onNormal.textColor = Color.white;

            Panel = new GUIStyle(GUI.skin.box);
            Panel.normal.background = Tex(new Color(0.03f, 0.035f, 0.045f, 0.88f));
            Panel.padding = new RectOffset(24, 24, 20, 20);

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
            Field.normal.background = Tex(new Color(0.18f, 0.18f, 0.2f, 1f));
            Field.focused.background = Tex(new Color(0.24f, 0.24f, 0.27f, 1f));
            Field.normal.textColor = Color.white;
            Field.focused.textColor = Color.white;
            Field.padding = new RectOffset(10, 10, 6, 6);
        }

        public static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Tex(c));

        public static void Bar(Rect r, float t, Color fill, Color back)
        {
            Fill(r, back);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), fill);
        }

        public static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.85f * color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }
    }
}
