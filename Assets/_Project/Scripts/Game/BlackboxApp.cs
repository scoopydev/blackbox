using System;
using Blackbox.Core;
using Blackbox.Puzzles;
using Blackbox.UI;
using Blackbox.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blackbox.Game
{
    /// <summary>
    /// The whole game in one object: it loads the circuits and the save, sets up
    /// the camera, canvas, input and sound, and moves between screens. Every
    /// screen is built in code, so an empty scene with this component (or no
    /// scene object at all, see <see cref="GameBootstrap"/>) is enough to play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackboxApp : MonoBehaviour
    {
        public static BlackboxApp Instance { get; private set; }

        public ContentLibrary Library { get; private set; }
        public SaveData Save { get; private set; }
        public Progress Progress { get; private set; }
        public AudioSystem Audio { get; private set; }
        public ScreenStack Screens { get; private set; }

        /// <summary>Set when the puzzle files couldn't be read; the game shows it instead of the title.</summary>
        public string ContentError { get; private set; }

        float _saveAt = -1f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadContent();
            Save = SaveStore.Load();
            Progress = new Progress(Library, Save);

            SetUpCamera();
            SetUpEventSystem();
            Screens = new ScreenStack(SetUpCanvas());

            Audio = gameObject.AddComponent<AudioSystem>();
            Audio.Init();
            App.Audio = Audio;
            ApplyPrefs(false);
        }

        void Start()
        {
            Audio.StartMusic();
            if (ContentError != null)
            {
                var error = Screens.Create<ErrorScreen>();
                error.Message = ContentError;
                Screens.Replace(error);
                return;
            }
            GoTitle();
        }

        void Update()
        {
            GameInput.Poll();
            Screens.Update();
            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt) SaveNow();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && _saveAt >= 0f) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (_saveAt >= 0f) SaveNow();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ setup

        void LoadContent()
        {
            try
            {
                Library = ContentLibrary.Load(name =>
                {
                    var asset = Resources.Load<TextAsset>("BlackboxPuzzles/" + name);
                    return asset != null ? asset.text : null;
                });
                Library.CheckCircuits();
            }
            catch (Exception e)
            {
                ContentError = e.Message;
                Library = new ContentLibrary();
                Debug.LogError("BLACKBOX: " + e.Message);
            }
        }

        static void SetUpCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            if (cam.GetComponent<AudioListener>() == null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        static void SetUpEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(go);
        }

        RectTransform SetUpCanvas()
        {
            var go = new GameObject("BLACKBOX Interface", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(VoidFrame.Width, VoidFrame.Height);
            // Expand keeps the whole 1920 x 1080 frame on screen at any aspect ratio.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();

            var frame = UIKit.Rect(go.transform, "Frame").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            VoidFrame.Current = frame;
            return frame;
        }

        // ------------------------------------------------------------------ preferences and saving

        /// <summary>Applies the preferences in the save: sound levels, motion and the window.</summary>
        public void ApplyPrefs(bool save = true)
        {
            var p = Save.Prefs;
            App.ReduceMotion = p.ReduceMotion;
            App.LoopSpeed = p.LoopSpeed;
            App.RunSpeed = p.RunSpeed;
            Audio.SetVolumes(p.MasterVolume, p.MusicVolume, p.EffectsVolume);
#if !UNITY_EDITOR && (UNITY_STANDALONE || UNITY_WSA)
            var mode = p.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode)
            {
                if (p.Fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, mode);
                else Screen.SetResolution(Mathf.RoundToInt(Display.main.systemWidth * 0.75f), Mathf.RoundToInt(Display.main.systemHeight * 0.75f), mode);
            }
#endif
            if (save) MarkDirty();
        }

        /// <summary>Saves a moment from now, so a burst of changes is written once.</summary>
        public void MarkDirty()
        {
            if (_saveAt < 0f) _saveAt = Time.unscaledTime + 0.75f;
        }

        public void SaveNow()
        {
            _saveAt = -1f;
            SaveStore.Save(Save);
        }

        public void EraseProgress()
        {
            Save.ResetProgress();
            SaveNow();
            GoTitle();
        }

        // ------------------------------------------------------------------ navigation

        public void GoTitle()
        {
            Screens.Replace(Screens.Create<TitleScreen>());
        }

        /// <summary>The chapter map, open on <paramref name="chapter"/> or wherever the player is up to.</summary>
        public void GoChapters(ChapterDef chapter)
        {
            var screen = Screens.Create<ChapterScreen>();
            screen.Chapter = chapter;
            Screens.Replace(screen);
        }

        public void GoPuzzle(PuzzleDef puzzle)
        {
            if (puzzle == null)
            {
                GoChapters(null);
                return;
            }
            Save.LastPuzzle = puzzle.Id;
            MarkDirty();
            if (puzzle.Wiring)
            {
                var wiring = Screens.Create<WiringScreen>();
                wiring.Puzzle = puzzle;
                Screens.Replace(wiring);
            }
            else
            {
                var screen = Screens.Create<PuzzleScreen>();
                screen.Puzzle = puzzle;
                Screens.Replace(screen);
            }

            // The first visit to a chapter opens with its title card.
            var chapter = puzzle.Chapter;
            if (chapter != null && !Save.SeenIntros.Contains(chapter.Id))
            {
                Save.SeenIntros.Add(chapter.Id);
                var intro = Screens.Create<ChapterIntroOverlay>();
                intro.Chapter = chapter;
                Screens.Push(intro);
            }
        }

        public void OpenSettings()
        {
            Screens.Push(Screens.Create<SettingsOverlay>());
        }

        public void Confirm(string question, string yes, bool danger, Action onYes)
        {
            var c = Screens.Create<ConfirmOverlay>();
            c.Question = question;
            c.YesText = yes;
            c.Danger = danger;
            c.Yes = onYes;
            Screens.Push(c);
        }

        public void Quit()
        {
            SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Starts the game in any scene: if nothing in the scene has a
    /// <see cref="BlackboxApp"/>, one is created when the scene loads.
    /// </summary>
    static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (BlackboxApp.Instance != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<BlackboxApp>() != null) return;
            new GameObject("BLACKBOX").AddComponent<BlackboxApp>();
        }
    }
}
