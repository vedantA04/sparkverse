using System;
using System.IO;
using UnityEngine;

namespace UI
{
    [Serializable]
    public class UserPreferences
    {
        public string lastUsedConfigPath = "";

        public static UserPreferences Load()
        {
            var prefsPath = PrefsFilePath;
            if (!File.Exists(prefsPath))
                return new UserPreferences();

            var json = File.ReadAllText(prefsPath);
            return JsonUtility.FromJson<UserPreferences>(json) ?? new UserPreferences();
        }

        public static void SaveLastUsedConfigPath(string configPath)
        {
            Debug.Log($"saving last used: {configPath}");
            var prefsPath = PrefsFilePath;
            EnsurePrefsDir(prefsPath);

            var prefs = Load();
            prefs.lastUsedConfigPath = configPath;
            File.WriteAllText(prefsPath, JsonUtility.ToJson(prefs, true));
        }

        private static void EnsurePrefsDir(string filePath)
        {
            var prefsDir = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(prefsDir))
            {
                throw new InvalidOperationException("Prefs dir must not be null");
            }

            if (!Directory.Exists(prefsDir))
            {
                Directory.CreateDirectory(prefsDir);
            }
        }

        private static string PrefsFilePath
        {
            get
            {
                var prefsDir = Application.persistentDataPath;
                return Path.Combine(prefsDir, "user_prefs.json");
            }
        }
    }
}
