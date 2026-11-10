using System.Collections.Generic;
using System.Linq;
using Blackbox.Circuits;
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
        /// <summary>The circuits of Part One, where the player changes inputs and blocks.</summary>
        static IEnumerable<PuzzleDef> AllPuzzles() => TestContent.Library.AllPuzzles.Where(p => !p.Wiring);

        /// <summary>The chapters of Part One.</summary>
        static IEnumerable<ChapterDef> LearnChapters => TestContent.Library.Parts[0].Chapters;

        [Test]
        public void ChaptersLoad()
        {
            var library = TestContent.Library;
            Assert.That(library.Parts.Select(p => p.Id), Is.EqualTo(new[] { "learn", "combine", "build" }));
            Assert.That(library.Parts[0].AfterPrevious, Is.False, "Part One is open from the start");
            Assert.That(library.Parts[1].AfterPrevious && library.Parts[2].AfterPrevious, Is.True, "each later part waits for the one before");
            Assert.That(library.Chapters.Count, Is.EqualTo(9));
            Assert.That(library.Chapters, Is.EqualTo(library.Parts.SelectMany(p => p.Chapters)), "the parts hold every chapter, in order");
            foreach (var part in library.Parts)
            {
                Assert.That(part.Title, Is.Not.Empty);
                Assert.That(part.Tagline, Is.Not.Empty);
                foreach (var chapter in part.Chapters) Assert.That(chapter.Part, Is.SameAs(part), chapter.Title);
            }
            foreach (var chapter in library.Parts[1].Chapters.Concat(library.Parts[2].Chapters))
            {
                Assert.That(chapter.Wiring, Is.True, chapter.Title);
                Assert.That(chapter.Puzzles.All(p => p.Wiring), Is.True, chapter.Title);
                Assert.That(chapter.Puzzles.All(p => p.Marked == (chapter.Part.Id == "combine")), Is.True, $"{chapter.Title}: blocks are named in Combine, unnamed in Build");
                Assert.That(chapter.Tagline, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Signals, Is.Not.Empty, chapter.Title);
            }
            foreach (var chapter in LearnChapters)
            {
                Assert.That(chapter.Wiring, Is.False, chapter.Title);
                Assert.That(chapter.Puzzles.Count, Is.EqualTo(6), chapter.Title);
                Assert.That(chapter.Tagline, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Signals, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Puzzles.All(p => p.Medium == chapter.Medium), Is.True, chapter.Title);
            }
            Assert.That(LearnChapters.Select(c => c.Medium).Distinct().Count(), Is.EqualTo(5), "one chapter for each medium");
            foreach (var chapter in library.Chapters)
                Assert.That(chapter.UnlockAfter, Is.Null.Or.Empty, $"{chapter.Title}: every chapter is open from the start");
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

        [TestCaseSource(nameof(AllPuzzles))]
        public void TargetsAreWrittenAsTheSolutionMakesThem(PuzzleDef puzzle)
        {
            // Two sampled waves can trace the same line and both meet a target. The
            // file should still name the one the solution makes, so the target's
            // caption reads the same as the output's.
            var report = PuzzleSolver.Analyse(puzzle, 1);
            Assume.That(report.Solutions, Is.Not.Empty);
            var solution = report.Solutions[0];
            var circuit = new CompiledCircuit(puzzle);
            var values = circuit.Evaluate(solution.Inputs, solution.States);
            foreach (var t in puzzle.Targets)
                Assert.That(Media.Describe(puzzle.Medium, values[circuit.NodeOf(t.Node)]), Is.EqualTo(Media.Describe(puzzle.Medium, t.Want)), $"{puzzle.Id}: target on {t.Node.Id}");
        }

        [TestCaseSource(nameof(AllPuzzles))]
        public void AdjustableWavesListTheirOptions(PuzzleDef puzzle)
        {
            foreach (var input in puzzle.Inputs)
            {
                if (!input.Adjustable || !Media.IsWave(puzzle.Medium)) continue;
                Assert.That(input.Options, Is.Not.Null, $"{puzzle.Id}: {input.Id}");
                Assert.That(input.Options.Length, Is.InRange(2, 5), $"{puzzle.Id}: {input.Id} has a short list to step through");
                for (int a = 0; a < input.Options.Length; a++)
                    for (int b = a + 1; b < input.Options.Length; b++)
                        Assert.That(Media.Same(puzzle.Medium, input.Options[a], input.Options[b]), Is.False, $"{puzzle.Id}: {input.Id} offers two options that are the same signal");
            }
        }

        [Test]
        public void EveryChapterEndsOnAUniqueSolution()
        {
            foreach (var chapter in LearnChapters)
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
            foreach (var chapter in LearnChapters)
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
            foreach (var p in AllPuzzles())
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
            foreach (var chapter in LearnChapters)
            {
                var first = chapter.Puzzles[0];
                Assert.That(first.Blocks.Select(b => b.Kind.Id).Distinct().Count(), Is.EqualTo(1), chapter.Title);
                Assert.That(first.Blocks.Any(b => b.Locked), Is.False, chapter.Title);
                Assert.That(first.Blocks.Count, Is.LessThanOrEqualTo(2), chapter.Title);
                Assert.That(first.Probes, Is.EqualTo(0), chapter.Title);
            }
        }

        [Test]
        public void EveryChapterStartsWithItsFirstCircuitOpen()
        {
            var library = TestContent.Library;
            var save = new SaveData();
            var progress = new Progress(library, save);
            foreach (var chapter in LearnChapters)
            {
                Assert.That(progress.IsUnlocked(chapter), Is.True, chapter.Title);
                Assert.That(progress.IsUnlocked(chapter.Puzzles[0]), Is.True, chapter.Title);
                Assert.That(progress.IsUnlocked(chapter.Puzzles[1]), Is.False, $"{chapter.Title}: only the first circuit is open at the start");
            }
            Assert.That(progress.ContinueTarget(), Is.SameAs(library.Chapters[0].Puzzles[0]), "a new player starts at the beginning");

            var first = library.Chapters[0];
            save.RecordSolve(first.Puzzles[0].Id, 3);
            Assert.That(progress.IsUnlocked(first.Puzzles[1]), Is.True);
            Assert.That(progress.IsUnlocked(first.Puzzles[2]), Is.True, "one spare");
            Assert.That(progress.IsUnlocked(first.Puzzles[3]), Is.False);
            Assert.That(progress.IsUnlocked(library.Chapters[1].Puzzles[1]), Is.False, "solving in one chapter opens nothing in another");

            foreach (var part in library.Parts.Skip(1))
            {
                Assert.That(progress.IsUnlocked(part), Is.False, $"{part.Title} waits for the part before it");
                foreach (var chapter in part.Chapters)
                {
                    Assert.That(progress.IsUnlocked(chapter), Is.False, chapter.Title);
                    Assert.That(progress.IsUnlocked(chapter.Puzzles[0]), Is.False, chapter.Title);
                }
            }
        }

        [Test]
        public void APartOpensWhenThePartBeforeItIsFinished()
        {
            var library = TestContent.Library;
            var save = new SaveData();
            var progress = new Progress(library, save);
            var learn = library.Parts[0];
            var combine = library.Parts[1];
            var all = learn.Chapters.SelectMany(c => c.Puzzles).ToList();
            foreach (var p in all.Take(all.Count - 1)) save.RecordSolve(p.Id, 3);
            Assert.That(progress.IsComplete(learn), Is.False);
            Assert.That(progress.IsUnlocked(combine), Is.False, "one circuit still to go");
            save.RecordSolve(all.Last().Id, 3);
            Assert.That(progress.IsComplete(learn), Is.True);
            Assert.That(progress.IsUnlocked(combine), Is.True);
            Assert.That(progress.PartBefore(combine), Is.SameAs(learn));
            Assert.That(progress.IsUnlocked(combine.Chapters[0].Puzzles[0]), Is.True, "the first circuit of the new part is open");
            Assert.That(progress.IsUnlocked(combine.Chapters[1]), Is.True, "every chapter of an open part is open");
            Assert.That(progress.IsUnlocked(combine.Chapters[1].Puzzles[0]), Is.True);
            Assert.That(progress.IsUnlocked(library.Parts[2]), Is.False);
        }

        [Test]
        public void OpenEverythingOpensEveryCircuit()
        {
            var library = TestContent.Library;
            var save = new SaveData();
            save.Prefs.DebugOpenAll = true;
            var progress = new Progress(library, save);
            foreach (var part in library.Parts) Assert.That(progress.IsUnlocked(part), Is.True, part.Title);
            foreach (var chapter in library.Chapters) Assert.That(progress.IsUnlocked(chapter), Is.True, chapter.Title);
            foreach (var p in library.AllPuzzles) Assert.That(progress.IsUnlocked(p), Is.True, p.Id);
            save.Prefs.DebugOpenAll = false;
            Assert.That(progress.IsUnlocked(library.Parts[1]), Is.False, "turning it off locks them again");
        }

        [Test]
        public void AChapterCanWaitForAnEarlierOne()
        {
            // The shipped chapters don't use it, but a chapter file can still ask to wait.
            var library = ContentLibrary.Load(name =>
            {
                string text = TestContent.Read(name);
                return name == "chapter-2-tally" ? text.Replace("\"tagline\"", "\"unlock\": { \"after\": \"first-light\", \"solved\": 4 }, \"tagline\"") : text;
            });
            var save = new SaveData();
            var progress = new Progress(library, save);
            var first = library.Chapters[0];
            var second = library.Chapters[1];
            Assert.That(second.UnlockSolved, Is.EqualTo(4));
            Assert.That(progress.IsUnlocked(second), Is.False);
            Assert.That(progress.IsUnlocked(second.Puzzles[0]), Is.False);
            for (int i = 0; i < 4; i++) save.RecordSolve(first.Puzzles[i].Id, 5);
            Assert.That(progress.IsUnlocked(second), Is.True);
            Assert.That(progress.IsUnlocked(second.Puzzles[0]), Is.True);
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

                if (target.Wiring)
                {
                    var wiring = new WiringSession(target, save.Circuits.TryGetValue(target.Id, out var saved) ? saved.Wires : null);
                    foreach (var w in target.Solution)
                        Assert.That(wiring.Connect(wiring.NodeOf(w.From), wiring.NodeOf(w.To), w.ToPort), Is.True, $"{target.Id}: {w.From.Id} -> {w.To.Id}");
                    Assert.That(wiring.Run(), Is.True, target.Id);
                    save.RecordSolve(target.Id, wiring.Moves);
                    var state = new CircuitState();
                    state.Wires.AddRange(wiring.Snapshot());
                    save.Circuits[target.Id] = state;
                    continue;
                }

                var report = PuzzleSolver.Analyse(target, 1);
                var solution = report.Solutions[0];
                var session = new PuzzleSession(target, save.Circuits.TryGetValue(target.Id, out var resume) ? resume : null);
                for (int i = 0; i < target.Inputs.Count; i++)
                    if (session.CanChangeInput(i)) session.SetInput(i, solution.Inputs[i]);
                    else Assert.That(solution.Inputs[i], Is.EqualTo(target.Inputs[i].Value), $"{target.Id}: the solver changed fixed input {target.Inputs[i].Id}");
                for (int b = 0; b < target.Blocks.Count; b++)
                    if (session.CanChangeBlock(b)) session.SetState(b, solution.States[b]);
                    else Assert.That(solution.States[b], Is.EqualTo(target.Blocks[b].State), $"{target.Id}: the solver turned locked block {target.Blocks[b].Id}");
                Assert.That(session.Run(), Is.True, target.Id);
                save.RecordSolve(target.Id, session.Moves);
                save.Circuits[target.Id] = session.Snapshot();
            }

            Assert.That(progress.TotalSolved, Is.EqualTo(total));
            foreach (var chapter in library.Chapters) Assert.That(progress.IsUnlocked(chapter), Is.True, chapter.Title);
            foreach (var part in library.Parts) Assert.That(progress.IsComplete(part), Is.True, part.Title);
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
            var wired = new CircuitState();
            wired.Wires.AddRange(new[] { "A -> g.0", "g -> X" });
            save.Circuits["w"] = wired;
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
            save.Prefs.DebugRules = true;
            save.Prefs.DebugOpenAll = true;

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
            Assert.That(copy.Prefs.DebugRules, Is.True);
            Assert.That(new SaveData().Prefs.DebugRules, Is.False, "off by default");
            Assert.That(copy.Prefs.DebugOpenAll, Is.True);
            Assert.That(new SaveData().Prefs.DebugOpenAll, Is.False, "off by default");
            Assert.That(copy.Circuits["w"].Wires, Is.EqualTo(new[] { "A -> g.0", "g -> X" }));
            Assert.That(copy.Circuits["b"].Wires, Is.Empty);
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
