using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blackbox.EditorTools
{
    /// <summary>The BLACKBOX menu: check the circuits, make the scene, and manage the save while testing.</summary>
    static class BlackboxMenu
    {
        const string PuzzleFolder = "Assets/_Project/Puzzles/Resources/BlackboxPuzzles";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        static ContentLibrary LoadFromDisk()
        {
            return ContentLibrary.Load(name =>
            {
                string path = Path.Combine(PuzzleFolder, name + ".json");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            });
        }

        /// <summary>
        /// Solves every circuit by brute force and reports anything wrong: no
        /// solution, already solved at the start, unwired nodes, repeated rows.
        /// Wired circuits are checked by trying every way to wire them.
        /// </summary>
        [MenuItem("BLACKBOX/Validate Puzzles", priority = 1)]
        static void ValidatePuzzles()
        {
            ContentLibrary library;
            try
            {
                library = LoadFromDisk();
            }
            catch (Exception e)
            {
                Debug.LogError("BLACKBOX: " + e.Message);
                EditorUtility.DisplayDialog("BLACKBOX", "The circuits couldn't be loaded:\n\n" + e.Message, "OK");
                return;
            }

            int total = 0;
            foreach (var chapter in library.Chapters) total += chapter.Puzzles.Count;
            int circuits = 0, errors = 0, warnings = 0;
            var summary = new StringBuilder();
            foreach (var chapter in library.Chapters)
            {
                summary.AppendLine($"Chapter {chapter.Numeral} · {chapter.Title}");
                foreach (var p in chapter.Puzzles)
                {
                    circuits++;
                    EditorUtility.DisplayProgressBar("BLACKBOX", "Solving " + p.Name, (float)circuits / Math.Max(1, total));
                    if (p.Wiring)
                    {
                        ValidateWired(p, summary, ref errors, ref warnings);
                        continue;
                    }
                    PuzzleReport report;
                    try
                    {
                        report = PuzzleSolver.Analyse(p, 4);
                    }
                    catch (Exception e)
                    {
                        errors++;
                        Debug.LogError($"BLACKBOX: {p.Id}: {e.Message}");
                        continue;
                    }
                    if (report.Errors.Count > 0)
                    {
                        errors += report.Errors.Count;
                        Debug.LogError("BLACKBOX: " + report);
                    }
                    else if (report.Warnings.Count > 0)
                    {
                        warnings += report.Warnings.Count;
                        Debug.LogWarning("BLACKBOX: " + report);
                    }
                    summary.AppendLine($"  {p.Label} {p.Name}: {report.SolutionCount} solution(s) in {report.SearchSpace} combinations");
                }
            }
            EditorUtility.ClearProgressBar();
            string result = $"{circuits} circuits, {errors} error(s), {warnings} warning(s).";
            if (errors == 0) Debug.Log($"BLACKBOX: {result}\n{summary}");
            EditorUtility.DisplayDialog("BLACKBOX", result + (errors + warnings > 0 ? "\n\nDetails are in the Console." : ""), "OK");
        }

        static void ValidateWired(PuzzleDef p, StringBuilder summary, ref int errors, ref int warnings)
        {
            WiringAnalysis analysis;
            try
            {
                analysis = WiringSolver.Analyse(p);
            }
            catch (Exception e)
            {
                errors++;
                Debug.LogError($"BLACKBOX: {p.Id}: {e.Message}");
                return;
            }
            var problems = new List<string>();
            if (!analysis.SolutionWorks) problems.Add("its solution doesn't solve it");
            if (!analysis.StartsUnsolved) problems.Add("it starts solved");
            if (problems.Count > 0)
            {
                errors += problems.Count;
                Debug.LogError($"BLACKBOX: {p.Id}: {string.Join("; ", problems)}\n{analysis}");
            }
            var notes = new List<string>();
            if (!analysis.Searched) notes.Add($"too many wirings to check them all ({analysis.Space})");
            foreach (var o in p.Outputs)
                foreach (var i in p.Inputs)
                    // A steady bit is only 0 or 1, so an input often matches it; the every-block rule still applies.
                    if (!(o.Want.Medium == Blackbox.Components.Medium.Binary && o.Want.IsFlat) && Blackbox.Components.Devices.Same(o.Device, i.Signal.Trace, o.Want))
                        notes.Add($"wiring {i.Id} straight to {o.Id} already matches its target");
            if (notes.Count > 0)
            {
                warnings += notes.Count;
                Debug.LogWarning($"BLACKBOX: {p.Id}: {string.Join("; ", notes)}");
            }
            summary.AppendLine(analysis.Searched
                ? $"  {p.Label} {p.Name}: {analysis.Solutions} wiring(s) solve it, of {analysis.Space} ways to wire its blocks"
                : $"  {p.Label} {p.Name}: {analysis.Space} ways to wire its blocks, too many to search");
        }

        /// <summary>Makes the scene the game runs in: a black camera and the BLACKBOX object.</summary>
        [MenuItem("BLACKBOX/Create Main Scene", priority = 20)]
        static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("BLACKBOX", ScenePath + " already exists. Replace it?", "Replace", "Cancel")) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camera.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camera.AddComponent<AudioListener>();
            new GameObject("BLACKBOX").AddComponent<BlackboxApp>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"BLACKBOX: created {ScenePath} and put it first in Build Settings.");
        }

        [MenuItem("BLACKBOX/Save/Show Save File", priority = 40)]
        static void ShowSave()
        {
            if (File.Exists(SaveStore.FilePath)) EditorUtility.RevealInFinder(SaveStore.FilePath);
            else EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("BLACKBOX/Save/Erase Save", priority = 41)]
        static void EraseSave()
        {
            if (!EditorUtility.DisplayDialog("BLACKBOX", "Erase the save file? Solved circuits, names and settings will be lost.", "Erase", "Cancel")) return;
            if (Application.isPlaying && BlackboxApp.Instance != null)
            {
                BlackboxApp.Instance.EraseProgress();
                return;
            }
            if (File.Exists(SaveStore.FilePath)) File.Delete(SaveStore.FilePath);
            Debug.Log("BLACKBOX: save erased.");
        }

        /// <summary>For testing later circuits: marks every circuit solved.</summary>
        [MenuItem("BLACKBOX/Save/Solve Every Circuit", priority = 42)]
        static void SolveEverything()
        {
            ContentLibrary library;
            try { library = LoadFromDisk(); }
            catch (Exception e) { Debug.LogError("BLACKBOX: " + e.Message); return; }

            bool live = Application.isPlaying && BlackboxApp.Instance != null;
            var save = live ? BlackboxApp.Instance.Save : SaveStore.Load();
            foreach (var p in library.AllPuzzles)
            {
                if (!save.Solved.Contains(p.Id)) save.RecordSolve(p.Id, 99);
                if (p.Wiring && !save.Circuits.ContainsKey(p.Id))
                {
                    var wired = new CircuitState();
                    foreach (var w in p.Solution)
                        wired.Wires.Add(w.To is BlockDef b && b.Kind.InputCount > 1 ? $"{w.From.Id} -> {w.To.Id}.{w.ToPort}" : $"{w.From.Id} -> {w.To.Id}");
                    save.Circuits[p.Id] = wired;
                }
            }
            foreach (var c in library.Chapters) save.SeenIntros.Add(c.Id);
            if (live)
            {
                BlackboxApp.Instance.SaveNow();
                BlackboxApp.Instance.GoTitle();
            }
            else SaveStore.Save(save);
            Debug.Log("BLACKBOX: every circuit marked solved.");
        }
    }
}
