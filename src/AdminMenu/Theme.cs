using System;
using MoonCleaners;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// IMGUI styles in the look of darkharasho's other admin menus: near-black window, slate grey buttons with a thin
    /// gold outline, and off-white text. Moon Sweepers has no UI Toolkit theme to borrow, so the menu is IMGUI, like the
    /// game's own crew notices. Build only inside OnGUI (GUI.skin isn't available elsewhere).
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Window = Rgb(0x17, 0x17, 0x17);
        public static readonly Color WindowBorder = Rgb(0xB9, 0xB9, 0xB9);
        public static readonly Color Surface = Rgb(0x22, 0x24, 0x29);
        public static readonly Color Control = Rgb(0x37, 0x3B, 0x45);
        public static readonly Color ControlHover = Rgb(0x46, 0x4B, 0x57);
        public static readonly Color Line = Rgb(0x55, 0x5A, 0x64);
        public static readonly Color Gold = Rgb(0xF0, 0xC8, 0x5E);
        public static readonly Color Text = Rgb(0xF2, 0xF2, 0xF2);
        public static readonly Color Muted = Rgb(0xA9, 0xAE, 0xB7);
        public static readonly Color Error = new Color(1f, 0.45f, 0.4f);

        public static GUIStyle WindowStyle, RowStyle, Title, Heading, Body, MutedText, ErrorText, Button, DangerButton, Toast;
        public static Texture2D Dim;

        /// <summary>Recreates everything if a scene load destroyed the textures.</summary>
        public static void Ensure()
        {
            if (WindowStyle != null && Dim != null && WindowStyle.normal.background != null)
                return;

            Font font = null;
            try
            {
                font = MoonFonts.Legacy;
            }
            catch (Exception)
            {
                // The game's font is a nicety; IMGUI's default works.
            }

            Dim = Solid(new Color(0f, 0f, 0f, 0.7f));
            WindowStyle = new GUIStyle { normal = { background = Bordered(Window, WindowBorder, 3) }, border = Edges(3), padding = Edges(24) };
            RowStyle = new GUIStyle { normal = { background = Bordered(Surface, Line, 1) }, border = Edges(1), padding = new RectOffset(14, 10, 8, 8), margin = new RectOffset(0, 0, 0, 6) };

            Title = MakeLabel(font, 34, Theme.Text, FontStyle.Bold);
            Heading = MakeLabel(font, 22, Gold, FontStyle.Bold);
            Heading.margin = new RectOffset(0, 0, 14, 8);
            Body = MakeLabel(font, 19, Theme.Text, FontStyle.Normal);
            Body.alignment = TextAnchor.MiddleLeft;
            MutedText = MakeLabel(font, 16, Muted, FontStyle.Normal);
            MutedText.wordWrap = true;
            ErrorText = MakeLabel(font, 16, Error, FontStyle.Normal);
            ErrorText.wordWrap = true;

            Button = ButtonStyle(font, Gold);
            DangerButton = ButtonStyle(font, Error);

            Toast = MakeLabel(font, 20, Gold, FontStyle.Bold);
            Toast.alignment = TextAnchor.MiddleCenter;
            Toast.normal.background = Bordered(Window, Gold, 2);
            Toast.border = Edges(2);
            Toast.padding = new RectOffset(18, 18, 10, 10);
        }

        private static GUIStyle ButtonStyle(Font font, Color outline)
        {
            var style = MakeLabel(font, 17, Theme.Text, FontStyle.Bold);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.background = Bordered(Control, outline, 2);
            style.hover.background = style.active.background = style.focused.background = Bordered(ControlHover, outline, 2);
            style.hover.textColor = style.active.textColor = style.focused.textColor = Theme.Text;
            style.border = Edges(2);
            style.padding = new RectOffset(14, 14, 6, 6);
            style.margin = new RectOffset(6, 0, 0, 0);
            return style;
        }

        private static GUIStyle MakeLabel(Font font, int size, Color color, FontStyle fontStyle)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = false, richText = false };
            if (font != null)
                style.font = font;
            style.normal.textColor = color;
            return style;
        }

        private static RectOffset Edges(int px) => new RectOffset(px, px, px, px);

        private static Texture2D Solid(Color color)
        {
            var texture = NewTexture(1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>A 9-sliceable texture: <paramref name="fill"/> inside a <paramref name="px"/>-wide border.</summary>
        private static Texture2D Bordered(Color fill, Color border, int px)
        {
            var size = px * 2 + 2;
            var texture = NewTexture(size);
            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < size; y++)
                {
                    var edge = x < px || y < px || x >= size - px || y >= size - px;
                    texture.SetPixel(x, y, edge ? border : fill);
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
