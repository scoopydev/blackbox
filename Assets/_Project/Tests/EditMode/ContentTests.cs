using System.Collections.Generic;
using System.Linq;
using Blackbox.Components;
using Blackbox.Core;
using Blackbox.Puzzles;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>
    /// Checks every shipped circuit: it loads, it can be solved, it doesn't start
    /// solved, and its goal can be put into words. Also the save file.
    /// </summary>
    public class ContentTests
    {
        static IEnumerable<PuzzleDef> AllPuzzles() => TestContent.Library.AllPuzzles;

        [Test]
        public void ChaptersLoad()
        {
            var library = TestContent.Library;
            Assert.That(library.Chapters.Count, Is.EqualTo(4));
            foreach (var chapter in library.Chapters)
            {
                Assert.That(chapter.Puzzles.Count, Is.EqualTo(6), chapter.Title);
                Assert.That(chapter.Tagline, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Signals, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Puzzles.All(p => p.Medium == chapter.Medium), Is.True, chapter.Title);
            }
            Assert.That(library.Chapters.Select(c => c.Medium).Distinct().Count(), Is.EqualTo(4), "one chapter for each medium");
            Assert.That(library.Chapters[0].UnlockAfter, Is.Null.Or.Empty);
            for (int i = 1; i < library.Chapters.Count; i++)
                Assert.That(library.Chapters[i].UnlockAfter, Is.EqualTo(library.Chapters[i - 1].Id));
        }

        [TestCaseSource(nameof(AllPuzzles))]
        public void CircuitIsSolvable(PuzzleDef puzzle)
        {
            var report = PuzzleSolver.Analyse(puzzle);
            Assert.That(report.Errors, Is.Empty, report.ToString());
            Assert.That(report.SolutionCount, Is.GreaterThan(0), report.ToString());
            Assert.That(puzzle.Targets.Count, Is.GreaterThan(0));
            Assert.That(report.StartMatched, Is.LessThan(puzzle.Targets.Count), "starts solved");
            Assert.That(report.FewestChanges, Is.GreaterThan(0));
        }

        [TestCaseSource(nameof(AllPuzzles))]
        public void GoalCanBeRead(PuzzleDef puzzle)
        {
            var goal = GoalText.For(puzzle);
            Assert.That(goal, Is.Not.Empty);
            Assert.That(goal, Does.Not.Contain("?"), "every target is a value of the medium");
            if (puzzle.Targets.Count <= GoalText.MaxListed)
                foreach (var t in puzzle.Targets) Assert.That(goal, Does.Contain(t.Node.Id), $"the goal names {t.Node.Id}");
        }

        [Test]
        public void EveryChapterEndsOnAUniqueSolution()
        {
            foreach (var chapter in TestContent.Library.Chapters)
            {
                var report = PuzzleSolver.Analyse(chapter.Puzzles.Last());
                Assert.That(report.SolutionCount, Is.EqualTo(1), report.ToString());
            }
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
        public void LockedBlocksCanBeLearned()
        {
            // A locked block can't be changed, so the player must be able to learn it
            // some other way: by having changed one of its kind before, or by changing
            // an input that reaches it and watching what comes out.
            var seenFree = new HashSet<string>();
            foreach (var p in TestContent.Library.AllPuzzles)
            {
                var circuit = new Blackbox.Circuits.CompiledCircuit(p);
                foreach (var block in p.Blocks.Where(b => b.Locked))
                {
                    if (seenFree.Contains(block.Kind.Id)) continue;
                    int node = circuit.BlockNode(block.Index);
                    bool reachable = Enumerable.Range(0, p.Inputs.Count).Any(i => p.InputChangeable(i) && circuit.Downstream(i)[node]);
                    Assert.That(reachable, Is.True, $"{p.Id}: locked {block.Kind.Id} {block.Id} is new and no adjustable input reaches it");
                }
                foreach (var block in p.Blocks.Where(b => !b.Locked)) seenFree.Add(block.Kind.Id);
            }
        }

        [Test]
        public void EveryChapterOpensGently()
        {
            // The first circuit of a chapter shows the new signals with one kind of
            // block, nothing locked and no probes to think about.
            foreach (var chapter in TestContent.Library.Chapters)
            {
                var first = chapter.Puzzles[0];
                Assert.That(first.Blocks.Select(b => b.Kind.Id).Distinct().Count(), Is.EqualTo(1), chapter.Title);
                Assert.That(first.Blocks.Any(b => b.Locked), Is.False, chapter.Title);
                Assert.That(first.Blocks.Count, Is.LessThanOrEqualTo(2), chapter.Title);
                Assert.That(first.Probes, Is.EqualTo(0), chapter.Title);
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
        /// suggested circuit, set it to a solution, record the solve. Every circuit
        /// must be reached, and each one must be solvable in play.
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
                var solution = report.Solutions[0];
                var session = new PuzzleSession(target, save.Circuits.TryGetValue(target.Id, out var resume) ? resume : null);
                for (int i = 0; i < target.Inputs.Count; i++)
                    if (session.CanChangeInput(i)) session.SetInput(i, solution.Inputs[i]);
                    else Assert.That(solution.Inputs[i], Is.EqualTo(target.Inputs[i].Value), $"{target.Id}: the solver changed fixed input {target.Inputs[i].Id}");
                for (int b = 0; b < target.Blocks.Count; b++)
                    if (session.CanChangeBlock(b)) session.SetState(b, solution.States[b]);
                    else Assert.That(solution.States[b], Is.EqualTo(target.Blocks[b].State), $"{target.Id}: the solver turned locked block {target.Blocks[b].Id}");
                Assert.That(session.Solved, Is.True, target.Id);
                save.RecordSolve(target.Id, session.Moves);
                save.Circuits[target.Id] = session.Snapshot();
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
            var circuit = new CircuitState { Inputs = new[] { 1, 0 }, States = new[] { 2, 0, 1 } };
            circuit.Probes.Add((3, 250));
            save.Circuits["b"] = circuit;
            var stroke = new InkStroke { Color = 2 };
            stroke.Points.AddRange(new[] { 100, 200, 104, 207, 110, 215 });
            save.InkFor("b").Add(stroke);
            const string typed = "B2 is \"and\" when 0\nthen or? \u00e9\u2192\\ done";
            save.NotesFor("b").Add(new TextNote { X = 640, Y = 300, Color = 1, Text = typed });
            save.NotesFor("b").Add(new TextNote { X = 10, Y = 10, Text = "   " });
            save.SeenIntros.Add("first-light");
            save.LastPuzzle = "b";
            save.Prefs.MusicVolume = 0.25f;
            save.Prefs.ReduceMotion = true;
            save.Prefs.SignalSounds = false;

            var copy = SaveData.FromJson(save.ToJson());
            Assert.That(copy.Solved, Does.Contain("a"));
            Assert.That(copy.BestMoves["a"], Is.EqualTo(4));
            Assert.That(copy.Circuits["b"].Inputs, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(copy.Circuits["b"].States, Is.EqualTo(new[] { 2, 0, 1 }));
            Assert.That(copy.Circuits["b"].Probes, Is.EqualTo(new[] { (3, 250) }));
            Assert.That(copy.Ink["b"].Count, Is.EqualTo(1));
            Assert.That(copy.Ink["b"][0].Color, Is.EqualTo(2));
            Assert.That(copy.Ink["b"][0].Points, Is.EqualTo(stroke.Points));
            Assert.That(copy.Notes["b"].Count, Is.EqualTo(1), "blank notes aren't kept");
            Assert.That(copy.Notes["b"][0].Text, Is.EqualTo(typed));
            Assert.That(copy.Notes["b"][0].X, Is.EqualTo(640));
            Assert.That(copy.Notes["b"][0].Y, Is.EqualTo(300));
            Assert.That(copy.Notes["b"][0].Color, Is.EqualTo(1));
            Assert.That(copy.SeenIntros, Does.Contain("first-light"));
            Assert.That(copy.LastPuzzle, Is.EqualTo("b"));
            Assert.That(copy.Prefs.MusicVolume, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(copy.Prefs.ReduceMotion, Is.True);
            Assert.That(copy.Prefs.SignalSounds, Is.False);
            Assert.That(new SaveData().Prefs.SignalSounds, Is.True, "on by default");
        }

        [Test]
        public void DamagedSaveFallsBackToDefaults()
        {
            var save = SaveData.FromJson(@"{ ""version"": 2, ""solved"": 5, ""circuits"": { ""a"": 3 }, ""ink"": { ""a"": [""x"", ""1:"", 7] }, ""notes"": { ""a"": [3, { ""t"": 4 }, { ""t"": "" "" }], ""b"": 7 }, ""preferences"": { ""masterVolume"": 9 } }");
            Assert.That(save.Solved, Is.Empty);
            Assert.That(save.Circuits, Is.Empty);
            Assert.That(save.Ink, Is.Empty);
            Assert.That(save.Notes, Is.Empty);
            Assert.That(save.Prefs.MasterVolume, Is.EqualTo(1f));
        }

        [Test]
        public void SavesFromBeforeTheRedesignKeepOnlyPreferences()
        {
            var save = SaveData.FromJson(@"{ ""solved"": [""first-light-01""], ""bestTests"": { ""first-light-01"": 2 }, ""preferences"": { ""musicVolume"": 0.3 } }");
            Assert.That(save.Solved, Is.Empty, "old circuits don't carry over");
            Assert.That(save.Prefs.MusicVolume, Is.EqualTo(0.3f).Within(0.0001f));
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
