using System;
using System.IO;
using Blackbox.Components;
using Blackbox.Puzzles;

namespace Blackbox.Tests
{
    /// <summary>Loads the shipped chapter files straight from disk.</summary>
    public static class TestContent
    {
        public static string PuzzleFolder
        {
            get
            {
                var overridden = Environment.GetEnvironmentVariable("BLACKBOX_PUZZLES_DIR");
                if (!string.IsNullOrEmpty(overridden)) return overridden;
#if UNITY_5_3_OR_NEWER
                return Path.Combine(UnityEngine.Application.dataPath, "_Project", "Puzzles", "Resources", "BlackboxPuzzles");
#else
                throw new InvalidOperationException("Set BLACKBOX_PUZZLES_DIR to the puzzle folder.");
#endif
            }
        }

        public static string Read(string name)
        {
            var path = Path.Combine(PuzzleFolder, name + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        static ContentLibrary s_Library;

        public static ContentLibrary Library => s_Library ?? (s_Library = ContentLibrary.Load(Read));

        public static PuzzleDef Parse(string circuitJson, Medium medium = Medium.Binary)
        {
            return PuzzleLoader.ReadPuzzle(Blackbox.Core.JsonValue.Parse(circuitJson), "test", medium);
        }

        /// <summary>Reads one wired circuit, as it would appear in a chapter with "mode": "wiring".</summary>
        public static PuzzleDef ParseWired(string circuitJson, bool marked = true)
        {
            return PuzzleLoader.ReadPuzzle(Blackbox.Core.JsonValue.Parse(circuitJson), "test", Medium.Binary, true, marked);
        }
    }
}
