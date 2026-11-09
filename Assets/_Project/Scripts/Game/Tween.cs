using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackbox.Game
{
    public enum Ease { Linear, OutQuad, OutCubic, InOutCubic, InCubic, OutBack }

    /// <summary>
    /// A tiny tween runner on unscaled time. Tweens are keyed by owner and channel,
    /// so starting a new tween on the same channel replaces the old one.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        sealed class Item
        {
            public object Owner;
            public string Channel;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public Action<float> Step;
            public Ease Ease;
            public Action Done;
            public bool Dead;
        }

        static Tweener s_Instance;
        readonly List<Item> _items = new List<Item>();
        readonly List<Item> _running = new List<Item>();

        /// <summary>Multiplies every duration. Reduce Motion shortens animations through this.</summary>
        public static float TimeScale = 1f;

        static Tweener Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("Tweener");
                    DontDestroyOnLoad(go);
                    s_Instance = go.AddComponent<Tweener>();
                }
                return s_Instance;
            }
        }

        public static void Run(object owner, string channel, float duration, Action<float> step, Ease ease = Ease.OutCubic, float delay = 0f, Action done = null)
        {
            var inst = Instance;
            inst.KillInternal(owner, channel);
            duration *= TimeScale;
            delay *= TimeScale;
            if (duration <= 0f && delay <= 0f)
            {
                step?.Invoke(1f);
                done?.Invoke();
                return;
            }
            inst._items.Add(new Item { Owner = owner, Channel = channel, Duration = Mathf.Max(0.0001f, duration), Delay = delay, Step = step, Ease = ease, Done = done });
        }

        /// <summary>Calls <paramref name="action"/> after a delay. Replaces any pending call on the same channel.</summary>
        public static void After(object owner, string channel, float delay, Action action)
        {
            Run(owner, channel, 0.0001f, null, Ease.Linear, delay, action);
        }

        public static void Kill(object owner, string channel = null)
        {
            if (s_Instance != null) s_Instance.KillInternal(owner, channel);
        }

        public static bool IsRunning(object owner, string channel)
        {
            if (s_Instance == null) return false;
            foreach (var item in s_Instance._items)
                if (!item.Dead && ReferenceEquals(item.Owner, owner) && item.Channel == channel) return true;
            return false;
        }

        void KillInternal(object owner, string channel)
        {
            foreach (var item in _items)
                if (ReferenceEquals(item.Owner, owner) && (channel == null || item.Channel == channel)) item.Dead = true;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _running.Clear();
            _running.AddRange(_items);
            foreach (var item in _running)
            {
                if (item.Dead) continue;
                if (item.Owner is UnityEngine.Object o && o == null) { item.Dead = true; continue; }
                if (item.Delay > 0f) { item.Delay -= dt; continue; }
                item.Elapsed += dt;
                float t = Mathf.Clamp01(item.Elapsed / item.Duration);
                try { item.Step?.Invoke(Apply(item.Ease, t)); }
                catch (Exception e) { Debug.LogException(e); item.Dead = true; continue; }
                if (t >= 1f)
                {
                    item.Dead = true;
                    try { item.Done?.Invoke(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            _items.RemoveAll(i => i.Dead);
        }

        public static float Apply(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.InCubic: return t * t * t;
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
                default: return t;
            }
        }
    }
}
