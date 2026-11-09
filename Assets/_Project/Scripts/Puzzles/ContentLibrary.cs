using System;
using System.Collections.Generic;
using Blackbox.Core;

namespace Blackbox.Puzzles
{
    /// <summary>All chapters in play order.</summary>
    public sealed class ContentLibrary
    {
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        readonly Dictionary<string, PuzzleDef> _puzzles = new Dictionary<string, PuzzleDef>();

        /// <summary>
        /// Loads the index and every chapter it lists.
        /// <paramref name="readText"/> returns a file's text by name (without extension).
        /// </summary>
        public static ContentLibrary Load(Func<string, string> readText)
        {
            var library = new ContentLibrary();
            var index = PuzzleLoader.ReadIndex(readText("chapters") ?? throw new PuzzleFormatException("chapters.json is missing."));
            foreach (var name in index)
            {
                string text = readText(name) ?? throw new PuzzleFormatException($"{name}.json is listed in chapters.json but missing.");
                library.Add(PuzzleLoader.ReadChapter(text, name + ".json", library.Chapters.Count));
            }
            return library;
        }

        public void Add(ChapterDef chapter)
        {
            foreach (var p in chapter.Puzzles)
            {
                if (_puzzles.ContainsKey(p.Id)) throw new PuzzleFormatException($"Two circuits use the id '{p.Id}'.");
                _puzzles.Add(p.Id, p);
            }
            Chapters.Add(chapter);
        }

        public PuzzleDef FindPuzzle(string id) => id != null && _puzzles.TryGetValue(id, out var p) ? p : null;

        public ChapterDef FindChapter(string id) => Chapters.Find(c => c.Id == id);

        public IEnumerable<PuzzleDef> AllPuzzles
        {
            get
            {
                foreach (var c in Chapters)
                    foreach (var p in c.Puzzles) yield return p;
            }
        }
    }

    /// <summary>
    /// Unlock rules. Circuits open one at a time, but once the first circuit of a
    /// chapter is solved there is always one spare open, so a player who is stuck
    /// can try the next one. A chapter opens after enough circuits of the one before.
    /// </summary>
    public sealed class Progress
    {
        readonly ContentLibrary _library;
        readonly SaveData _save;

        public Progress(ContentLibrary library, SaveData save)
        {
            _library = library;
            _save = save;
        }

        public bool IsSolved(PuzzleDef p) => _save.Solved.Contains(p.Id);

        public int SolvedIn(ChapterDef chapter)
        {
            int n = 0;
            foreach (var p in chapter.Puzzles) if (IsSolved(p)) n++;
            return n;
        }

        public int TotalSolved
        {
            get
            {
                int n = 0;
                foreach (var c in _library.Chapters) n += SolvedIn(c);
                return n;
            }
        }

        public bool IsUnlocked(ChapterDef chapter)
        {
            if (string.IsNullOrEmpty(chapter.UnlockAfter)) return true;
            var before = _library.FindChapter(chapter.UnlockAfter);
            if (before == null) return true;
            return IsUnlocked(before) && SolvedIn(before) >= Math.Min(chapter.UnlockSolved, before.Puzzles.Count);
        }

        public bool IsUnlocked(PuzzleDef p)
        {
            if (!IsUnlocked(p.Chapter)) return false;
            if (IsSolved(p) || p.Number == 0) return true;
            // With k circuits solved, circuits up to number k are open, plus one spare.
            int solved = SolvedIn(p.Chapter);
            int spare = solved > 0 ? 1 : 0;
            return p.Number <= solved + spare;
        }

        /// <summary>The circuit the chapter screen highlights: the first open one not yet solved.</summary>
        public PuzzleDef CurrentIn(ChapterDef chapter)
        {
            foreach (var p in chapter.Puzzles)
                if (!IsSolved(p) && IsUnlocked(p)) return p;
            return null;
        }

        /// <summary>The next circuit after <paramref name="p"/> that is open, in this chapter or the next.</summary>
        public PuzzleDef NextAfter(PuzzleDef p)
        {
            var list = p.Chapter.Puzzles;
            for (int i = p.Number + 1; i < list.Count; i++)
                if (IsUnlocked(list[i]) && !IsSolved(list[i])) return list[i];
            int c = p.Chapter.Number + 1;
            if (c < _library.Chapters.Count && IsUnlocked(_library.Chapters[c]))
                return CurrentIn(_library.Chapters[c]) ?? _library.Chapters[c].Puzzles[0];
            for (int i = 0; i < list.Count; i++)
                if (IsUnlocked(list[i]) && !IsSolved(list[i])) return list[i];
            return null;
        }

        /// <summary>Where "Continue" on the title screen goes.</summary>
        public PuzzleDef ContinueTarget()
        {
            var last = _library.FindPuzzle(_save.LastPuzzle);
            if (last != null && !IsSolved(last) && IsUnlocked(last)) return last;
            if (last != null)
            {
                var next = NextAfter(last);
                if (next != null) return next;
            }
            for (int c = _library.Chapters.Count - 1; c >= 0; c--)
            {
                var chapter = _library.Chapters[c];
                if (!IsUnlocked(chapter)) continue;
                var current = CurrentIn(chapter);
                if (current != null) return current;
            }
            return last ?? (_library.Chapters.Count > 0 ? _library.Chapters[0].Puzzles[0] : null);
        }

        /// <summary>The chapter a new visit to the chapter screen should open on.</summary>
        public ChapterDef CurrentChapter()
        {
            var target = ContinueTarget();
            return target?.Chapter ?? (_library.Chapters.Count > 0 ? _library.Chapters[0] : null);
        }
    }
}
