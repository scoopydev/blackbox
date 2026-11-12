using System;
using System.Collections.Generic;
using Blackbox.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blackbox.UI
{
    /// <summary>
    /// Pointer events for anything clickable. Needs a raycast-target graphic on the
    /// same object (a <see cref="HitArea"/> or a shape). Visuals are left to the owner.
    /// </summary>
    public sealed class Clickable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        public Action Clicked;
        public Action RightClicked;
        /// <summary>Scroll wheel: +1 up, -1 down.</summary>
        public Action<int> Scrolled;
        public Action<bool> HoverChanged;
        public Action<bool> PressChanged;
        public bool Interactable = true;
        public bool HoverSound = true;
        public Sfx ClickSound = Sfx.Click;

        public bool Hovered { get; private set; }
        public bool Pressed { get; private set; }

        public void OnPointerEnter(PointerEventData e)
        {
            Hovered = true;
            HoverChanged?.Invoke(true);
            if (Interactable && HoverSound) App.Sound(Sfx.Hover);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Hovered = false;
            if (Pressed) { Pressed = false; PressChanged?.Invoke(false); }
            HoverChanged?.Invoke(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Interactable || e.button != PointerEventData.InputButton.Left) return;
            Pressed = true;
            PressChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Pressed) return;
            Pressed = false;
            PressChanged?.Invoke(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            if (e.button == PointerEventData.InputButton.Left)
            {
                if (Clicked == null) return;
                if (ClickSound != Sfx.Hover) App.Sound(ClickSound);
                Clicked();
            }
            else if (e.button == PointerEventData.InputButton.Right)
            {
                RightClicked?.Invoke();
            }
        }

        public void OnScroll(PointerEventData e)
        {
            if (!Interactable || Scrolled == null) return;
            float dy = e.scrollDelta.y;
            if (Mathf.Abs(dy) < 0.01f) return;
            Scrolled(dy > 0 ? 1 : -1);
        }

        void OnDisable()
        {
            Hovered = false;
            Pressed = false;
        }
    }

    /// <summary>Static access to app services that widgets need, so they don't hold references.</summary>
    public static class App
    {
        public static AudioSystem Audio;
        public static bool ReduceMotion;
        /// <summary>How many times faster than normal the signals on screen loop (1 to 4).</summary>
        public static float LoopSpeed = 1f;
        /// <summary>How many times faster than normal a run plays out (1 to 4).</summary>
        public static float RunSpeed = 1f;

        public static void Sound(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (Audio != null) Audio.Play(sfx, pitch, volume);
        }
    }

    /// <summary>
    /// A text menu entry with the Void focus mark: a small pulsing dot to its left.
    /// </summary>
    public sealed class MenuItem : MonoBehaviour
    {
        public VoidText Label;
        public ShapeGraphic Dot;
        public Clickable Click;
        public Action Activated;
        bool _focused;
        bool _enabled = true;
        float _pulse;

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Click.Interactable = value; Restyle(); }
        }

        public static MenuItem Create(Transform parent, string text, float x, float y, float width = 520f, TextStyle? style = null)
        {
            var rt = UIKit.Rect(parent, "Menu " + text).At(x, y, width, 46f);
            var item = rt.gameObject.AddComponent<MenuItem>();
            UIKit.Hit(rt).rectTransform.Stretch();
            item.Click = rt.gameObject.AddComponent<Clickable>();
            var s = style ?? TextStyle.Menu;
            item.Label = UIKit.Text(rt, text, s);
            item.Label.rectTransform.Stretch();
            item.Dot = UIKit.Shape(rt, "Focus", ShapeKind.Circle);
            item.Dot.Style(Paint.Solid, Palette.Cyan, Paint.None, Color.clear);
            item.Dot.GlowPaint = Paint.Solid;
            item.Dot.GlowColor = Palette.Cyan;
            item.Dot.Glow = 0.5f;
            item.Dot.GlowRadius = 10f;
            item.Click.Clicked = () => item.Activated?.Invoke();
            item.Restyle();
            return item;
        }

        public void SetText(string text)
        {
            Label.text = text;
            Restyle();
        }

        public bool Focused
        {
            get => _focused;
            set { if (_focused != value) { _focused = value; Restyle(); } }
        }

        void Restyle()
        {
            if (Label == null) return;
            Label.color = !_enabled ? Palette.Faint : _focused ? Palette.Text : Palette.Dim;
            float w = Label.MeasureWidth();
            Dot.rectTransform.Centered(-w * 0.5f - 34f, 0f, 7f, 7f);
            Dot.enabled = _focused && _enabled;
        }

        void Update()
        {
            if (!_focused || Dot == null) return;
            if (App.ReduceMotion) { Dot.canvasRenderer.SetAlpha(1f); return; }
            _pulse += Time.unscaledDeltaTime;
            Dot.canvasRenderer.SetAlpha(0.45f + 0.55f * (0.5f + 0.5f * Mathf.Cos(_pulse * Mathf.PI * 2f / 2.4f)));
        }
    }

    /// <summary>A vertical list of <see cref="MenuItem"/>s with keyboard focus.</summary>
    public sealed class MenuList
    {
        public readonly List<MenuItem> Items = new List<MenuItem>();
        int _focus;

        public MenuItem Add(MenuItem item, Action action)
        {
            item.Activated = action;
            int index = Items.Count;
            Items.Add(item);
            item.Click.HoverChanged += h => { if (h && item.Enabled) Focus(index); };
            if (Items.Count == 1) Focus(0);
            return item;
        }

        public void Focus(int index)
        {
            if (Items.Count == 0) return;
            _focus = Mathf.Clamp(index, 0, Items.Count - 1);
            for (int i = 0; i < Items.Count; i++) Items[i].Focused = i == _focus;
        }

        /// <summary>Arrow keys move, Enter or Space activates.</summary>
        public void HandleKeys()
        {
            if (Items.Count == 0) return;
            if (GameInput.Down(Key.Down)) Move(1);
            else if (GameInput.Down(Key.Up)) Move(-1);
            else if (GameInput.Confirm)
            {
                var item = Items[_focus];
                if (item.Enabled && item.Activated != null)
                {
                    App.Sound(Sfx.Click);
                    item.Activated();
                }
            }
        }

        void Move(int dir)
        {
            for (int step = 1; step <= Items.Count; step++)
            {
                int i = (_focus + dir * step + Items.Count * 4) % Items.Count;
                if (!Items[i].Enabled) continue;
                Focus(i);
                App.Sound(Sfx.Hover);
                return;
            }
        }
    }

    /// <summary>A pill-shaped button: the gradient RUN style, or a quiet outline.</summary>
    public sealed class PillButton : MonoBehaviour
    {
        public ShapeGraphic Shape;
        public VoidText Label;
        public GlyphGraphic Icon;
        public Clickable Click;
        bool _primary;
        bool _active;
        bool _enabled = true;
        float _attention;
        float _phase;

        public static PillButton Create(Transform parent, string text, float x, float y, float w, float h, bool primary, string icon = null)
        {
            var rt = UIKit.Rect(parent, "Button " + text).At(x, y, w, h);
            var b = rt.gameObject.AddComponent<PillButton>();
            b._primary = primary;
            b.Shape = UIKit.Shape(rt, "Pill", ShapeKind.Pill);
            b.Shape.rectTransform.Stretch();
            b.Shape.raycastTarget = true;
            b.Click = rt.gameObject.AddComponent<Clickable>();
            var style = primary ? new TextStyle(600, 18, 0.32f, true, Color.black) : new TextStyle(400, 17, 0.3f, true, Palette.Dim);
            b.Label = UIKit.Text(rt, text, style);
            b.Label.rectTransform.Stretch();
            if (icon != null)
            {
                b.Icon = UIKit.Glyph(rt, icon, primary ? Color.black : Palette.Dim, 1.8f);
                float tw = b.Label.MeasureWidth();
                b.Icon.rectTransform.Centered(tw * 0.5f + 6f, 0f, 16f, 16f);
                b.Label.rectTransform.offsetMin = new Vector2(-14f, 0f);
                b.Label.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }
            b.Click.HoverChanged += _ => b.Restyle();
            b.Click.PressChanged += _ => b.Restyle();
            b.Restyle();
            return b;
        }

        public void SetText(string text)
        {
            Label.text = text;
            if (Icon != null) Icon.rectTransform.Centered(Label.MeasureWidth() * 0.5f + 6f, 0f, 16f, 16f);
        }

        /// <summary>Outline buttons can be toggled on, like Probe.</summary>
        public bool Active { get => _active; set { _active = value; Restyle(); } }

        public bool Enabled { get => _enabled; set { _enabled = value; Click.Interactable = value; Restyle(); } }

        /// <summary>A slow glow that invites a press, used on Run when the circuit has changed.</summary>
        public float Attention { get => _attention; set { _attention = value; Restyle(); } }

        void Restyle()
        {
            if (Shape == null) return;
            bool hot = Click.Hovered && _enabled;
            if (_primary)
            {
                Shape.Style(_enabled ? Paint.Signal : Paint.Solid, _enabled ? Color.white : Palette.Outline, Paint.None, Color.clear);
                Shape.Glow = !_enabled ? 0f : Click.Pressed ? 0.2f : hot ? 0.55f : 0.25f + _attention * 0.25f;
                Shape.GlowRadius = 20f;
                Label.color = _enabled ? Color.black : Palette.Faint;
                if (Icon != null) Icon.Tint = Label.color;
            }
            else
            {
                Paint stroke = _active ? Paint.Signal : Paint.Solid;
                Color strokeColor = !_enabled ? Palette.Outline : hot ? Palette.Dim : Palette.Faint;
                Shape.Style(Paint.Solid, Color.black, stroke, _active ? Color.white : strokeColor, _active ? 1.6f : 1.3f);
                Shape.Glow = _active ? 0.3f : 0f;
                Shape.GlowRadius = 16f;
                Label.color = !_enabled ? Palette.Faint : _active || hot ? Palette.Text : Palette.Dim;
                if (Icon != null) Icon.Tint = Label.color;
            }
        }

        void Update()
        {
            if (!_primary || _attention <= 0f || App.ReduceMotion || !_enabled) return;
            _phase += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_phase * Mathf.PI * 2f / 1.8f);
            Shape.Glow = (Click.Hovered ? 0.55f : 0.25f) + _attention * 0.35f * pulse;
        }
    }

    /// <summary>A thin slider: hairline track, lit portion, round knob.</summary>
    public sealed class VoidSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<float> Changed;
        PathGraphic _track;
        ShapeGraphic _knob;
        RectTransform _rt;
        float _value;
        bool _focused, _hover;

        public static VoidSlider Create(Transform parent, float x, float y, float width)
        {
            var rt = UIKit.Rect(parent, "Slider").At(x, y, width, 36f);
            UIKit.Hit(rt).rectTransform.Stretch();
            var s = rt.gameObject.AddComponent<VoidSlider>();
            s._rt = rt;
            s._track = UIKit.Add<PathGraphic>(rt, "Track");
            s._track.raycastTarget = false;
            s._track.rectTransform.Stretch();
            s._track.BaseColor = Palette.Outline;
            s._track.BaseWidth = 1.5f;
            s._track.LitWidth = 2f;
            s._track.Glow = 0.3f;
            s._track.GlowRadius = 9f;
            s._track.SetPoints(new List<Vector2> { new Vector2(-width * 0.5f, 0f), new Vector2(width * 0.5f, 0f) });
            s._knob = UIKit.Shape(rt, "Knob", ShapeKind.Circle);
            s._knob.Style(Paint.Solid, Color.black, Paint.Solid, Palette.Text, 1.5f);
            return s;
        }

        public float Value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp01(value);
                float w = _rt.rect.width;
                _track.SetLit(0f, Mathf.Max(0.0001f, _value));
                _track.LitAlpha = _value > 0.001f ? 1f : 0f;
                _knob.rectTransform.Centered(-w * 0.5f + w * _value, 0f, 16f, 16f);
            }
        }

        public bool Focused { get => _focused; set { _focused = value; Restyle(); } }

        void Restyle()
        {
            _knob.StrokeColor = _focused || _hover ? Palette.Text : Palette.Dim;
            _knob.Glow = _focused ? 0.35f : 0f;
            _knob.GlowPaint = Paint.Solid;
            _knob.GlowColor = Palette.Text;
            _knob.GlowRadius = 12f;
        }

        public void Nudge(float delta)
        {
            Value = _value + delta;
            Changed?.Invoke(_value);
            App.Sound(Sfx.Tick, 0.8f + _value * 0.6f);
        }

        void SetFromPointer(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local)) return;
            float w = _rt.rect.width;
            float v = Mathf.Clamp01((local.x + w * 0.5f) / w);
            if (Mathf.Abs(v - _value) < 0.001f) return;
            Value = v;
            Changed?.Invoke(_value);
        }

        public void OnPointerDown(PointerEventData e) { SetFromPointer(e); App.Sound(Sfx.Tick); }
        public void OnDrag(PointerEventData e) => SetFromPointer(e);
        public void OnPointerEnter(PointerEventData e) { _hover = true; Restyle(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; Restyle(); }
    }

    /// <summary>An on/off switch. On is lit like a live signal.</summary>
    public sealed class VoidToggle : MonoBehaviour
    {
        public Action<bool> Changed;
        ShapeGraphic _body;
        ShapeGraphic _knob;
        Clickable _click;
        bool _on;
        bool _focused;

        public static VoidToggle Create(Transform parent, float x, float y)
        {
            var rt = UIKit.Rect(parent, "Toggle").At(x, y, 58f, 30f);
            var t = rt.gameObject.AddComponent<VoidToggle>();
            t._body = UIKit.Shape(rt, "Body", ShapeKind.Pill);
            t._body.rectTransform.Stretch();
            t._body.raycastTarget = true;
            t._knob = UIKit.Shape(rt, "Knob", ShapeKind.Circle);
            t._click = rt.gameObject.AddComponent<Clickable>();
            t._click.ClickSound = Sfx.Dial;
            t._click.Clicked = () => t.Flip();
            t._click.HoverChanged += _ => t.Restyle();
            t.Restyle();
            return t;
        }

        public bool On { get => _on; set { _on = value; Restyle(); } }
        public bool Focused { get => _focused; set { _focused = value; Restyle(); } }

        public void Flip()
        {
            On = !_on;
            Changed?.Invoke(_on);
        }

        void Restyle()
        {
            if (_body == null) return;
            bool hot = _click.Hovered || _focused;
            _body.Style(Paint.Solid, Color.black, _on ? Paint.Signal : Paint.Solid, _on ? Color.white : hot ? Palette.Dim : Palette.Faint, 1.4f);
            _body.Glow = _on ? 0.3f : 0f;
            _body.GlowRadius = 14f;
            _knob.Style(_on ? Paint.Signal : Paint.Solid, _on ? Color.white : hot ? Palette.Dim : Palette.Faint, Paint.None, Color.clear);
            _knob.rectTransform.Centered(_on ? 14f : -14f, 0f, 16f, 16f);
        }
    }
}
