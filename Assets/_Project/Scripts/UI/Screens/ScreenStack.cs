using System;
using System.Collections.Generic;
using Blackbox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI.Screens
{
    /// <summary>
    /// A full-frame page of the interface: the title, the chapter map, a circuit,
    /// or an overlay such as Pause. Only the screen on top receives keys.
    /// </summary>
    public abstract class GameScreen : MonoBehaviour
    {
        public RectTransform Root => (RectTransform)transform;
        public CanvasGroup Group { get; private set; }
        protected static BlackboxApp Game => BlackboxApp.Instance;
        internal bool Built;

        /// <summary>Overlays sit above another screen instead of replacing it.</summary>
        public virtual bool IsOverlay => false;

        public static T Create<T>(Transform frame) where T : GameScreen
        {
            var rt = UIKit.Rect(frame, typeof(T).Name).Stretch();
            var screen = rt.gameObject.AddComponent<T>();
            screen.Group = rt.gameObject.AddComponent<CanvasGroup>();
            screen.Group.alpha = 0f;
            return screen;
        }

        /// <summary>Lays out the screen. Called once, just before it first appears.</summary>
        protected internal abstract void Build();

        /// <summary>Called every frame while this screen is on top and no transition is running.</summary>
        public virtual void HandleInput() { }

        /// <summary>This screen is on top again after an overlay closed.</summary>
        public virtual void OnUncovered() { }

        /// <summary>This screen is about to go away.</summary>
        public virtual void OnClosing() { }
    }

    /// <summary>
    /// A screen drawn over the one beneath it, behind a dark veil that swallows clicks.
    /// </summary>
    public abstract class Overlay : GameScreen
    {
        public override bool IsOverlay => true;
        protected Image Veil;

        /// <summary>How dark the veil is. The board stays faintly visible behind Pause.</summary>
        protected virtual float VeilAlpha => 0.9f;

        protected void BuildVeil()
        {
            Veil = UIKit.Add<Image>(Root, "Veil");
            Veil.rectTransform.anchorMin = Vector2.zero;
            Veil.rectTransform.anchorMax = Vector2.one;
            // Reach past the frame so wide screens are covered too.
            Veil.rectTransform.offsetMin = new Vector2(-2000f, -2000f);
            Veil.rectTransform.offsetMax = new Vector2(2000f, 2000f);
            Veil.color = new Color(0f, 0f, 0f, VeilAlpha);
            Veil.raycastTarget = true;
        }

        /// <summary>Closes this overlay.</summary>
        public void Close() => Game.Screens.Pop(this);
    }

    /// <summary>The stack of screens and the fades between them.</summary>
    public sealed class ScreenStack
    {
        readonly RectTransform _frame;
        readonly List<GameScreen> _stack = new List<GameScreen>();
        float _busyUntil;

        const float FadeOut = 0.22f;
        const float FadeIn = 0.4f;
        const float OverlayFade = 0.16f;

        public ScreenStack(RectTransform frame)
        {
            _frame = frame;
        }

        public GameScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public RectTransform Frame => _frame;
        public bool Busy => Time.unscaledTime < _busyUntil;

        public T Find<T>() where T : GameScreen
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i] is T t) return t;
            return null;
        }

        public T Create<T>() where T : GameScreen => GameScreen.Create<T>(_frame);

        /// <summary>Fades everything out, then fades <paramref name="next"/> in.</summary>
        public void Replace(GameScreen next)
        {
            var old = new List<GameScreen>(_stack);
            _stack.Clear();
            _stack.Add(next);
            next.gameObject.SetActive(false);
            float wait = old.Count > 0 ? FadeOut : 0f;
            _busyUntil = Time.unscaledTime + wait + 0.12f;

            foreach (var s in old)
            {
                if (s == null) continue;
                Tweener.Kill(s, "show");
                if (s.Built) s.OnClosing();
                s.Group.blocksRaycasts = false;
                var screen = s;
                float from = s.Group.alpha;
                Tweener.Run(screen, "fade", FadeOut, t => screen.Group.alpha = from * (1f - t), Ease.OutQuad, 0f, () => UnityEngine.Object.Destroy(screen.gameObject));
            }

            Tweener.After(next, "show", wait, () =>
            {
                if (next == null) return;
                next.gameObject.SetActive(true);
                Ensure(next);
                next.Group.alpha = 0f;
                // An overlay may already be waiting above it, such as a chapter's title card.
                bool onTop = Top == next;
                next.Group.blocksRaycasts = onTop;
                next.Group.interactable = onTop;
                Tweener.Run(next, "fade", FadeIn, t => next.Group.alpha = t, Ease.OutCubic);
            });
        }

        /// <summary>Opens an overlay above the current screen.</summary>
        public void Push(GameScreen overlay)
        {
            var below = Top;
            if (below != null)
            {
                below.Group.blocksRaycasts = false;
                below.Group.interactable = false;
            }
            _stack.Add(overlay);
            overlay.transform.SetAsLastSibling();
            Ensure(overlay);
            overlay.Group.alpha = 0f;
            overlay.Group.blocksRaycasts = true;
            _busyUntil = Mathf.Max(_busyUntil, Time.unscaledTime + 0.05f);
            Tweener.Run(overlay, "fade", OverlayFade, t => overlay.Group.alpha = t, Ease.OutQuad);
        }

        /// <summary>Closes an overlay and hands the keys back to the screen beneath.</summary>
        public void Pop(GameScreen overlay)
        {
            int index = _stack.IndexOf(overlay);
            if (index < 0) return;
            _stack.RemoveAt(index);
            if (overlay.Built) overlay.OnClosing();
            overlay.Group.blocksRaycasts = false;
            float from = overlay.Group.alpha;
            Tweener.Run(overlay, "fade", OverlayFade, t => overlay.Group.alpha = from * (1f - t), Ease.OutQuad, 0f, () => UnityEngine.Object.Destroy(overlay.gameObject));
            _busyUntil = Mathf.Max(_busyUntil, Time.unscaledTime + 0.05f);

            var top = Top;
            if (top != null && index == _stack.Count)
            {
                top.Group.blocksRaycasts = true;
                top.Group.interactable = true;
                if (top.Built) top.OnUncovered();
            }
        }

        public void Update()
        {
            if (Busy) return;
            var top = Top;
            if (top == null || !top.Built || !top.gameObject.activeInHierarchy) return;
            try { top.HandleInput(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Ensure(GameScreen s)
        {
            if (s.Built) return;
            s.Built = true;
            s.Build();
        }
    }
}
