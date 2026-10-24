using System;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// A tiny hand-written layout for IMGUI. This build's generated GUILayout internals (BeginLayoutGroup, DoGetRect,
    /// GUI.DoWindow) don't JIT, so everything is drawn with plain GUI.* calls into explicit rectangles: a
    /// <see cref="Pane"/> hands out full-width rows top to bottom and a <see cref="Line"/> places controls along one
    /// row from the left and from the right. Text sizes are estimated (GUIStyle.CalcSize needs layout internals too).
    /// </summary>
    internal sealed class Pane
    {
        /// <summary>Height of the visible band; controls partly outside it are drawn but not clickable.</summary>
        public float ViewHeight;
        public float Width;
        /// <summary>Top of the next row, in the scrolled content space (negative once scrolled down).</summary>
        public float Y;

        public Pane(float width, float viewHeight, float top)
        {
            Width = width;
            ViewHeight = viewHeight;
            Y = top;
        }

        public void Space(float height) => Y += height;

        /// <summary>A plain row of the given height.</summary>
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
                GUI.Box(rect, "", Theme.RowStyle);
            var line = new Line(this, 14f, Y, Width - 24f, height, 36f);
            Y += height + 6f;
            return line;
        }

        public void Text(string text, GUIStyle style)
        {
            var height = Theme.TextHeight(text, style, Width);
            var rect = new Rect(0f, Y, Width, height);
            if (Visible(rect))
                GUI.Label(rect, text, style);
            Y += height;
        }

        public void Heading(string text)
        {
            Y += 10f;
            Text(text, Theme.Heading);
            Y += 4f;
        }

        public bool Visible(Rect rect) => rect.yMax > 0f && rect.y < ViewHeight;

        public bool Clickable(Rect rect) => rect.y >= 0f && rect.yMax <= ViewHeight;
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

        private static float WidthOf(string text, GUIStyle style, float width) =>
            width > 0f ? width : Theme.TextWidth(text, style) + 36f;

        public bool Button(string text, GUIStyle style, float width = 0f) => Place(text, style, WidthOf(text, style, width), false);

        /// <summary>A button against the right edge; the next ones go to its left.</summary>
        public bool ButtonRight(string text, GUIStyle style, float width = 0f) => Place(text, style, WidthOf(text, style, width), true);

        private bool Place(string text, GUIStyle style, float width, bool fromRight)
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
            if (!_pane.Clickable(rect))
            {
                GUI.Box(rect, text, style);
                return false;
            }
            return GUI.Button(rect, text, style);
        }

        /// <summary>Text taking all the room left in the row.</summary>
        public void Label(string text, GUIStyle style)
        {
            var rect = new Rect(_left, _y, Math.Max(0f, _right - _left), _height);
            if (_pane.Visible(rect))
                GUI.Label(rect, text, style);
        }

        public void LabelRight(string text, GUIStyle style, float width)
        {
            _right -= width;
            var rect = new Rect(_right, _y, width, _height);
            _right -= Gap;
            if (_pane.Visible(rect))
                GUI.Label(rect, text, style);
        }

        public string TextField(string text, int maxLength, GUIStyle style, float width)
        {
            var rect = Slot(_left, width);
            _left += width + Gap;
            if (!_pane.Visible(rect))
                return text;
            if (!_pane.Clickable(rect))
            {
                GUI.Box(rect, text, style);
                return text;
            }
            return GUI.TextField(rect, text ?? "", maxLength, style);
        }

        public float Slider(float value, float min, float max, float width)
        {
            var rect = new Rect(_left, _y + (_height - 24f) / 2f, width, 24f);
            _left += width + Gap;
            return _pane.Clickable(rect) ? GUI.HorizontalSlider(rect, value, min, max) : value;
        }
    }
}
