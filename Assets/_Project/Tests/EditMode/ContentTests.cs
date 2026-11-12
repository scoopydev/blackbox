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
    /// Checks the shipped chapters and parts, how they open, a full playthrough,
    /// and the save file. Each wired circuit's own checks are in WiringTests.
    /// </summary>
    public class ContentTests
    {
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
            Assert.That(library.Parts[0].Chapters.Select(c => c.Id), Is.EqualTo(new[] { "first-light", "tally", "ticker", "resonance" }), "bits, steady digits, changing digits, then waves; shapes are set aside");
            Assert.That(library.Parts[1].Chapters.Select(c => c.Id), Is.EqualTo(new[] { "patch", "junction", "blend" }), "Combine reads, then meets, then mixes");
            Assert.That(library.Chapters, Is.EqualTo(library.Parts.SelectMany(p => p.Chapters)), "the parts hold every chapter, in order");
            Assert.That(library.AllPuzzles.All(p => p.Wiring), Is.True, "the player wires every circuit in the game");
            foreach (var part in library.Parts)
            {
                Assert.That(part.Title, Is.Not.Empty);
                Assert.That(part.Tagline, Is.Not.Empty);
                foreach (var chapter in part.Chapters) Assert.That(chapter.Part, Is.SameAs(part), chapter.Title);
            }
            foreach (var chapter in library.Chapters)
            {
                Assert.That(chapter.Wiring, Is.True, chapter.Title);
                Assert.That(chapter.Tagline, Is.Not.Empty, chapter.Title);
                Assert.That(chapter.Signals, Is.Not.Empty, chapter.Title);
                foreach (var text in new[] { chapter.Tagline, chapter.Signals })
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(" + string.Join("|", KindCatalog.All.Select(k => k.Id)) + @"|[BDSWQM][1-8])\b|\bM blocks?\b"), Is.False, $"{chapter.Title} names a kind of block: {text}");
                Assert.That(chapter.UnlockAfter, Is.Null.Or.Empty, $"{chapter.Title}: every chapter of an open part is open");
            }
            foreach (var chapter in LearnChapters)
            {
                Assert.That(chapter.Mixed, Is.False, $"{chapter.Title} is about one kind of signal");
                Assert.That(chapter.Puzzles.Count, Is.EqualTo(chapter.Id == "first-light" || chapter.Id == "resonance" ? 7 : chapter.Id == "tally" ? 5 : 6), chapter.Title);
            }
            foreach (var chapter in library.Parts.Skip(1).SelectMany(p => p.Chapters))
                Assert.That(chapter.Mixed, Is.True, $"{chapter.Title} mixes kinds of signal");
            var media = LearnChapters.Select(c => c.Medium).ToList();
            Assert.That(media.Count(m => m == Medium.Decimal), Is.EqualTo(2), "digits have two chapters, steady then changing");
            Assert.That(media.Where(m => m != Medium.Decimal).Distinct().Count(), Is.EqualTo(media.Count - 2), "every other medium has one chapter");
            Assert.That(media.Distinct(), Is.EquivalentTo(System.Enum.GetValues(typeof(Medium)).Cast<Medium>().Where(m => !KindCatalog.MediumShelved(m))), "every medium in play has its chapter");
        }

        [Test]
        public void EveryChapterOpensWithASignalGuide()
        {
            var guides = TestContent.Library.Chapters.ToDictionary(c => c.Id, c => c.Guide);
            Assert.That(guides, Is.EqualTo(new Dictionary<string, SignalGuide>
            {
                ["first-light"] = SignalGuide.Bits,
                ["tally"] = SignalGuide.Digits,
                ["ticker"] = SignalGuide.ChangingDigits,
                ["resonance"] = SignalGuide.Waves,
                ["patch"] = SignalGuide.Readings,
                ["junction"] = SignalGuide.Readings,
                ["blend"] = SignalGuide.Readings,
                ["unmarked"] = SignalGuide.Recap,
                ["blackbox"] = SignalGuide.Recap,
            }), "each Learn chapter explains its kind, Combine how blocks read each kind, and the last chapters recap them all");

            ContentLibrary Load(string guide) => ContentLibrary.Load(name =>
            {
                string text = TestContent.Read(name);
                return name == "chapter-2-tally" ? text.Replace("\"tagline\"", $"\"guide\": \"{guide}\", \"tagline\"") : text;
            });
            Assert.That(Load("none").Chapters[1].Guide, Is.EqualTo(SignalGuide.None), "a chapter can go without one");
            Assert.That(Load("recap").Chapters[1].Guide, Is.EqualTo(SignalGuide.Recap));
            var e = Assert.Throws<PuzzleFormatException>(() => Load("pictures"));
            StringAssert.Contains("unknown guide 'pictures'", e.Message);
        }

        [Test]
        public void NothingShippedUsesWhatIsSetAside()
        {
            // Stepped waves, shapes and every output device but the display were
            // set aside on 11 Nov 2026: no circuit may send or want a stepped wave
            // or a shape, hold a block set aside with them, or end in anything but
            // a display.
            Assert.That(KindCatalog.MediumShelved(Medium.Sampled), Is.True);
            foreach (var p in TestContent.Library.AllPuzzles)
            {
                foreach (var b in p.Blocks) Assert.That(b.Kind.Shelved, Is.False, $"{p.Id}: {b.Kind.Id} is set aside");
                foreach (var o in p.Outputs) Assert.That(o.Device, Is.EqualTo(Device.Display), $"{p.Id}: output {o.Id} is a {Devices.Noun(o.Device)}, which is set aside");
                // Every input, block and output along the intended wiring.
                foreach (var t in WiringSession.Evaluate(p, p.Solution))
                    if (t != null) Assert.That(KindCatalog.MediumShelved(t.Medium), Is.False, $"{p.Id}: a {Media.Noun(t.Medium)}, which is set aside");
            }
            Assert.That(KindCatalog.Get("STEPS").Shelved, Is.True);
        }

        [Test]
        public void TheStartupCheckPasses()
        {
            // What BlackboxApp runs when the game starts. Wired circuits start with
            // few or no wires, so it must not ask them to have every block input wired.
            Assert.DoesNotThrow(() => TestContent.Library.CheckCircuits());
        }

        [Test]
        public void PuzzlesGetHarderAfterLearn()
        {
            // Patch, Junction, Blend and Unmarked each climb: inside a chapter no
            // circuit has fewer blocks, or fewer ways to wire them, than the one
            // before it, and each chapter opens and closes on a bigger circuit than
            // the one before. Blackbox brings in the three-input blocks halfway, so
            // only its first half has to climb, from an opener bigger than Unmarked's.
            var library = TestContent.Library;
            var climbing = library.Parts[1].Chapters.Concat(library.Chapters.Where(c => c.Id == "unmarked")).ToList();
            Assert.That(climbing.Select(c => c.Id), Is.EqualTo(new[] { "patch", "junction", "blend", "unmarked" }));
            long Space(PuzzleDef p) => WiringSolver.Analyse(p, maxSpace: 0).Space;
            PuzzleDef openerBefore = null, finaleBefore = null;
            foreach (var chapter in climbing)
            {
                for (int i = 1; i < chapter.Puzzles.Count; i++)
                {
                    var before = chapter.Puzzles[i - 1];
                    var p = chapter.Puzzles[i];
                    Assert.That(p.Blocks.Count, Is.GreaterThanOrEqualTo(before.Blocks.Count), $"{p.Id} has fewer blocks than {before.Id}");
                    Assert.That(Space(p), Is.GreaterThanOrEqualTo(Space(before)), $"{p.Id} has fewer ways to wire it than {before.Id}");
                }
                var opener = chapter.Puzzles[0];
                var finale = chapter.Puzzles[chapter.Puzzles.Count - 1];
                if (openerBefore != null)
                {
                    Assert.That(Space(opener), Is.GreaterThan(Space(openerBefore)), $"{opener.Id} opens no harder than {openerBefore.Id}");
                    Assert.That(Space(finale), Is.GreaterThan(Space(finaleBefore)), $"{finale.Id} closes no harder than {finaleBefore.Id}");
                }
                openerBefore = opener;
                finaleBefore = finale;
            }
            foreach (var p in library.Chapters.Single(c => c.Id == "blend").Puzzles)
                Assert.That(p.Blocks.Count, Is.GreaterThanOrEqualTo(3), $"{p.Id}: every Blend circuit has three blocks or more");

            var blackbox = library.Chapters.Single(c => c.Id == "blackbox").Puzzles;
            var firstHalf = blackbox.TakeWhile(p => p.Blocks.All(b => b.Kind.InputCount < 3)).ToList();
            Assert.That(Space(blackbox[0]), Is.GreaterThan(Space(openerBefore)), "Blackbox opens bigger than Unmarked");
            for (int i = 1; i < firstHalf.Count; i++)
                Assert.That(Space(firstHalf[i]), Is.GreaterThanOrEqualTo(Space(firstHalf[i - 1])), $"{firstHalf[i].Id} has fewer ways to wire it than {firstHalf[i - 1].Id}");
            foreach (var p in blackbox)
                Assert.That(p.Blocks.Count, Is.GreaterThanOrEqualTo(3), $"{p.Id}: every Blackbox circuit has three blocks or more");
        }

        [Test]
        public void DottedOutlinesMarkWhatEachAnswerLeavesOut()
        {
            // Every input and block a circuit's answer doesn't need is marked optional
            // and drawn dotted; everything the answer uses is solid.
            foreach (var p in TestContent.Library.AllPuzzles.Where(p => p.Wiring))
            {
                var used = WiringSession.UsedNodes(p, p.Solution);
                for (int i = 0; i < p.Inputs.Count; i++)
                    Assert.That(p.Inputs[i].Optional, Is.EqualTo(!used[i]), $"{p.Id} input {p.Inputs[i].Id}");
                for (int b = 0; b < p.Blocks.Count; b++)
                    Assert.That(p.Blocks[b].Optional, Is.EqualTo(!used[p.Inputs.Count + b]), $"{p.Id} block {p.Blocks[b].Id}");
            }
            var first = TestContent.Library.AllPuzzles.First(p => p.Wiring);
            Assert.That(first.Inputs.Any(i => i.Optional), Is.True, "the first circuit has an optional input");
            Assert.That(first.Hint, Does.Contain("dotted"), "and its hint says what the dotted outline means");
        }

        [Test]
        public void NoTwoPuzzlesAfterLearnUseTheSameBlocks()
        {
            // Each circuit after Learn is meant to be solved by its own idea, so no
            // two of them hand the player the same set of blocks.
            var later = TestContent.Library.Parts.Skip(1).SelectMany(part => part.Chapters).SelectMany(c => c.Puzzles).ToList();
            string BlockSet(PuzzleDef p) => string.Join("+", p.Blocks.Select(b => b.Kind.Id).OrderBy(id => id, System.StringComparer.Ordinal));
            var dupes = later.GroupBy(BlockSet).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(p => p.Id))}").ToList();
            Assert.That(dupes, Is.Empty, "circuits with the same set of blocks");
        }

        [Test]
        public void JunctionBringsKindsTogether()
        {
            // Every Junction circuit's solution feeds some block two different
            // kinds of signal at once, and uses only blocks named in Part One.
            var junction = TestContent.Library.Chapters.Single(c => c.Id == "junction");
            Assert.That(junction.Puzzles.Count, Is.EqualTo(6));
            foreach (var p in junction.Puzzles)
            {
                Assert.That(p.Blocks.All(b => !b.Kind.Mixing), Is.True, $"{p.Id}: the mixing blocks wait for Blend");
                var values = WiringSession.Evaluate(p, p.Solution);
                var session = new WiringSession(p);
                bool meets = false;
                foreach (var b in p.Blocks)
                {
                    var media = p.Solution.Where(w => w.To == b).Select(w => values[session.NodeOf(w.From)].Medium).Distinct().Count();
                    if (media > 1) meets = true;
                }
                Assert.That(meets, Is.True, $"{p.Id}: some block meets two kinds of signal");
                Assert.That(p.Inputs.Select(i => i.Signal.Medium).Distinct().Count(), Is.GreaterThan(1), $"{p.Id}: more than one kind of input");
            }
        }

        [Test]
        public void EachLearnChapterSticksToItsSignal()
        {
            // Part One teaches one kind of signal per chapter, with that kind's four
            // blocks, before Part Two mixes them. The one exception is Resonance's
            // TURNAROUND, where the wave block GATE, met with two waves, shows that
            // which input goes on top can matter (no wave block of the four cares).
            foreach (var chapter in LearnChapters)
            {
                var kinds = new HashSet<string>();
                foreach (var p in chapter.Puzzles)
                {
                    foreach (var i in p.Inputs) Assert.That(i.Signal.Medium, Is.EqualTo(chapter.Medium), $"{p.Id}: input {i.Id}");
                    foreach (var b in p.Blocks)
                    {
                        if (p.Id == TurnaroundId && b.Kind.Id == "GATE") continue;
                        Assert.That(b.Kind.Mixing, Is.False, $"{p.Id}: {b.Kind.Id} belongs in Part Two");
                        Assert.That(b.Kind.Medium, Is.EqualTo(chapter.Medium), $"{p.Id}: {b.Kind.Id}");
                        kinds.Add(b.Kind.Id);
                    }
                }
                Assert.That(kinds.Count, Is.EqualTo(4), $"{chapter.Title} uses all four of its kinds: {string.Join(", ", kinds)}");
            }
        }

        [Test]
        public void TheGameStartsWithSteadyBits()
        {
            // First Light opens with bits that stay at 0 or 1, and only then moves
            // on to bits that switch as the loop plays.
            var firstLight = TestContent.Library.Chapters[0];
            Assert.That(firstLight.Id, Is.EqualTo("first-light"));
            var steady = firstLight.Puzzles.TakeWhile(p => p.Inputs.All(i => i.Signal.Trace.IsFlat)).Count();
            Assert.That(steady, Is.EqualTo(3), "three circuits of steady bits come first");
            foreach (var p in firstLight.Puzzles.Skip(steady))
                Assert.That(p.Inputs.Any(i => !i.Signal.Trace.IsFlat), Is.True, $"{p.Id}: once patterns start, every circuit has one");
        }

        [Test]
        public void DigitsComeSteadyThenChanging()
        {
            // Tally keeps to whole digits that hold steady all loop; Ticker, after it,
            // is about digits that change as the loop plays.
            var learn = TestContent.Library.Parts[0].Chapters;
            var tally = learn.First(c => c.Id == "tally");
            var ticker = learn.First(c => c.Id == "ticker");
            Assert.That(learn.IndexOf(ticker), Is.EqualTo(learn.IndexOf(tally) + 1));
            foreach (var p in tally.Puzzles)
            {
                foreach (var i in p.Inputs) Assert.That(i.Signal.Trace.IsFlat, Is.True, $"{p.Id}: input {i.Id} is steady");
                foreach (var o in p.Outputs) Assert.That(o.Want.IsFlat, Is.True, $"{p.Id}: target {o.Id} is steady");
            }
            foreach (var p in ticker.Puzzles)
                Assert.That(p.Inputs.Any(i => !i.Signal.Trace.IsFlat), Is.True, $"{p.Id} has a digit that changes");
        }

        [Test]
        public void CircuitsAreNamedByChapterAndNumber()
        {
            foreach (var chapter in TestContent.Library.Chapters)
                for (int i = 0; i < chapter.Puzzles.Count; i++)
                    Assert.That(chapter.Puzzles[i].Name, Is.EqualTo($"{chapter.Title.ToUpperInvariant()} {i + 1}"));
            Assert.That(TestContent.Library.Chapters[1].Puzzles[0].Name, Is.EqualTo("TALLY 1"));
        }

        /// <summary>The Resonance circuit that teaches that which input goes on top can matter.</summary>
        const string TurnaroundId = "resonance-03";

        [Test]
        public void TurnaroundTeachesThatInputOrderCanMatter()
        {
            // It comes wired with its block's two inputs the wrong way round: every
            // wire is in, the run fails, and swapping the two wires solves it.
            var resonance = LearnChapters.First(c => c.Id == "resonance");
            var p = resonance.Puzzles.First(q => q.Id == TurnaroundId);
            Assert.That(resonance.Puzzles.IndexOf(p), Is.EqualTo(2), "after MIX's circuit, whose two inputs are alike");
            Assert.That(p.Blocks.Count, Is.EqualTo(1));
            Assert.That(p.Blocks[0].Kind.InputCount, Is.EqualTo(2));
            Assert.That(p.Blocks[0].Kind.AlikeFrom, Is.LessThan(0), "a block whose inputs are not alike");

            var session = new WiringSession(p);
            var report = session.Check();
            Assert.That(report.EmptyBlocks, Is.Empty, "every input wired");
            Assert.That(report.Solved, Is.False, "wired the wrong way round");

            int block = session.BlockNode(0);
            int top = session.DriverOf(block, 0), bottom = session.DriverOf(block, 1);
            Assert.That(top, Is.Not.EqualTo(bottom), "two different signals go in");
            session.Disconnect(block, 0);
            session.Disconnect(block, 1);
            session.Connect(bottom, block, 0);
            session.Connect(top, block, 1);
            Assert.That(session.Check().Solved, Is.True, "swapping the two wires solves it");
            Assert.That(WiringSolver.Analyse(p).Solutions, Is.EqualTo(1), "and nothing else does");
        }

        [Test]
        public void EachLearnChapterTeachesWiringStepByStep()
        {
            // The first circuit comes wired and needs one wire moved; the second is
            // half wired; from the third on, the player draws every wire. Ticker
            // carries on from Tally's digits, so the player draws every wire there.
            foreach (var chapter in LearnChapters.Where(c => c.Id != "ticker"))
            {
                var first = chapter.Puzzles[0];
                var second = chapter.Puzzles[1];
                Assert.That(first.Blocks.Count, Is.EqualTo(1), first.Id);
                var whole = new WiringSession(first);
                var report = whole.Check();
                Assert.That(report.EmptyBlocks, Is.Empty, $"{first.Id} starts with every block input wired");
                Assert.That(report.UnwiredOutputs, Is.Empty, $"{first.Id} starts with every output wired");
                Assert.That(report.Solved, Is.False, $"{first.Id} needs a wire moved");

                var half = new WiringSession(second);
                Assert.That(half.WireCount, Is.GreaterThan(0), $"{second.Id} starts half wired");
                Assert.That(half.Check().EmptyBlocks, Is.Not.Empty, $"{second.Id} leaves a block input for the player");

                foreach (var p in chapter.Puzzles.Skip(2).Where(p => p.Id != TurnaroundId))
                    Assert.That(p.Start, Is.Empty, $"{p.Id}: from the third circuit on, the player draws every wire");
            }
            foreach (var p in TestContent.Library.Parts.Skip(1).SelectMany(part => part.Chapters).SelectMany(c => c.Puzzles))
                Assert.That(p.Start, Is.Empty, $"{p.Id} starts with no wires");
            foreach (var p in LearnChapters.First(c => c.Id == "ticker").Puzzles)
                Assert.That(p.Start, Is.Empty, $"{p.Id} starts with no wires");
        }

        [Test]
        public void DevicesTurnUpInPartOne()
        {
            var used = new HashSet<Device>(LearnChapters.SelectMany(c => c.Puzzles).SelectMany(p => p.Outputs).Select(o => o.Device));
            foreach (var d in Devices.All.Where(d => !Devices.Shelved.Contains(d))) Assert.That(used, Does.Contain(d), $"Part One has a {Devices.Noun(d)}");
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

                Assert.That(target.Wiring, Is.True, target.Id);
                var wiring = new WiringSession(target, save.Circuits.TryGetValue(target.Id, out var saved) ? saved.Wires : null);
                foreach (var w in target.Solution)
                {
                    int from = wiring.NodeOf(w.From), to = wiring.NodeOf(w.To);
                    if (wiring.DriverOf(to, w.ToPort) == from) continue; // one of the wires it started with
                    Assert.That(wiring.Connect(from, to, w.ToPort), Is.True, $"{target.Id}: {w.From.Id} -> {w.To.Id}");
                }
                Assert.That(wiring.Run(), Is.True, target.Id);
                save.RecordSolve(target.Id, wiring.Moves);
                var state = new CircuitState();
                state.Wires.AddRange(wiring.Snapshot());
                save.Circuits[target.Id] = state;
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
            wired.Places["g"] = (-120, 64);
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
            Assert.That(copy.Circuits["w"].Places["g"], Is.EqualTo((-120, 64)), "where the player moved a block");
            Assert.That(copy.Circuits["b"].Places, Is.Empty);
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

            var settingsGame = SaveData.FromJson(@"{ ""version"": 2, ""solved"": [""first-light-01""], ""preferences"": { ""reduceMotion"": true } }");
            Assert.That(settingsGame.Solved, Is.Empty, "progress from before the wiring game doesn't carry over");
            Assert.That(settingsGame.Prefs.ReduceMotion, Is.True);
            var now = new SaveData();
            now.RecordSolve("first-light-01", 3);
            Assert.That(SaveData.FromJson(now.ToJson()).Solved, Does.Contain("first-light-01"));
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
