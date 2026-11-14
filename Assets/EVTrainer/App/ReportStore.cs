using System;
using System.IO;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Saves completed runs as pretty-printed JSON in Application.persistentDataPath/reports (Core <see cref="ReportFiles"/>
    /// names, newest 20 kept) and reads the newest back for the "last result" line. JsonUtility (built in, no package).
    /// File writing runs off the main thread so a save never stalls a frame.
    /// </summary>
    public static class ReportStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "reports");

        /// <summary>Serialises <paramref name="report"/> now and writes it in the background; returns the file path.</summary>
        public static string Save(TrainingReport report, DateTimeOffset end)
        {
            string json = JsonUtility.ToJson(report, prettyPrint: true);
            string folder = Folder;
            string path = UniquePath(folder, ReportFiles.FileName(end));
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(path, json);
                    foreach (var old in ReportFiles.ToDelete(Names(folder))) File.Delete(Path.Combine(folder, old));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EVTrainer] Could not save the report to {path}: {e.Message}");
                }
            });
            return path;
        }

        /// <summary>The newest saved report, or null (none yet, or unreadable).</summary>
        public static TrainingReport LoadLatest()
        {
            try
            {
                string folder = Folder;
                if (!Directory.Exists(folder)) return null;
                var newest = ReportFiles.NewestFirst(Names(folder));
                if (newest.Count == 0) return null;
                return JsonUtility.FromJson<TrainingReport>(File.ReadAllText(Path.Combine(folder, newest[0])));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EVTrainer] Could not read the last report: {e.Message}");
                return null;
            }
        }

        private static string[] Names(string folder)
        {
            var files = Directory.GetFiles(folder);
            for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileName(files[i]);
            return files;
        }

        /// <summary>Two runs finishing in the same second get "_2", "_3" (they still sort together).</summary>
        private static string UniquePath(string folder, string name)
        {
            string path = Path.Combine(folder, name);
            string stem = Path.GetFileNameWithoutExtension(name);
            for (int i = 2; File.Exists(path); i++) path = Path.Combine(folder, $"{stem}_{i}{ReportFiles.Extension}");
            return path;
        }
    }
}
