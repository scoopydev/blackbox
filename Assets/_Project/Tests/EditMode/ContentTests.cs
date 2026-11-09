using System.Collections.Generic;
using System.Linq;
using Blackbox.Core;
using Blackbox.Puzzles;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>Checks every shipped circuit: it loads, it can be solved, and it doesn't start solved.</summary>
    public class ContentTests
    {
        static IEnumerable<PuzzleDef> AllPuzzles() => TestContent.Library.AllPuzzles;

        [Test]
        public void ChaptersLoad()
        {
            var library = TestContent.Library;
            Assert.That(library.Chapters.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var chapter in library.Chapters)
            {
                Assert.That(chapter.Puzzles.Count, Is.GreaterThanOrEqualTo(6), chapter.Title);
                Assert.That(chapter.Tagline, Is.Not.Empty, chapter.Title);
            }
            Assert.That(library.Chapters[0].UnlockAfter, Is.Null.Or.Empty);
            Assert.That(library.Chapters[1].UnlockAfter, Is.EqualTo(library.Chapters[0].Id));
        }

        [TestCaseSource(nameof(AllPuzzles))]
        public void CircuitIsSolvable(PuzzleDef puzzle)
        {
            var report = PuzzleSolver.Analyse(puzzle);
            Assert.That(report.Errors, Is.Empty, report.ToString());
            Assert.That(report.SolutionCount, Is.GreaterThan(0), report.ToString());
        }

        [Test]
        public void CircuitsGetHarder()
        {
            // Within a chapter the number of combinations should broadly grow, so the
            // last circuit can't be brute forced as easily as the first.
            foreach (var chapter in TestContent.Library.Chapters)
            {
                var spaces = chapter.Puzzles.Select(p => PuzzleSolver.Analyse(p).SearchSpace).ToList();
                Assert.That(spaces.Last(), Is.GreaterThan(spaces.First() * 8), $"{chapter.Title}: {string.Join(", ", spaces)}");
            }
        }

        [Test]
        public void ChaptersOnlyUseKindsTheyIntroduceOrInherit()
        {
            // Every kind should first appear in a circuit where it is not locked,
            // so the player gets to turn its dial before having to reason about it.
            var seenFree = new HashSet<string>();
            foreach (var p in TestContent.Library.AllPuzzles)
            {
                foreach (var box in p.Boxes.Where(b => !b.Locked)) seenFree.Add(box.Kind.Id);
                foreach (var box in p.Boxes.Where(b => b.Locked))
                    Assert.That(seenFree, Does.Contain(box.Kind.Id), $"{p.Id}: locked {box.Kind.Id} before the player has used one");
            }
        }

        [Test]
        public void ProgressUnlocksInOrder()
        {
            var library = TestContent.Library;
            var save = new SaveData();
            var progress = new Progress(library, save);
            var first = library.Chapters[0];
            var second = library.Chapters[1];

            Assert.That(progress.IsUnlocked(first.Puzzles[0]), Is.True);
            Assert.That(progress.IsUnlocked(first.Puzzles[1]), Is.False, "only the first circuit is open at the start");
            Assert.That(progress.IsUnlocked(second), Is.False);

            save.RecordSolve(first.Puzzles[0].Id, 3);
            Assert.That(progress.IsUnlocked(first.Puzzles[1]), Is.True);
            Assert.That(progress.IsUnlocked(first.Puzzles[2]), Is.True, "one spare");
            Assert.That(progress.IsUnlocked(first.Puzzles[3]), Is.False);

            for (int i = 0; i < second.UnlockSolved; i++) save.RecordSolve(first.Puzzles[i].Id, 5);
            Assert.That(progress.IsUnlocked(second), Is.True);
            Assert.That(progress.ContinueTarget(), Is.Not.Null);
        }

        /// <summary>
        /// Plays the whole game the way the title screen's Continue does: open the
        /// suggested circuit, set its boxes to a solution, run, record the solve.
        /// Every circuit must be reached, and each one must open in play.
        /// </summary>
        [Test]
        public void ContinueReachesEveryCircuit()
        {
            var library = TestContent.Library;
            var save = new SaveData();
            var progress = new Progress(library, save);
            int total = library.AllPuzzles.Count();

            for (int step = 0; step < total; step++)
            {
                var target = progress.ContinueTarget();
                Assert.That(target, Is.Not.Null, $"Continue had nowhere to go after {step} circuits");
                Assert.That(progress.IsUnlocked(target), Is.True, $"Continue chose {target.Id}, which is locked");
                Assert.That(progress.IsSolved(target), Is.False, $"Continue chose {target.Id}, which is already solved");
                save.LastPuzzle = target.Id;

                var report = PuzzleSolver.Analyse(target, 1);
                var session = new PuzzleSession(target, save.Settings.TryGetValue(target.Id, out var resume) ? resume : null);
                for (int b = 0; b < target.Boxes.Count; b++) session.SetSetting(b, report.Solutions[0][b]);
                var run = session.Run();
                Assert.That(run.AllMatched, Is.True, target.Id);
                Assert.That(session.SolvedNow, Is.True, target.Id);
                save.RecordSolve(target.Id, session.Tests);
                save.Settings[target.Id] = session.CopySettings();
            }

            Assert.That(progress.TotalSolved, Is.EqualTo(total));
            foreach (var chapter in library.Chapters) Assert.That(progress.IsUnlocked(chapter), Is.True, chapter.Title);
        }

        [Test]
        public void SaveRoundTrips()
        {
            var save = new SaveData();
            save.RecordSolve("a", 7);
            save.RecordSolve("a", 4);
            save.RecordSolve("a", 9);
            save.Settings["b"] = new[] { 2, 0, 1 };
            save.SetName("K4", 2, RuleName.FromGlyph("gt"));
            save.SetName("K1", 0, RuleName.FromText("NOT"));
            save.SeenIntros.Add("first-light");
            save.LastPuzzle = "b";
            save.Prefs.MusicVolume = 0.25f;
            save.Prefs.ReduceMotion = true;

            var copy = SaveData.FromJson(save.ToJson());
            Assert.That(copy.Solved, Does.Contain("a"));
            Assert.That(copy.BestTests["a"], Is.EqualTo(4));
            Assert.That(copy.Settings["b"], Is.EqualTo(new[] { 2, 0, 1 }));
            Assert.That(copy.GetName("K4", 2).Glyph, Is.EqualTo("gt"));
            Assert.That(copy.GetName("K1", 0).Text, Is.EqualTo("NOT"));
            Assert.That(copy.GetName("K1", 1).IsEmpty, Is.True);
            Assert.That(copy.SeenIntros, Does.Contain("first-light"));
            Assert.That(copy.LastPuzzle, Is.EqualTo("b"));
            Assert.That(copy.Prefs.MusicVolume, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(copy.Prefs.ReduceMotion, Is.True);
        }

        [Test]
        public void DamagedSaveFallsBackToDefaults()
        {
            var save = SaveData.FromJson(@"{ ""solved"": 5, ""names"": { ""K1:0"": ""??"" }, ""preferences"": { ""masterVolume"": 9 } }");
            Assert.That(save.Solved, Is.Empty);
            Assert.That(save.Names, Is.Empty);
            Assert.That(save.Prefs.MasterVolume, Is.EqualTo(1f));
        }

        [Test]
        public void JsonKeepsKeyOrderAndAllowsComments()
        {
            var v = JsonValue.Parse("{ // note\n \"b\": 1, \"a\": [true, null, \"x\\n\"] }");
            Assert.That(v.Members.Select(m => m.Key), Is.EqualTo(new[] { "b", "a" }));
            Assert.That(v["a"][2].AsString(), Is.EqualTo("x\n"));
            Assert.That(JsonValue.Parse(v.ToString())["b"].AsInt(), Is.EqualTo(1));
        }
    }
}
