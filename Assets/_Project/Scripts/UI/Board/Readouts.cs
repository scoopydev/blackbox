using System;
using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blackbox.UI.Board
{
    /// <summary>
    /// A square wave over ticks. Values that are on glow like a live wire; wanted
    /// values are a dashed ghost behind; misses get a violet mark below.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaveGraphic : VoidGraphic
    {
        bool[] _values;
        sbyte[] _want;
        int _reveal = int.MaxValue;
        int _ticks;
        bool _stale;
        bool _showMisses = true;

        readonly List<Vector2> _pts = new List<Vector2>(64);
        readonly List<List<Vector2>> _dashes = new List<List<Vector2>>();

        public void Set(int ticks, bool[] values, sbyte[] want, int reveal, bool stale, bool showMisses = true)
        {
            _ticks = ticks;
            _values = values;
            _want = want;
            _reveal = reveal;
            _stale = stale;
            _showMisses = showMisses;
            SetVerticesDirty();
        }

        /// <summary>A square wave from tick <paramref name="from"/> up to <paramref name="to"/>, with no repeated points.</summary>
        static void StepLine(List<Vector2> pts, int from, int to, Func<int, bool> on, float low, float high, float x0, float tw)
        {
            pts.Clear();
            for (int t = from; t < to; t++)
            {
                float y = on(t) ? high : low;
                if (t == from || on(t) != on(t - 1)) pts.Add(new Vector2(x0 + t * tw, y));
                if (t == to - 1 || on(t + 1) != on(t)) pts.Add(new Vector2(x0 + (t + 1) * tw, y));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_ticks <= 0) return;
            PrepareGradient();
            Rect r = rectTransform.rect;
            float f = Feather;
            float tw = r.width / _ticks;
            float low = r.yMin + r.height * 0.18f, high = r.yMax - r.height * 0.18f;
            float X(int t) => r.xMin + t * tw;

            // Wanted wave: a dashed ghost, broken where any value is fine.
            if (_want != null)
            {
                int t = 0;
                while (t < _ticks)
                {
                    if (_want[t] < 0) { t++; continue; }
                    int from = t;
                    while (t < _ticks && _want[t] >= 0) t++;
                    var want = _want;
                    StepLine(_pts, from, t, i => want[i] == 1, low, high, r.xMin, tw);
                    _dashes.Clear();
                    MeshKit.Dashes(_pts, false, 3f, 4f, 0f, _dashes);
                    foreach (var d in _dashes) MeshKit.Stroke(vh, d, false, 1.2f, f, _ => Palette.Faint);
                }
            }

            if (_values == null) return;
            int shown = Mathf.Clamp(_reveal, 0, _ticks);
            if (shown <= 0) return;

            // The actual wave: a dim baseline everywhere, lit where it is on.
            var values = _values;
            StepLine(_pts, 0, shown, i => values[i], low, high, r.xMin, tw);
            MeshKit.Stroke(vh, _pts, false, 1.4f, f, _ => _stale ? Palette.Outline : Palette.Faint);

            float alpha = _stale ? 0.3f : 1f;
            var lit = Colour(Paint.Signal, Color.white, alpha);
            int s = 0;
            while (s < shown)
            {
                if (!_values[s]) { s++; continue; }
                int e = s;
                while (e < shown && _values[e]) e++;
                _pts.Clear();
                bool risesFromLow = s > 0 && !_values[s - 1];
                bool fallsAfter = e < shown && !_values[e];
                if (risesFromLow) _pts.Add(new Vector2(X(s), low));
                _pts.Add(new Vector2(X(s), high));
                _pts.Add(new Vector2(X(e), high));
                if (fallsAfter) _pts.Add(new Vector2(X(e), low));
                MeshKit.Glow(vh, _pts, false, 9f, lit, 0.4f);
                MeshKit.Stroke(vh, _pts, false, 2f, f, lit);
                s = e;
            }

            if (_showMisses && _want != null && !_stale)
            {
                for (int t = 0; t < shown; t++)
                {
                    if (_want[t] < 0 || (_want[t] == 1) == _values[t]) continue;
                    var c = new Vector2(X(t) + tw * 0.5f, r.yMin - 4f);
                    MeshKit.SoftDisc(vh, c, 9f, Palette.Violet.WithAlpha(0.8f));
                    MeshKit.Disc(vh, c, 2.6f, f, Palette.Violet);
                }
            }
        }
    }

    /// <summary>
    /// The truth table along the bottom of a Chapter I circuit: each row's
    /// inputs, what the circuit should give, and what it gave on the last run.
    /// Click a row to show it on the board.
    /// </summary>
    public sealed class CaseTable : MonoBehaviour
    {
        public Action<int> RowClicked;
        PuzzleDef _puzzle;
        readonly List<Row> _rows = new List<Row>();
        int _selected;
        RunResult _run;
        bool _stale;
        int _revealed = int.MaxValue;

        sealed class Row
        {
            public RectTransform Rect;
            public ShapeGraphic Highlight;
            public ShapeGraphic Status;
            public VoidText[] Ins;
            public VoidText[] Wants;
            public VoidText[] Gots;
            public Clickable Click;
            public float Pulse;
        }

        const float RowHeight = 24f;
        const float Col = 32f;
        const float ColumnGap = 56f;

        /// <summary>The table's width on screen, for laying out what sits beside it.</summary>
        public float Width { get; private set; }

        public static CaseTable Create(Transform parent, PuzzleDef puzzle, float x, float y)
        {
            var rt = UIKit.Rect(parent, "Table").At(x, y, 10f, 10f);
            var table = rt.gameObject.AddComponent<CaseTable>();
            table.Build(puzzle);
            return table;
        }

        void Build(PuzzleDef p)
        {
            _puzzle = p;
            var rt = (RectTransform)transform;
            int ni = p.Inputs.Count, no = p.Outputs.Count;
            float xIn = 40f;
            float xArrow = xIn + ni * Col + 8f;
            float xWant = xArrow + 40f;
            float xGot = xWant + no * Col + 36f;

            float width = xGot + no * Col + 10f;

            // More than four rows wrap into a second column, so the table stays low.
            int perColumn = p.Cases.Count > 4 ? (p.Cases.Count + 1) / 2 : p.Cases.Count;
            int columns = (p.Cases.Count + perColumn - 1) / perColumn;
            for (int col = 0; col < columns; col++)
            {
                float ox = col * (width + ColumnGap);
                Header(rt, "IN", ox + xIn + (ni - 1) * Col * 0.5f, 0f);
                Header(rt, "WANT", ox + xWant + (no - 1) * Col * 0.5f, 0f);
                Header(rt, "GOT", ox + xGot + (no - 1) * Col * 0.5f, 0f);
                for (int i = 0; i < ni; i++) Name(rt, p.Inputs[i].Id, ox + xIn + i * Col, 24f);
                for (int o = 0; o < no; o++) Name(rt, p.Outputs[o].Id, ox + xWant + o * Col, 24f);
                for (int o = 0; o < no; o++) Name(rt, p.Outputs[o].Id, ox + xGot + o * Col, 24f);
            }
            Width = columns * width + (columns - 1) * ColumnGap;

            for (int c = 0; c < p.Cases.Count; c++)
            {
                var cs = p.Cases[c];
                float ox = (c / perColumn) * (width + ColumnGap);
                float y = 54f + (c % perColumn) * RowHeight;
                var row = new Row { Rect = UIKit.Rect(rt, "Row " + (c + 1)).At(ox + width * 0.5f - 6f, y, width + 12f, RowHeight) };
                row.Highlight = UIKit.Shape(row.Rect, "Selected", ShapeKind.Pill);
                row.Highlight.rectTransform.Stretch();
                row.Highlight.Style(Paint.Solid, Palette.Surface, Paint.Solid, Palette.Outline, 1f);
                UIKit.Hit(row.Rect).rectTransform.Stretch();
                row.Click = row.Rect.gameObject.AddComponent<Clickable>();
                row.Click.ClickSound = Sfx.Tick;
                int index = c;
                row.Click.Clicked = () => RowClicked?.Invoke(index);
                row.Click.HoverChanged = _ => Restyle();

                row.Status = UIKit.Shape(row.Rect, "Status", ShapeKind.Circle);
                row.Status.rectTransform.At(16f, RowHeight * 0.5f, 7f, 7f);

                row.Ins = new VoidText[ni];
                for (int i = 0; i < ni; i++) row.Ins[i] = Digit(row.Rect, cs.Inputs[0][i] ? "1" : "0", xIn + i * Col + 6f);
                var arrow = UIKit.Glyph(row.Rect, "arrow", Palette.Faint, 1.2f);
                arrow.rectTransform.At(xArrow + 6f + 10f, RowHeight * 0.5f, 14f, 14f);
                row.Wants = new VoidText[no];
                for (int o = 0; o < no; o++) row.Wants[o] = Digit(row.Rect, cs.Want[0][o] < 0 ? "·" : cs.Want[0][o].ToString(), xWant + o * Col + 6f);
                row.Gots = new VoidText[no];
                for (int o = 0; o < no; o++) row.Gots[o] = Digit(row.Rect, "·", xGot + o * Col + 6f);
                _rows.Add(row);
            }
            Restyle();
        }

        static void Header(RectTransform parent, string text, float x, float y)
        {
            var t = UIKit.Text(parent, text, TextStyle.Label.WithSize(14).WithColor(Palette.Faint));
            t.rectTransform.At(x, y, 120f, 20f);
        }

        static void Name(RectTransform parent, string text, float x, float y)
        {
            var t = UIKit.Text(parent, text, TextStyle.Label.WithSize(15).WithTracking(0.1f).WithColor(Palette.Dim));
            t.rectTransform.At(x, y, 40f, 20f);
        }

        static VoidText Digit(RectTransform parent, string text, float x)
        {
            var t = UIKit.Text(parent, text, TextStyle.Digit);
            t.rectTransform.At(x, RowHeight * 0.5f, 30f, RowHeight);
            return t;
        }

        public int Selected
        {
            get => _selected;
            set { _selected = Mathf.Clamp(value, 0, _rows.Count - 1); Restyle(); }
        }

        /// <summary>Shows a run. Rows fill in one after another when <paramref name="stagger"/> is positive.</summary>
        public void ShowRun(RunResult run, float stagger)
        {
            _run = run;
            _stale = false;
            Tweener.Kill(this, "reveal");
            if (stagger <= 0f || App.ReduceMotion)
            {
                _revealed = int.MaxValue;
                Restyle();
                return;
            }
            _revealed = 0;
            Restyle();
            Tweener.Run(this, "reveal", stagger * _rows.Count, t =>
            {
                int n = Mathf.Clamp(Mathf.FloorToInt(t * _rows.Count + 0.999f), 0, _rows.Count);
                if (n != _revealed)
                {
                    _revealed = n;
                    if (n > 0) App.Sound(Sfx.Tick, run.Cases[n - 1].Matched ? 1.25f : 0.8f, 0.7f);
                    Restyle();
                }
            }, Ease.Linear);
        }

        public void SetStale(bool stale)
        {
            _stale = stale;
            Restyle();
        }

        public void Clear()
        {
            _run = null;
            Restyle();
        }

        void Restyle()
        {
            for (int c = 0; c < _rows.Count; c++)
            {
                var row = _rows[c];
                bool selected = c == _selected;
                row.Highlight.enabled = selected || row.Click.Hovered;
                row.Highlight.StrokeColor = selected ? Palette.Outline : Palette.Rule;
                row.Highlight.FillColor = selected ? Palette.Surface : Color.black;

                var cs = _puzzle.Cases[c];
                for (int i = 0; i < row.Ins.Length; i++)
                    row.Ins[i].color = cs.Inputs[0][i] ? Palette.Cyan : Palette.Dim;
                foreach (var w in row.Wants) w.color = selected ? Palette.Text : Palette.Dim;

                bool shown = _run != null && c < _revealed;
                var result = shown ? _run.Cases[c] : null;
                for (int o = 0; o < row.Gots.Length; o++)
                {
                    var got = row.Gots[o];
                    if (result == null) { got.text = "·"; got.color = Palette.Faint; continue; }
                    bool v = result.Output(0, o);
                    got.text = v ? "1" : "0";
                    bool ok = result.OutputOk(0, o);
                    got.color = _stale ? Palette.Faint : !ok ? Palette.Violet : v ? Palette.Cyan : Palette.Text;
                }

                var status = row.Status;
                if (result == null || _stale)
                {
                    status.Style(Paint.None, Color.clear, Paint.Solid, Palette.Faint, 1.2f);
                    status.Glow = 0f;
                }
                else if (result.Matched)
                {
                    status.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                    status.Glow = 0.5f;
                    status.GlowPaint = Paint.Signal;
                    status.GlowRadius = 9f;
                }
                else
                {
                    status.Style(Paint.Solid, Palette.Violet, Paint.None, Color.clear);
                    status.Glow = 0.5f;
                    status.GlowPaint = Paint.Solid;
                    status.GlowColor = Palette.Violet;
                    status.GlowRadius = 9f;
                }
            }
        }
    }

    /// <summary>
    /// The timeline along the bottom of a Chapter II circuit: one lane per input
    /// and output, ticks left to right, and a cursor for the moment the board shows.
    /// </summary>
    public sealed class Timeline : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<int> TickChosen;
        PuzzleDef _puzzle;
        int _case;
        int _ticks;
        readonly List<WaveGraphic> _inLanes = new List<WaveGraphic>();
        readonly List<WaveGraphic> _outLanes = new List<WaveGraphic>();
        PathGraphic _cursor;
        VoidText _tickLabel;
        RectTransform _area;
        float _width;
        int _tick;
        CaseResult _result;
        int _reveal;
        bool _stale;

        const float LaneHeight = 30f;
        const float LaneGap = 12f;
        const float LabelWidth = 64f;

        public static Timeline Create(Transform parent, PuzzleDef puzzle, float x, float y, float width)
        {
            var rt = UIKit.Rect(parent, "Timeline").At(x, y, 10f, 10f);
            var tl = rt.gameObject.AddComponent<Timeline>();
            tl.Build(puzzle, width);
            return tl;
        }

        void Build(PuzzleDef p, float width)
        {
            _puzzle = p;
            _width = width;
            var rt = (RectTransform)transform;
            _ticks = p.Cases[0].Ticks;
            int lanes = p.Inputs.Count + p.Outputs.Count;
            float laneWidth = width - LabelWidth;
            float height = lanes * (LaneHeight + LaneGap);

            _area = UIKit.Rect(rt, "Lanes").At(LabelWidth + laneWidth * 0.5f, 10f + height * 0.5f, laneWidth, height);
            UIKit.Hit(_area).rectTransform.Stretch();
            // Pointer events on the hit area bubble up to this component.
            var forward = _area.gameObject.AddComponent<PointerForward>();
            forward.Target = this;

            int lane = 0;
            foreach (var input in p.Inputs) _inLanes.Add(Lane(rt, input.Id, lane++, laneWidth));
            foreach (var output in p.Outputs) _outLanes.Add(Lane(rt, output.Id, lane++, laneWidth));

            // Tick marks under the lanes.
            var ticks = UIKit.Add<TickMarks>(rt, "Ticks");
            ticks.raycastTarget = false;
            ticks.Count = _ticks;
            ticks.rectTransform.At(LabelWidth + laneWidth * 0.5f, 10f + height + 2f, laneWidth, 6f);

            var cursorRt = UIKit.Rect(_area, "Cursor").Stretch();
            _cursor = cursorRt.gameObject.AddComponent<PathGraphic>();
            _cursor.raycastTarget = false;
            _cursor.BaseColor = Palette.Dim.WithAlpha(0.7f);
            _cursor.BaseWidth = 1f;
            _tickLabel = UIKit.Text(rt, "", TextStyle.Label.WithSize(13).WithColor(Palette.Dim));
            _tickLabel.rectTransform.At(LabelWidth, 10f + height + 20f, 120f, 18f);
            SetTick(0);
        }

        WaveGraphic Lane(RectTransform parent, string name, int index, float laneWidth)
        {
            float y = 10f + index * (LaneHeight + LaneGap) + LaneHeight * 0.5f;
            var label = UIKit.Text(parent, name, TextStyle.Label.WithSize(15).WithTracking(0.12f).WithColor(Palette.Dim), TextAnchor.MiddleLeft);
            label.rectTransform.At(20f, y, 40f, LaneHeight);
            var wave = UIKit.Add<WaveGraphic>(parent, "Lane " + name);
            wave.raycastTarget = false;
            wave.rectTransform.At(LabelWidth + laneWidth * 0.5f, y, laneWidth, LaneHeight);
            return wave;
        }

        public int Tick => _tick;
        public int Ticks => _ticks;

        public void ShowCase(int caseIndex)
        {
            _case = caseIndex;
            Redraw();
        }

        public void ShowRun(CaseResult result, int reveal, bool stale)
        {
            _result = result;
            _reveal = reveal;
            _stale = stale;
            Redraw();
        }

        public void SetStale(bool stale)
        {
            _stale = stale;
            Redraw();
        }

        public void SetTick(int tick)
        {
            _tick = Mathf.Clamp(tick, 0, _ticks - 1);
            Rect r = _area.rect;
            float tw = r.width / _ticks;
            float x = r.xMin + (_tick + 0.5f) * tw;
            _cursor.SetPoints(new List<Vector2> { new Vector2(x, r.yMax + 4f), new Vector2(x, r.yMin - 4f) });
            _tickLabel.text = "TICK " + (_tick + 1).ToString("00") + " / " + _ticks.ToString("00");
        }

        void Redraw()
        {
            var cs = _puzzle.Cases[_case];
            for (int i = 0; i < _inLanes.Count; i++)
            {
                var values = new bool[_ticks];
                for (int t = 0; t < _ticks; t++) values[t] = cs.Inputs[t][i];
                _inLanes[i].Set(_ticks, values, null, int.MaxValue, false, false);
            }
            for (int o = 0; o < _outLanes.Count; o++)
            {
                var want = new sbyte[_ticks];
                for (int t = 0; t < _ticks; t++) want[t] = cs.Want[t][o];
                bool[] values = null;
                if (_result != null)
                {
                    values = new bool[_ticks];
                    for (int t = 0; t < _ticks; t++) values[t] = _result.Output(t, o);
                }
                _outLanes[o].Set(_ticks, values, want, _reveal, _stale);
            }
        }

        int TickAt(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var local)) return _tick;
            Rect r = _area.rect;
            return Mathf.Clamp(Mathf.FloorToInt((local.x - r.xMin) / (r.width / _ticks)), 0, _ticks - 1);
        }

        public void OnPointerDown(PointerEventData e)
        {
            int t = TickAt(e);
            if (t != _tick) App.Sound(Sfx.Tick);
            TickChosen?.Invoke(t);
        }

        public void OnDrag(PointerEventData e)
        {
            int t = TickAt(e);
            if (t == _tick) return;
            App.Sound(Sfx.Tick);
            TickChosen?.Invoke(t);
        }
    }

    /// <summary>Sends pointer presses and drags on a child up to its owner.</summary>
    public sealed class PointerForward : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Timeline Target;
        public void OnPointerDown(PointerEventData e) => Target?.OnPointerDown(e);
        public void OnDrag(PointerEventData e) => Target?.OnDrag(e);
    }

    /// <summary>Faint dots marking tick boundaries.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TickMarks : VoidGraphic
    {
        public int Count;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Count <= 0) return;
            Rect r = rectTransform.rect;
            float f = Feather;
            for (int t = 0; t <= Count; t++)
            {
                float x = r.xMin + t * r.width / Count;
                MeshKit.Disc(vh, new Vector2(x, r.center.y), t % 4 == 0 ? 1.8f : 1.1f, f, Palette.Faint);
            }
        }
    }
}
