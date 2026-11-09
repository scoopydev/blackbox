using System;
using System.IO;
using Blackbox.Core;
using UnityEngine;

namespace Blackbox.Game
{
    /// <summary>
    /// Reads and writes the save file (blackbox-save.json in Application.persistentDataPath).
    /// Writes go to a temporary file first so a crash can't leave half a save.
    /// </summary>
    public static class SaveStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "blackbox-save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath)) return SaveData.FromJson(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BLACKBOX: couldn't read the save at {FilePath}, starting fresh. {e.Message}");
                TryBackup();
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, data.ToJson());
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BLACKBOX: couldn't write the save. {e.Message}");
            }
        }

        static void TryBackup()
        {
            try { File.Copy(FilePath, FilePath + ".broken", true); }
            catch { /* best effort */ }
        }
    }
}
