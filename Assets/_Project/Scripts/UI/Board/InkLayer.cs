using System;
using System.Collections.Generic;
using Blackbox.Core;
using Blackbox.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blackbox.UI.Board
{
    public enum InkTool { Off, Pen, Eraser, Text }

    /// <summary>
    /// The player's notes, kept per circuit: freehand strokes and typed text over
    /// the circuit screen.
    /// <list type="bullet">
    /// <item>Pen: click and drag to draw. Right-drag rubs out.</item>
    /// <item>Eraser: any drag rubs out strokes and typed notes.</item>
    /// <item>Text: click anywhere and type. Click a note to edit it, drag it to move it, click away to finish. Right-drag rubs out.</item>
    /// </list>
    /// With every tool off the layer lets every click through to the board.
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
        public const int NoteSize = 22;
        public const float NoteLineHeight = 1.3f;
        public const int MaxNoteLength = 160;
        public const int MaxNoteLines = 6;
        /// <summary>Typed lines wrap at this width, or sooner near the right edge.</summary>
        public const float MaxNoteWidth = 560f;

        /// <summary>A stroke or note was added, changed or removed. The lists are the ones passed to <see cref="Create"/>.</summary>
        public Action Changed;
        /// <summary>Typing into a note started (true) or finished (false).</summary>
        public Action<bool> TypingChanged;

        sealed class NoteView
        {
            public TextNote Note;
            public RectTransform Rt;
            public VoidText Text;
        }

        List<InkStroke> _strokes;
        List<TextNote> _notes;
        readonly List<InkGraphic> _graphics = new List<InkGraphic>();
        readonly List<NoteView> _noteViews = new List<NoteView>();
        /// <summary>Strokes and notes added during this visit, oldest first, for undo.</summary>
        readonly List<object> _history = new List<object>();
        RectTransform _rt, _strokeLayer, _noteLayer;
        Rect _area;
        HitArea _capture;
        ShapeGraphic _cursor, _caret;
        VoidText _measure;
        InkTool _tool;
        int _colour;
        bool _hover;

        InkStroke _drawing;
        InkGraphic _drawingGraphic;
        bool _erasing;
        bool _erasedThisDrag;

        NoteView _editing;
        /// <summary>The note just finished, which Backspace can open again.</summary>
        NoteView _lastTyped;
        float _blink;
        float _backspaceHeld;
        readonly float[] _held = new float[4];
        int _at; // caret position in the note being typed
        NoteView _pressed;
        Vector2 _pressAt, _pressOffset;
        bool _dragged;

        public InkTool Tool => _tool;
        public int Colour
        {
            get => _colour;
            set
            {
                _colour = Mathf.Clamp(value, 0, Inks.Length - 1);
                if (_editing == null) return;
                _editing.Note.Color = _colour;
                _editing.Text.color = Inks[_colour];
                _caret.FillColor = Inks[_colour];
            }
        }

        /// <summary>Strokes and typed notes on this circuit.</summary>
        public int ItemCount => _strokes.Count + _notes.Count;

        /// <summary>True while a note is being typed. The screen should hand its keys to <see cref="HandleTyping"/>.</summary>
        public bool Typing => _editing != null;

        /// <param name="area">Where notes can go, in frame coordinates from the top-left.</param>
        public static InkLayer Create(Transform parent, List<InkStroke> strokes, List<TextNote> notes, Rect area)
        {
            var rt = UIKit.Rect(parent, "Ink").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            var layer = rt.gameObject.AddComponent<InkLayer>();
            layer._rt = rt;
            layer._strokes = strokes;
            layer._notes = notes;
            layer._area = area;
            layer._strokeLayer = UIKit.Rect(rt, "Strokes").Stretch();
            layer._noteLayer = UIKit.Rect(rt, "Notes").Stretch();

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

            layer._caret = UIKit.Shape(rt, "Caret");
            layer._caret.Radius = 1f;
            layer._caret.Style(Paint.Solid, Inks[0], Paint.None, Color.clear);
            layer._caret.raycastTarget = false;
            layer._caret.enabled = false;

            layer._measure = UIKit.Text(rt, "", NoteStyle(Color.clear), TextAnchor.UpperLeft, "Measure");
            layer._measure.enabled = false;

            foreach (var s in strokes) layer.AddGraphic(s);
            foreach (var n in notes) layer.AddNoteView(n);
            return layer;
        }

        static TextStyle NoteStyle(Color c) => new TextStyle(300, NoteSize, 0.01f, false, c);

        public void SetTool(InkTool tool)
        {
            if (_tool == tool) return;
            EndStroke();
            _pressed = null;
            _tool = tool;
            if (tool != InkTool.Text) FinishTyping();
            _lastTyped = null;
            _capture.raycastTarget = tool != InkTool.Off;
            UpdateCursor();
        }

        public void Clear()
        {
            EndStroke();
            FinishTyping();
            if (ItemCount == 0) return;
            _strokes.Clear();
            foreach (var g in _graphics) Destroy(g.gameObject);
            _graphics.Clear();
            _notes.Clear();
            foreach (var v in _noteViews) Destroy(v.Rt.gameObject);
            _noteViews.Clear();
            _history.Clear();
            _lastTyped = null;
            Changed?.Invoke();
        }

        /// <summary>Takes away the newest stroke or note.</summary>
        public bool UndoLast()
        {
            EndStroke();
            FinishTyping();
            while (_history.Count > 0)
            {
                var item = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                if (item is InkStroke stroke && RemoveStroke(_strokes.IndexOf(stroke))) return true;
                if (item is TextNote note && RemoveNote(_notes.IndexOf(note))) return true;
            }
            // Nothing added on this visit: take the newest of what was there before.
            if (_strokes.Count > 0) return RemoveStroke(_strokes.Count - 1);
            if (_notes.Count > 0) return RemoveNote(_notes.Count - 1);
            return false;
        }

        bool RemoveStroke(int i)
        {
            if (i < 0) return false;
            _strokes.RemoveAt(i);
            Destroy(_graphics[i].gameObject);
            _graphics.RemoveAt(i);
            Changed?.Invoke();
            return true;
        }

        bool RemoveNote(int i)
        {
            if (i < 0) return false;
            if (_editing == _noteViews[i]) StopEditing();
            if (_lastTyped == _noteViews[i]) _lastTyped = null;
            _notes.RemoveAt(i);
            Destroy(_noteViews[i].Rt.gameObject);
            _noteViews.RemoveAt(i);
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
        static int FrameX(Vector2 local) => Mathf.RoundToInt(local.x + VoidFrame.Width * 0.5f);
        static int FrameY(Vector2 local) => Mathf.RoundToInt(VoidFrame.Height * 0.5f - local.y);

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
            int fx = FrameX(local), fy = FrameY(local);
            _drawing.Points.Add(fx);
            _drawing.Points.Add(fy);
            if (_drawingGraphic == null)
            {
                _strokes.Add(_drawing);
                _history.Add(_drawing);
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
            bool erased = false;
            for (int i = _graphics.Count - 1; i >= 0; i--)
            {
                if (_graphics[i].DistanceTo(local) > EraserRadius + PenWidth * 0.5f) continue;
                Destroy(_graphics[i].gameObject);
                _graphics.RemoveAt(i);
                _strokes.RemoveAt(i);
                erased = true;
            }
            for (int i = _noteViews.Count - 1; i >= 0; i--)
            {
                var v = _noteViews[i];
                if (v == _editing || !Touches(Bounds(v), local, EraserRadius)) continue;
                if (v == _lastTyped) _lastTyped = null;
                Destroy(v.Rt.gameObject);
                _noteViews.RemoveAt(i);
                _notes.RemoveAt(i);
                erased = true;
            }
            if (!erased) return;
            if (!_erasedThisDrag) App.Sound(Sfx.Close, 1.8f, 0.25f);
            _erasedThisDrag = true;
        }

        static bool Touches(Rect r, Vector2 p, float radius)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return dx * dx + dy * dy <= radius * radius;
        }

        // ------------------------------------------------------------------ typed notes

        NoteView AddNoteView(TextNote note)
        {
            var rt = UIKit.Rect(_noteLayer, "Note");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(1400f, 400f);
            rt.anchoredPosition = ToLocal(note.X, note.Y);
            var text = UIKit.Text(rt, note.Text, NoteStyle(Inks[Mathf.Clamp(note.Color, 0, Inks.Length - 1)]), TextAnchor.UpperLeft, "Text");
            text.LineHeight = NoteLineHeight;
            text.rectTransform.Stretch();
            var view = new NoteView { Note = note, Rt = rt, Text = text };
            _noteViews.Add(view);
            return view;
        }

        static int LineCount(string s)
        {
            int lines = 1;
            foreach (char ch in s) if (ch == '\n') lines++;
            return lines;
        }

        float LineWidth(string line)
        {
            _measure.text = line;
            return _measure.MeasureWidth();
        }

        /// <summary>The space a note takes up, in the layer's local coordinates.</summary>
        Rect Bounds(NoteView v)
        {
            Vector2 topLeft = ToLocal(v.Note.X, v.Note.Y);
            float w = Mathf.Max(18f, v.Text.MeasureWidth());
            float h = NoteSize * (1f + (LineCount(v.Note.Text) - 1) * NoteLineHeight);
            return new Rect(topLeft.x - 4f, topLeft.y - h - 4f, w + 8f, h + 8f);
        }

        NoteView NoteAt(Vector2 local)
        {
            for (int i = _noteViews.Count - 1; i >= 0; i--)
                if (Bounds(_noteViews[i]).Contains(local)) return _noteViews[i];
            return null;
        }

        void StartNote(Vector2 local)
        {
            // Put the click in the middle of the first line, where the caret will blink,
            // and keep at least a short line's room inside the notes area.
            int x = Mathf.Clamp(FrameX(local) - 2, Mathf.RoundToInt(_area.xMin + 6f), Mathf.RoundToInt(_area.xMax - 200f));
            int y = Mathf.Clamp(FrameY(local) - Mathf.RoundToInt(NoteSize * 0.5f), Mathf.RoundToInt(_area.yMin + 4f), Mathf.RoundToInt(_area.yMax - NoteSize - 6f));
            var note = new TextNote { X = x, Y = y, Color = _colour };
            _notes.Add(note);
            _history.Add(note);
            StartEditing(AddNoteView(note));
        }

        void StartEditing(NoteView v)
        {
            if (_editing == v) return;
            FinishTyping();
            _editing = v;
            _blink = 0f;
            _backspaceHeld = 0f;
            _at = v.Note.Text.Length;
            _caret.FillColor = Inks[Mathf.Clamp(v.Note.Color, 0, Inks.Length - 1)];
            PlaceCaret();
            TypingChanged?.Invoke(true);
        }

        void StopEditing()
        {
            if (_editing == null) return;
            _editing = null;
            _caret.enabled = false;
            TypingChanged?.Invoke(false);
        }

        /// <summary>Finishes the note being typed. An empty note is thrown away.</summary>
        public void FinishTyping()
        {
            var v = _editing;
            if (v == null) return;
            StopEditing();
            v.Note.Text = v.Note.Text.TrimEnd();
            v.Text.text = v.Note.Text;
            int i = _noteViews.IndexOf(v);
            if (v.Note.Text.Length == 0) RemoveNote(i);
            else
            {
                _lastTyped = v;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Opens the note just finished again and takes off its last character, so
        /// Backspace straight after Enter carries on editing instead of undoing the note.
        /// </summary>
        public bool ResumeTyping()
        {
            var v = _lastTyped;
            if (_tool != InkTool.Text || v == null || !_noteViews.Contains(v)) return false;
            StartEditing(v);
            if (v.Note.Text.Length > 0)
            {
                v.Note.Text = v.Note.Text.Substring(0, v.Note.Text.Length - 1);
                v.Text.text = v.Note.Text;
            }
            _at = v.Note.Text.Length;
            PlaceCaret();
            return true;
        }

        /// <summary>
        /// Reads the keyboard into the note being typed. Enter or Esc finishes it,
        /// Shift+Enter starts a new line, the arrow keys move the caret, Backspace and
        /// Delete remove the character before or after it (both repeat when held).
        /// </summary>
        public void HandleTyping()
        {
            if (_editing == null) return;
            if (GameInput.Down(Key.Escape) || (GameInput.Down(Key.Enter) && !GameInput.Held(Key.Shift)))
            {
                FinishTyping();
                return;
            }
            var note = _editing.Note;
            string before = note.Text;
            int was = _at;
            _at = Mathf.Clamp(_at, 0, note.Text.Length);

            if (Repeat(Key.Left, 0)) _at = Mathf.Max(0, _at - 1);
            if (Repeat(Key.Right, 1)) _at = Mathf.Min(note.Text.Length, _at + 1);
            if (Repeat(Key.Up, 2)) _at = LineMove(note.Text, _at, -1);
            if (Repeat(Key.Down, 3)) _at = LineMove(note.Text, _at, +1);

            if (GameInput.Down(Key.Enter) && LineCount(note.Text) < LinesAllowed(note)) Insert(note, '\n');
            foreach (char ch in GameInput.Typed)
            {
                if (char.IsControl(ch) || note.Text.Length >= MaxNoteLength) continue;
                Type(note, ch);
            }

            if (Repeat(Key.Backspace, -1) && _at > 0)
            {
                note.Text = note.Text.Remove(_at - 1, 1);
                _at--;
            }
            if (GameInput.Down(Key.Delete) && _at < note.Text.Length) note.Text = note.Text.Remove(_at, 1);

            if (note.Text != before) _editing.Text.text = note.Text;
            if (note.Text != before || _at != was)
            {
                _blink = 0f;
                PlaceCaret();
            }
        }

        /// <summary>True on the press, then again at a steady rate while the key is held.</summary>
        bool Repeat(Key key, int slot)
        {
            if (GameInput.Down(key))
            {
                if (slot < 0) _backspaceHeld = 0f; else _held[slot] = 0f;
                return true;
            }
            if (!GameInput.Held(key)) return false;
            float held = slot < 0 ? _backspaceHeld : _held[slot];
            float now = held + Time.unscaledDeltaTime;
            if (slot < 0) _backspaceHeld = now; else _held[slot] = now;
            const float Pause = 0.45f, Every = 0.04f;
            return now > Pause && Mathf.FloorToInt((now - Pause) / Every) != Mathf.FloorToInt((held - Pause) / Every);
        }

        /// <summary>The caret moved to the line above or below, keeping its column where it can.</summary>
        static int LineMove(string text, int at, int dir)
        {
            int lineStart = at > 0 ? text.LastIndexOf('\n', at - 1) + 1 : 0;
            int column = at - lineStart;
            if (dir < 0)
            {
                if (lineStart == 0) return 0;
                int prevStart = lineStart > 1 ? text.LastIndexOf('\n', lineStart - 2) + 1 : 0;
                return Mathf.Min(prevStart + column, lineStart - 1);
            }
            int lineEnd = text.IndexOf('\n', at);
            if (lineEnd < 0) return text.Length;
            int nextEnd = text.IndexOf('\n', lineEnd + 1);
            if (nextEnd < 0) nextEnd = text.Length;
            return Mathf.Min(lineEnd + 1 + column, nextEnd);
        }

        void Insert(TextNote note, char ch)
        {
            note.Text = note.Text.Insert(_at, ch.ToString());
            _at++;
        }

        /// <summary>Adds one character, wrapping the word onto a new line when the line gets too wide.</summary>
        void Type(TextNote note, char ch)
        {
            if (_at < note.Text.Length)
            {
                // In the middle of the text: insert only if the line still fits.
                string inserted = note.Text.Insert(_at, ch.ToString());
                int lineStart = inserted.LastIndexOf('\n', _at) + 1;
                int lineEnd = inserted.IndexOf('\n', _at);
                string line = inserted.Substring(lineStart, (lineEnd < 0 ? inserted.Length : lineEnd) - lineStart);
                if (LineWidth(line) > LineLimit(note)) return;
                note.Text = inserted;
                _at++;
                return;
            }
            TypeAtEnd(note, ch);
            _at = note.Text.Length;
        }

        void TypeAtEnd(TextNote note, char ch)
        {
            string text = note.Text + ch;
            int lastBreak = text.LastIndexOf('\n');
            string last = text.Substring(lastBreak + 1);
            if (LineWidth(last) <= LineLimit(note)) { note.Text = text; return; }
            if (LineCount(text) >= LinesAllowed(note)) return;
            if (ch == ' ') { note.Text += "\n"; return; }
            int space = last.LastIndexOf(' ');
            note.Text = space > 0
                ? text.Substring(0, lastBreak + 1 + space) + "\n" + last.Substring(space + 1)
                : note.Text + "\n" + ch;
        }

        float LineLimit(TextNote note) => Mathf.Clamp(_area.xMax - note.X - 12f, 160f, MaxNoteWidth);

        /// <summary>How many lines fit between the note's top and the bottom of the notes area.</summary>
        int LinesAllowed(TextNote note)
        {
            float room = _area.yMax - 4f - note.Y - NoteSize;
            return Mathf.Clamp(1 + Mathf.FloorToInt(room / (NoteSize * NoteLineHeight)), 1, MaxNoteLines);
        }

        void PlaceCaret()
        {
            if (_editing == null) return;
            string text = _editing.Note.Text.Substring(0, Mathf.Clamp(_at, 0, _editing.Note.Text.Length));
            int lines = LineCount(text);
            int lastBreak = text.LastIndexOf('\n');
            string last = lastBreak >= 0 ? text.Substring(lastBreak + 1) : text;
            Vector2 topLeft = ToLocal(_editing.Note.X, _editing.Note.Y);
            float x = topLeft.x + LineWidth(last) + 3f;
            float y = topLeft.y - NoteSize * 0.4f - (lines - 1) * NoteSize * NoteLineHeight;
            _caret.rectTransform.anchoredPosition = new Vector2(x, y);
            _caret.rectTransform.sizeDelta = new Vector2(2f, NoteSize * 1.1f);
        }

        void MoveNote(NoteView v, Vector2 topLeftLocal)
        {
            // Keep the whole note inside the notes area.
            var b = Bounds(v);
            int minX = Mathf.RoundToInt(_area.xMin + 6f), minY = Mathf.RoundToInt(_area.yMin + 4f);
            v.Note.X = Mathf.Clamp(FrameX(topLeftLocal), minX, Mathf.Max(minX, Mathf.RoundToInt(_area.xMax - b.width)));
            v.Note.Y = Mathf.Clamp(FrameY(topLeftLocal), minY, Mathf.Max(minY, Mathf.RoundToInt(_area.yMax - b.height)));
            v.Rt.anchoredPosition = ToLocal(v.Note.X, v.Note.Y);
            if (v == _editing) PlaceCaret();
        }

        // ------------------------------------------------------------------ pointer

        internal void PointerDown(PointerEventData e)
        {
            if (_tool == InkTool.Off || e.button == PointerEventData.InputButton.Middle || !Local(e, out var local)) return;
            _lastTyped = null;
            bool erase = _tool == InkTool.Eraser || e.button == PointerEventData.InputButton.Right;
            if (erase)
            {
                _erasing = true;
                _erasedThisDrag = false;
                EraseAt(local);
                return;
            }
            if (_tool == InkTool.Text)
            {
                var hit = NoteAt(local);
                if (hit != null)
                {
                    // Pressing a note picks it up: a drag moves it, a click edits it.
                    _pressed = hit;
                    _pressAt = local;
                    _pressOffset = ToLocal(hit.Note.X, hit.Note.Y) - local;
                    _dragged = false;
                    if (_editing != null && _editing != hit) FinishTyping();
                }
                else if (_editing != null)
                {
                    // Clicking away finishes the note; the next click starts a new one.
                    FinishTyping();
                }
                else
                {
                    StartNote(local);
                    App.Sound(Sfx.Tick, 1.2f, 0.6f);
                }
                return;
            }
            BeginStroke(local);
        }

        internal void Drag(PointerEventData e)
        {
            if (!Local(e, out var local)) return;
            if (_pressed != null)
            {
                if (!_dragged && Vector2.Distance(local, _pressAt) > 4f) _dragged = true;
                if (_dragged) MoveNote(_pressed, local + _pressOffset);
                return;
            }
            if (_erasing) EraseAt(local);
            else Extend(local);
        }

        internal void PointerUp(PointerEventData e)
        {
            if (_pressed != null)
            {
                var v = _pressed;
                _pressed = null;
                if (_dragged) Changed?.Invoke();
                else if (_noteViews.Contains(v)) StartEditing(v);
                return;
            }
            EndStroke();
        }

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
            if (_editing != null)
            {
                _blink += Time.unscaledDeltaTime;
                _caret.enabled = _blink % 1.05f < 0.62f;
            }
            if (!_cursor.enabled) return;
            var canvas = _cursor.canvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, GameInput.PointerPosition, cam, out var local))
                _cursor.rectTransform.anchoredPosition = local;
        }

        void OnDisable()
        {
            EndStroke();
            FinishTyping();
        }
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
