using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blackbox.UI.Board
{
    /// <summary>
    /// A wire: a faint line that never shows its value. A probe on it lights it
    /// with the signal colour; a change upstream sends a pulse along it. Hovering
    /// it, with probes to spare, shows where a probe would go.
    /// </summary>
    public sealed class WireView : MonoBehaviour
    {
        public WireDef Def;
        public int Index => Def.Index;
        public List<Vector2> Points => _path.Points;

        BoardView _board;
        PathGraphic _path;
        FlowGraphic _flow;
        ShapeGraphic _ghost;
        RectTransform _rt;
        bool _hover, _probed, _celebrate, _pulsing;
        float _length;

        public static WireView Create(RectTransform layer, WireDef def, List<Vector2> points, BoardView board)
        {
            var rt = UIKit.Rect(layer, $"Wire {def.From.Id}>{def.To.Id}.{def.ToPort}").Stretch();
            var view = rt.gameObject.AddComponent<WireView>();
            view.Def = def;
            view._board = board;
            view._rt = rt;
            view._path = rt.gameObject.AddComponent<PathGraphic>();
            view._path.raycastTarget = false;
            view._path.SetPoints(points);
            view._path.BaseWidth = 1.5f;
            view._path.LitWidth = 2.2f;
            view._length = MeshKit.Length(points);

            var flowRt = UIKit.Rect(rt, "Flow").Stretch();
            view._flow = flowRt.gameObject.AddComponent<FlowGraphic>();
            view._flow.raycastTarget = false;
            view._flow.Source = view._path;
            view._flow.enabled = false;

            // The hit area covers the wire's bounds; the filter keeps only points near the line.
            Vector2 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            var hit = UIKit.Hit(rt, "Hit");
            hit.rectTransform.Centered((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.x - min.x + 28f, max.y - min.y + 28f);
            var filter = hit.gameObject.AddComponent<WireHit>();
            filter.Wire = view;

            view._ghost = UIKit.Shape(rt, "Ghost probe", ShapeKind.Circle);
            view._ghost.Style(Paint.Solid, Color.black, Paint.Solid, Palette.Dim, 1.4f);
            view._ghost.rectTransform.Centered(0f, 0f, 14f, 14f);
            view._ghost.enabled = false;

            view.ApplyRest();
            return view;
        }

        /// <summary>The point at a fraction of the wire's length, in board space.</summary>
        public Vector2 PointAt(float t) => MeshKit.PointAt(_path.Points, t, out _);

        /// <summary>The nearest point on the wire to <paramref name="p"/>: its distance and fraction along.</summary>
        public float Project(Vector2 p, out float t)
        {
            var pts = _path.Points;
            float best = float.MaxValue, bestAlong = 0f, along = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 ab = b - a;
                float len = ab.magnitude;
                if (len < 1e-4f) continue;
                float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len));
                float d = Vector2.Distance(p, a + ab * u);
                if (d < best) { best = d; bestAlong = along + u * len; }
                along += len;
            }
            t = _length > 0f ? Mathf.Clamp(bestAlong / _length, 0.04f, 0.96f) : 0.5f;
            return best;
        }

        public bool ScreenToLocal(Vector2 screen, Camera cam, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out local);

        public bool Probed
        {
            get => _probed;
            set { if (_probed != value) { _probed = value; ApplyRest(); } }
        }

        internal void SetHover(bool on)
        {
            _hover = on;
            if (!on) _ghost.enabled = false;
            ApplyRest();
            _board.Host?.Hover(BoardItem.Wire, Index, on);
        }

        internal void Clicked(Vector2 local)
        {
            Project(local, out float t);
            _board.Host?.WireClicked(Index, t);
        }

        void Update()
        {
            if (!_hover) return;
            bool can = _board.Host != null && _board.Host.CanPlaceProbe;
            _ghost.enabled = can;
            if (!can) return;
            var canvas = _ghost.canvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!ScreenToLocal(GameInput.PointerPosition, cam, out var local)) return;
            Project(local, out float t);
            Vector2 p = PointAt(t);
            _ghost.rectTransform.anchoredPosition = p;
        }

        /// <summary>A burst of light travelling the length of the wire.</summary>
        public void Pulse(float delay, float duration = 0.32f)
        {
            if (_celebrate) return;
            _pulsing = true;
            Tweener.Run(this, "pulse", duration, t =>
            {
                _path.LitAlpha = 1f;
                _path.SetLit(Mathf.Max(0f, t - 0.35f), t);
                _path.Spark = t < 1f ? t : -1f;
            }, Ease.Linear, delay, () =>
            {
                _pulsing = false;
                ApplyRest();
            });
        }

        public void SetCelebrate(bool on, float delay)
        {
            if (_celebrate == on) return;
            _celebrate = on;
            Tweener.Kill(this, "pulse");
            _pulsing = false;
            if (on && !App.ReduceMotion)
            {
                _path.LitAlpha = 1f;
                _path.SetLit(0f, 0f);
                Tweener.Run(this, "celebrate", 0.3f, t =>
                {
                    _path.SetLit(0f, t);
                    _path.Spark = t < 1f ? t : -1f;
                }, Ease.Linear, delay, ApplyRest);
            }
            else
            {
                Tweener.Kill(this, "celebrate");
                ApplyRest();
            }
        }

        void ApplyRest()
        {
            if (_pulsing) return;
            _path.Spark = -1f;
            bool hoverable = _hover && _board.Host != null && _board.Host.CanPlaceProbe;
            _path.BaseColor = hoverable ? Palette.Dim : _probed ? Palette.Faint : Palette.Outline;
            _path.BaseWidth = hoverable ? 2f : 1.5f;
            if (_celebrate)
            {
                _path.LitAlpha = 1f;
                _path.Glow = 0.42f;
                _path.SetLit(0f, 1f);
            }
            else if (_probed)
            {
                _path.LitAlpha = 0.55f;
                _path.Glow = 0.2f;
                _path.SetLit(0f, 1f);
            }
            else
            {
                _path.SetLit(0f, 0f);
            }
            _flow.enabled = _celebrate;
            _flow.Animate = !App.ReduceMotion;
            _flow.SetVerticesDirty();
        }
    }

    /// <summary>
    /// The clickable part of a wire. Its rect covers the wire's bounds, and the
    /// raycast filter only lets through points within a few pixels of the line,
    /// so crossing wires each get their own clicks.
    /// </summary>
    public sealed class WireHit : MonoBehaviour, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public WireView Wire;
        const float Reach = 11f;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (Wire == null || !Wire.ScreenToLocal(screenPoint, eventCamera, out var local)) return false;
            return Wire.Project(local, out _) <= Reach;
        }

        public void OnPointerEnter(PointerEventData e) => Wire.SetHover(true);
        public void OnPointerExit(PointerEventData e) => Wire.SetHover(false);

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (Wire.ScreenToLocal(e.position, e.pressEventCamera, out var local)) Wire.Clicked(local);
        }
    }

    /// <summary>
    /// A probe: a ring on a wire and a small card above it reading the signal the
    /// wire carried on the latest run (a ? before one, or after a change). Clicking
    /// the card plays the signal; clicking the ring, or right-clicking either,
    /// takes the probe off.
    /// </summary>
    public sealed class ProbeView : MonoBehaviour
    {
        public int Index;
        ShapeGraphic _ring, _card;
        ValueView _value;
        PathGraphic _stem;
        bool _solved;
        float _flash;

        public static ProbeView Create(RectTransform layer, int index, WireView wire, float at, Medium medium, BoardView board)
        {
            Vector2 p = wire.PointAt(at);
            var rt = UIKit.Rect(layer, "Probe " + (index + 1)).Centered(p.x, p.y, 0f, 0f);
            var view = rt.gameObject.AddComponent<ProbeView>();
            view.Index = index;
            var host = board.Host;

            float valueSize = medium == Medium.Wave ? 22f : 24f;
            float valueW = ValueView.WidthFor(medium, valueSize);
            float cardW = valueW + 26f, cardH = 36f, lift = 40f;

            view._stem = UIKit.Add<PathGraphic>(rt, "Stem");
            view._stem.raycastTarget = false;
            view._stem.rectTransform.Centered(0f, 0f, 4f, 4f);
            view._stem.BaseColor = Palette.Faint;
            view._stem.BaseWidth = 1f;
            view._stem.SetPoints(new List<Vector2> { new Vector2(0f, 8f), new Vector2(0f, lift - cardH * 0.5f) });

            var cardRt = UIKit.Rect(rt, "Card").Centered(0f, lift, cardW, cardH);
            view._card = UIKit.Shape(cardRt, "Frame");
            view._card.rectTransform.Stretch();
            view._card.Radius = 11f;
            view._value = ValueView.Create(cardRt, medium, valueSize);
            UIKit.Hit(cardRt).rectTransform.Stretch();
            var cardClick = cardRt.gameObject.AddComponent<Clickable>();
            cardClick.HoverSound = false;
            cardClick.ClickSound = Sfx.Hover;
            // The card plays what the probe reads; the dot on the wire takes the probe off.
            cardClick.Clicked = () =>
            {
                if (!view._value.Known) return;
                host?.PlayValue(view._value.Value);
                view._value.Ping();
            };
            cardClick.RightClicked = () => host?.ProbeClicked(view.Index);
            cardClick.HoverChanged = on => { view._card.StrokeColor = on ? Palette.Dim : Palette.Faint; host?.Hover(BoardItem.Probe, view.Index, on); };

            var ringRt = UIKit.Rect(rt, "Ring").Centered(0f, 0f, 24f, 24f);
            view._ring = UIKit.Shape(ringRt, "Dot", ShapeKind.Circle);
            view._ring.rectTransform.Centered(0f, 0f, 13f, 13f);
            UIKit.Hit(ringRt).rectTransform.Stretch();
            var ringClick = ringRt.gameObject.AddComponent<Clickable>();
            ringClick.HoverSound = false;
            ringClick.ClickSound = Sfx.Close;
            ringClick.Clicked = () => host?.ProbeClicked(view.Index);
            ringClick.RightClicked = () => host?.ProbeClicked(view.Index);
            ringClick.HoverChanged = on => host?.Hover(BoardItem.Probe, view.Index, on);

            view.Restyle();
            if (!App.ReduceMotion)
            {
                rt.localScale = Vector3.one * 0.6f;
                Tweener.Run(view, "appear", 0.25f, t => rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t), Ease.OutBack);
            }
            return view;
        }

        /// <summary>Lights the probe's value while it sounds.</summary>
        public void Ping() => _value.Ping();

        /// <summary>Shows the probe's reading, or -1 for a ? when there isn't one yet.</summary>
        public void SetValue(int value, bool flash)
        {
            bool changed = value != _value.Value;
            _value.Set(value, ValueLook.Live);
            if (flash && changed && !App.ReduceMotion)
                Tweener.Run(this, "flash", 0.5f, t => { _flash = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public bool Solved { get => _solved; set { _solved = value; Restyle(); } }

        void Restyle()
        {
            _ring.Style(Paint.Solid, Color.black, Paint.Signal, Color.white, 2f);
            _ring.Glow = 0.5f + 0.4f * _flash;
            _ring.GlowPaint = Paint.Signal;
            _ring.GlowRadius = 12f;
            _card.Style(Paint.Solid, Color.black, _solved ? Paint.Signal : Paint.Solid, _solved ? Color.white : Color.Lerp(Palette.Faint, Palette.Text, _flash), 1.2f);
            _card.Glow = _solved ? 0.2f : 0.15f * _flash;
            _card.GlowPaint = Paint.Signal;
            _card.GlowRadius = 14f;
        }
    }
}
