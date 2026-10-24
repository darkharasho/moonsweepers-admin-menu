using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdminMenu
{
    /// <summary>
    /// Mouse and keyboard state for the menu. In this build every IMGUI control that takes a control id (Box, Button,
    /// TextField, sliders, BeginGroup) throws "Method unstripping failed", and only GUI.Label and GUI.DrawTexture work.
    /// So the menu draws with those alone and does its own input: the pointer comes from the Input System, converted
    /// to the scaled GUI space, and typed characters from IMGUI KeyDown events.
    /// </summary>
    internal static class MenuInput
    {
        /// <summary>Cursor in GUI space (the space the menu is drawn in, top-left origin).</summary>
        public static Vector2 Pos;
        /// <summary>A press nobody has used yet; whoever takes it sets it false.</summary>
        public static bool Click;
        public static bool Down;
        /// <summary>Wheel notches since last used: positive scrolls down.</summary>
        public static float Wheel;

        public static int Focus = -1;
        public static int Drag = -1;
        private static int _next;

        public static readonly StringBuilder Typed = new StringBuilder();
        public static int Backspaces;

        /// <summary>Once per frame, from Update.</summary>
        public static void Poll(float scale)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                Down = false;
                return;
            }
            var p = mouse.position.ReadValue();
            Pos = new Vector2(p.x / scale, (Screen.height - p.y) / scale);
            if (mouse.leftButton.wasPressedThisFrame)
                Click = true;
            Down = mouse.leftButton.isPressed;
            var scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                Wheel -= Math.Sign(scroll);
        }

        public static void Reset()
        {
            Click = false;
            Down = false;
            Wheel = 0f;
            Focus = -1;
            Drag = -1;
            Typed.Length = 0;
            Backspaces = 0;
        }

        /// <summary>Widget ids are the order they're drawn in, so every widget must ask, visible or not.</summary>
        public static int NextId() => _next++;

        public static void BeginPass() => _next = 0;

        /// <summary>After drawing: a press nothing took blurs the text field; unused input is dropped.</summary>
        public static void EndPass()
        {
            if (Click)
                Focus = -1;
            Click = false;
            Wheel = 0f;
            Typed.Length = 0;
            Backspaces = 0;
            if (!Down)
                Drag = -1;
        }

        /// <summary>Called for IMGUI KeyDown events while the menu is open.</summary>
        public static void Key(Event e)
        {
            if (Focus < 0)
                return;
            if (e.keyCode == KeyCode.Backspace)
            {
                Backspaces++;
                return;
            }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Focus = -1;
                return;
            }
            var c = e.character;
            if (c != '\0' && !char.IsControl(c))
                Typed.Append(c);
        }
    }

    /// <summary>
    /// A tiny hand-written layout: a <see cref="Pane"/> hands out full-width rows top to bottom and a
    /// <see cref="Line"/> places controls along one row from the left and from the right. Text sizes are estimated.
    /// Rows fully outside the viewport are skipped; ones half outside are drawn and the controller paints covers over
    /// the overflow afterwards.
    /// </summary>
    internal sealed class Pane
    {
        /// <summary>Where the viewport sits in GUI space.</summary>
        public readonly float OriginX, OriginY;
        public readonly float Width;
        public readonly float ViewHeight;
        /// <summary>Top of the next row, relative to the viewport (negative once scrolled down).</summary>
        public float Y;

        public Pane(Rect view, float width, float scroll)
        {
            OriginX = view.x;
            OriginY = view.y;
            Width = width;
            ViewHeight = view.height;
            Y = -scroll;
        }

        public void Space(float height) => Y += height;

        public Rect Abs(Rect local) => new Rect(local.x + OriginX, local.y + OriginY, local.width, local.height);

        public bool Visible(Rect local) => local.yMax > 0f && local.y < ViewHeight;

        /// <summary>The pointer is over this rect and inside the viewport.</summary>
        public bool Hot(Rect local)
        {
            var p = MenuInput.Pos;
            return Abs(local).Contains(p) && p.y >= OriginY && p.y <= OriginY + ViewHeight;
        }

        public Line Row(float height, float gap = 6f)
        {
            var line = new Line(this, 0f, Y, Width, height, height);
            Y += height + gap;
            return line;
        }

        /// <summary>A row on a raised card, with buttons 36 high inside it.</summary>
        public Line Card(float height = 52f)
        {
            var rect = new Rect(0f, Y, Width, height);
            if (Visible(rect))
                GUI.Label(Abs(rect), "", Theme.RowStyle);
            var line = new Line(this, 14f, Y, Width - 24f, height, 36f);
            Y += height + 6f;
            return line;
        }

        public void Text(string text, GUIStyle style)
        {
            var height = Theme.TextHeight(text, style, Width);
            var rect = new Rect(0f, Y, Width, height);
            if (Visible(rect))
                GUI.Label(Abs(rect), text, style);
            Y += height;
        }

        public void Heading(string text)
        {
            Y += 10f;
            Text(text, Theme.Heading);
            Y += 4f;
        }
    }

    internal sealed class Line
    {
        private const float Gap = 8f;

        private readonly Pane _pane;
        private readonly float _y;
        private readonly float _height;
        private readonly float _controlHeight;
        private float _left;
        private float _right;

        public Line(Pane pane, float left, float y, float width, float height, float controlHeight)
        {
            _pane = pane;
            _left = left;
            _right = left + width;
            _y = y;
            _height = height;
            _controlHeight = controlHeight;
        }

        private Rect Slot(float x, float width) => new Rect(x, _y + (_height - _controlHeight) / 2f, width, _controlHeight);

        private static float WidthOf(string text, Btn style, float width) =>
            width > 0f ? width : Theme.TextWidth(text, style.Normal) + 36f;

        public bool Button(string text, Btn style, float width = 0f) => Place(text, style, WidthOf(text, style, width), false);

        /// <summary>A button against the right edge; the next ones go to its left.</summary>
        public bool ButtonRight(string text, Btn style, float width = 0f) => Place(text, style, WidthOf(text, style, width), true);

        private bool Place(string text, Btn style, float width, bool fromRight)
        {
            Rect rect;
            if (fromRight)
            {
                _right -= width;
                rect = Slot(_right, width);
                _right -= Gap;
            }
            else
            {
                rect = Slot(_left, width);
                _left += width + Gap;
            }
            if (!_pane.Visible(rect))
                return false;
            var hot = _pane.Hot(rect);
            GUI.Label(_pane.Abs(rect), text, hot ? style.Hover : style.Normal);
            if (!hot || !MenuInput.Click)
                return false;
            MenuInput.Click = false;
            return true;
        }

        /// <summary>Text taking all the room left in the row.</summary>
        public void Label(string text, GUIStyle style)
        {
            var rect = new Rect(_left, _y, Math.Max(0f, _right - _left), _height);
            if (_pane.Visible(rect))
                GUI.Label(_pane.Abs(rect), text, style);
        }

        public void LabelRight(string text, GUIStyle style, float width)
        {
            _right -= width;
            var rect = new Rect(_right, _y, width, _height);
            _right -= Gap;
            if (_pane.Visible(rect))
                GUI.Label(_pane.Abs(rect), text, style);
        }

        /// <summary>One field has focus at a time: click to focus, click elsewhere or Enter to leave.</summary>
        public string TextField(string text, int maxLength, float width)
        {
            var id = MenuInput.NextId();
            var rect = Slot(_left, width);
            _left += width + Gap;
            text = text ?? "";
            if (!_pane.Visible(rect))
                return text;
            if (_pane.Hot(rect) && MenuInput.Click)
            {
                MenuInput.Click = false;
                MenuInput.Focus = id;
            }
            var focused = MenuInput.Focus == id;
            if (focused)
            {
                for (var i = 0; i < MenuInput.Backspaces && text.Length > 0; i++)
                    text = text.Substring(0, text.Length - 1);
                foreach (var c in MenuInput.Typed.ToString())
                {
                    if (text.Length < maxLength)
                        text += c;
                }
                MenuInput.Backspaces = 0;
                MenuInput.Typed.Length = 0;
            }
            var caret = focused && (int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : "";
            GUI.Label(_pane.Abs(rect), text + caret, focused ? Theme.FieldFocus : Theme.Field);
            return text;
        }

        /// <summary>Press on the bar to grab it; it follows the cursor while the button is held.</summary>
        public float Slider(float value, float min, float max, float width)
        {
            var id = MenuInput.NextId();
            var rect = new Rect(_left, _y + (_height - 24f) / 2f, width, 24f);
            _left += width + Gap;
            if (!_pane.Visible(rect))
                return value;

            const float thumbWidth = 16f;
            var travel = rect.width - thumbWidth;
            if (_pane.Hot(rect) && MenuInput.Click)
            {
                MenuInput.Click = false;
                MenuInput.Drag = id;
            }
            if (MenuInput.Drag == id && MenuInput.Down)
            {
                var t = Mathf.Clamp01((MenuInput.Pos.x - _pane.OriginX - rect.x - thumbWidth / 2f) / travel);
                value = min + t * (max - min);
            }

            var frac = max > min ? Mathf.Clamp01((value - min) / (max - min)) : 0f;
            var thumbX = rect.x + frac * travel;
            GUI.Label(_pane.Abs(new Rect(rect.x, rect.y + 9f, rect.width, 6f)), "", Theme.Track);
            GUI.Label(_pane.Abs(new Rect(rect.x, rect.y + 9f, frac * travel + thumbWidth / 2f, 6f)), "", Theme.Fill);
            GUI.Label(_pane.Abs(new Rect(thumbX, rect.y, thumbWidth, rect.height)), "", Theme.Thumb);
            return value;
        }
    }
}
