using System;
using System.Collections.Generic;
using Blackbox.Core;

namespace Blackbox.Puzzles
{
    /// <summary>All chapters in play order.</summary>
    public sealed class ContentLibrary
    {
        /// <summary>Every chapter in play order, across all parts.</summary>
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        public readonly List<PartDef> Parts = new List<PartDef>();
        readonly Dictionary<string, PuzzleDef> _puzzles = new Dictionary<string, PuzzleDef>();

        /// <summary>
        /// Loads the index and every chapter it lists.
        /// <paramref name="readText"/> returns a file's text by name (without extension).
        /// </summary>
        public static ContentLibrary Load(Func<string, string> readText)
        {
            var library = new ContentLibrary();
            var parts = PuzzleLoader.ReadParts(readText("chapters") ?? throw new PuzzleFormatException("chapters.json is missing."));
            foreach (var (part, files) in parts)
            {
                library.Parts.Add(part);
                foreach (var name in files)
                {
                    string text = readText(name) ?? throw new PuzzleFormatException($"{name}.json is listed in chapters.json but missing.");
                    var chapter = PuzzleLoader.ReadChapter(text, name + ".json", library.Chapters.Count);
                    chapter.Part = part;
                    part.Chapters.Add(chapter);
                    library.Add(chapter);
                }
            }
            return library;
        }

        public void Add(ChapterDef chapter)
        {
            if (chapter.Part == null)
            {
                if (Parts.Count == 0) Parts.Add(new PartDef { Id = "main", Title = "Circuits", Tagline = string.Empty });
                chapter.Part = Parts[Parts.Count - 1];
                chapter.Part.Chapters.Add(chapter);
            }
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
    /// Unlock rules. Every chapter's first circuit is open, and circuits open one at
    /// a time after it, but once the first circuit of a chapter is solved there is
    /// always one spare open, so a player who is stuck can try the next one. A
    /// chapter can also be made to wait for enough circuits of an earlier one
    /// (<c>unlock</c> in its file). A part of the game can wait for every circuit
    /// of the part before it (<c>afterPrevious</c> in chapters.json): the combining
    /// tutorial waits for the first, and the main game for both. The debug setting
    /// to open everything skips all of it.
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

        /// <summary>Debug: every part, chapter and circuit is open.</summary>
        bool OpenAll => _save.Prefs != null && _save.Prefs.DebugOpenAll;

        public int SolvedIn(PartDef part)
        {
            int n = 0;
            foreach (var c in part.Chapters) n += SolvedIn(c);
            return n;
        }

        public static int CountIn(PartDef part)
        {
            int n = 0;
            foreach (var c in part.Chapters) n += c.Puzzles.Count;
            return n;
        }

        public bool IsComplete(PartDef part) => SolvedIn(part) >= CountIn(part);

        /// <summary>The part before this one, or null for the first.</summary>
        public PartDef PartBefore(PartDef part)
        {
            int i = _library.Parts.IndexOf(part);
            return i > 0 ? _library.Parts[i - 1] : null;
        }

        public bool IsUnlocked(PartDef part)
        {
            if (part == null || OpenAll || !part.AfterPrevious) return true;
            var before = PartBefore(part);
            return before == null || (IsUnlocked(before) && IsComplete(before));
        }

        public bool IsUnlocked(ChapterDef chapter)
        {
            if (!IsUnlocked(chapter.Part)) return false;
            if (OpenAll || string.IsNullOrEmpty(chapter.UnlockAfter)) return true;
            var before = _library.FindChapter(chapter.UnlockAfter);
            if (before == null) return true;
            return IsUnlocked(before) && SolvedIn(before) >= Math.Min(chapter.UnlockSolved, before.Puzzles.Count);
        }

        public bool IsUnlocked(PuzzleDef p)
        {
            if (!IsUnlocked(p.Chapter)) return false;
            if (OpenAll || IsSolved(p) || p.Number == 0) return true;
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
            // A new player, or one who has finished where they were: the earliest
            // chapter with an open circuit still to solve.
            for (int c = 0; c < _library.Chapters.Count; c++)
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
