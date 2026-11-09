using System;
using System.Collections.Generic;
using Blackbox.Core;
using Blackbox.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blackbox.UI.Board
{
    public enum InkTool { Off, Pen, Eraser }

    /// <summary>
    /// The player's notes: freehand strokes over the circuit screen, kept per
    /// circuit. With the pen on, a click-and-drag draws and a right-drag erases;
    /// with the eraser on, any drag erases. With both off the layer lets every
    /// click through to the board.
    /// </summary>
    public sealed class InkLayer : MonoBehaviour
    {
        /// <summary>The pen colours, in the order the swatches show them.</summary>
        public static readonly Color[] Inks =
        {
            Palette.Hex(0xE8EAF2),
            Palette.Hex(0x7FE9FF),
            Palette.Hex(0xC3A2FF),
        };

        public const float PenWidth = 3f;
        public const float EraserRadius = 16f;

        /// <summary>A stroke was added or removed. The list is the one passed to <see cref="Create"/>.</summary>
        public Action Changed;

        List<InkStroke> _strokes;
        readonly List<InkGraphic> _graphics = new List<InkGraphic>();
        RectTransform _rt, _strokeLayer;
        HitArea _capture;
        ShapeGraphic _cursor;
        InkTool _tool;
        int _colour;
        bool _hover;

        InkStroke _drawing;
        InkGraphic _drawingGraphic;
        bool _erasing;
        bool _erasedThisDrag;

        public InkTool Tool => _tool;
        public int Colour { get => _colour; set => _colour = Mathf.Clamp(value, 0, Inks.Length - 1); }
        public int StrokeCount => _strokes.Count;

        /// <param name="area">Where the pen can draw, in frame coordinates from the top-left.</param>
        public static InkLayer Create(Transform parent, List<InkStroke> strokes, Rect area)
        {
            var rt = UIKit.Rect(parent, "Ink").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            var layer = rt.gameObject.AddComponent<InkLayer>();
            layer._rt = rt;
            layer._strokes = strokes;
            layer._strokeLayer = UIKit.Rect(rt, "Strokes").Stretch();

            layer._capture = UIKit.Hit(rt, "Capture");
            layer._capture.rectTransform.At(area.center.x, area.center.y, area.width, area.height);
            // The capture area forwards its events to this component.
            var relay = layer._capture.gameObject.AddComponent<InkRelay>();
            relay.Layer = layer;
            layer._capture.raycastTarget = false;

            layer._cursor = UIKit.Shape(rt, "Cursor", ShapeKind.Circle);
            layer._cursor.Style(Paint.None, Color.clear, Paint.Solid, Palette.Dim, 1.2f);
            layer._cursor.raycastTarget = false;
            layer._cursor.enabled = false;

            foreach (var s in strokes) layer.AddGraphic(s);
            return layer;
        }

        public void SetTool(InkTool tool)
        {
            if (_tool == tool) return;
            EndStroke();
            _tool = tool;
            _capture.raycastTarget = tool != InkTool.Off;
            UpdateCursor();
        }

        public void Clear()
        {
            EndStroke();
            if (_strokes.Count == 0) return;
            _strokes.Clear();
            foreach (var g in _graphics) Destroy(g.gameObject);
            _graphics.Clear();
            Changed?.Invoke();
        }

        /// <summary>Takes away the newest stroke.</summary>
        public bool UndoStroke()
        {
            if (_strokes.Count == 0) return false;
            int last = _strokes.Count - 1;
            _strokes.RemoveAt(last);
            Destroy(_graphics[last].gameObject);
            _graphics.RemoveAt(last);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ strokes

        InkGraphic AddGraphic(InkStroke stroke)
        {
            var g = UIKit.Add<InkGraphic>(_strokeLayer, "Stroke");
            g.raycastTarget = false;
            g.rectTransform.Stretch();
            g.LineWidth = PenWidth;
            g.Ink = Inks[Mathf.Clamp(stroke.Color, 0, Inks.Length - 1)];
            for (int i = 0; i + 1 < stroke.Points.Count; i += 2) g.Points.Add(ToLocal(stroke.Points[i], stroke.Points[i + 1]));
            g.SetVerticesDirty();
            _graphics.Add(g);
            return g;
        }

        static Vector2 ToLocal(int frameX, int frameY) => new Vector2(frameX - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - frameY);

        bool Local(PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out local);

        void BeginStroke(Vector2 local)
        {
            _drawing = new InkStroke { Color = _colour };
            _drawingGraphic = null;
            Extend(local);
        }

        void Extend(Vector2 local)
        {
            if (_drawing == null) return;
            if (_drawingGraphic != null && _drawingGraphic.Points.Count > 0 &&
                Vector2.Distance(_drawingGraphic.Points[_drawingGraphic.Points.Count - 1], local) < 2.5f) return;
            int fx = Mathf.RoundToInt(local.x + VoidFrame.Width * 0.5f);
            int fy = Mathf.RoundToInt(VoidFrame.Height * 0.5f - local.y);
            _drawing.Points.Add(fx);
            _drawing.Points.Add(fy);
            if (_drawingGraphic == null)
            {
                _strokes.Add(_drawing);
                _drawingGraphic = AddGraphic(_drawing);
            }
            else _drawingGraphic.Add(ToLocal(fx, fy));
        }

        void EndStroke()
        {
            bool had = _drawing != null && _drawingGraphic != null;
            _drawing = null;
            _drawingGraphic = null;
            if (had) Changed?.Invoke();
            if (_erasing && _erasedThisDrag) Changed?.Invoke();
            _erasing = false;
            _erasedThisDrag = false;
        }

        void EraseAt(Vector2 local)
        {
            for (int i = _graphics.Count - 1; i >= 0; i--)
            {
                if (_graphics[i].DistanceTo(local) > EraserRadius + PenWidth * 0.5f) continue;
                Destroy(_graphics[i].gameObject);
                _graphics.RemoveAt(i);
                _strokes.RemoveAt(i);
                if (!_erasedThisDrag) App.Sound(Sfx.Close, 1.8f, 0.25f);
                _erasedThisDrag = true;
            }
        }

        // ------------------------------------------------------------------ pointer

        internal void PointerDown(PointerEventData e)
        {
            if (_tool == InkTool.Off || !Local(e, out var local)) return;
            bool erase = _tool == InkTool.Eraser || e.button == PointerEventData.InputButton.Right;
            if (e.button == PointerEventData.InputButton.Middle) return;
            if (erase)
            {
                _erasing = true;
                _erasedThisDrag = false;
                EraseAt(local);
            }
            else BeginStroke(local);
        }

        internal void Drag(PointerEventData e)
        {
            if (!Local(e, out var local)) return;
            if (_erasing) EraseAt(local);
            else Extend(local);
        }

        internal void PointerUp(PointerEventData e) => EndStroke();

        internal void Hovering(bool on)
        {
            _hover = on;
            UpdateCursor();
        }

        void UpdateCursor()
        {
            _cursor.enabled = _hover && _tool == InkTool.Eraser;
            float d = EraserRadius * 2f;
            _cursor.rectTransform.sizeDelta = new Vector2(d, d);
        }

        void Update()
        {
            if (!_cursor.enabled) return;
            var canvas = _cursor.canvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, GameInput.PointerPosition, cam, out var local))
                _cursor.rectTransform.anchoredPosition = local;
        }

        void OnDisable() => EndStroke();
    }

    /// <summary>Sits on the ink layer's capture area and hands its pointer events to the layer.</summary>
    public sealed class InkRelay : MonoBehaviour, IInitializePotentialDragHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
    {
        public InkLayer Layer;
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e) => Layer.PointerDown(e);
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) => Layer.Drag(e);
        public void OnEndDrag(PointerEventData e) { }
        public void OnPointerUp(PointerEventData e) => Layer.PointerUp(e);
        public void OnPointerEnter(PointerEventData e) => Layer.Hovering(true);
        public void OnPointerExit(PointerEventData e) => Layer.Hovering(false);
    }
}
