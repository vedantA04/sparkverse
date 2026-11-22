using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    /// <summary>
    /// Ensures every config file in Robot_config_JSONS loads and is valid.
    /// </summary>
    public class SimulationConfigFileTests
    {
        private static readonly string[] SupportedPrefabs =
            { "digitaltwin", "simplediffdrivebot", "articulationbody" };

        private static string ConfigDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Robot_config_JSONS"));

        private static IEnumerable<TestCaseData> ConfigFiles()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Robot_config_JSONS"));
            if (!Directory.Exists(dir))
                yield break;

            foreach (var path in Directory.GetFiles(dir, "*.json").OrderBy(p => p))
                yield return new TestCaseData(path).SetName($"Config_is_valid({Path.GetFileName(path)})");
        }

        [Test]
        public void Config_directory_contains_configs()
        {
            Assert.IsTrue(Directory.Exists(ConfigDir), $"Config directory not found: {ConfigDir}");
            CollectionAssert.IsNotEmpty(Directory.GetFiles(ConfigDir, "*.json"));
        }

        [TestCaseSource(nameof(ConfigFiles))]
        public void Config_is_valid(string path)
        {
            var json = File.ReadAllText(path);

            // Strict parse: fails on malformed JSON, unknown/misspelled fields, or wrong types
            var strictSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };
            SimulationConfig config = null;
            Assert.DoesNotThrow(
                () => config = JsonConvert.DeserializeObject<SimulationConfig>(json, strictSettings),
                "Strict parse failed (malformed JSON or unknown field?)");
            Assert.IsNotNull(config, "Config deserialized to null");

            // Same loading path the simulation uses
            Assert.IsNotNull(SimulationConfig.FromFile(path), "SimulationConfig.FromFile returned null");

            Assert.IsTrue(config.IsValid(out var error), $"IsValid failed: {error}");

            foreach (var robot in config.robots)
            {
                CollectionAssert.Contains(SupportedPrefabs, robot.prefab?.ToLowerInvariant(),
                    $"Robot {robot.id} has unsupported prefab '{robot.prefab}'");
            }

            var duplicateIds = config.robots
                .GroupBy(r => r.id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            CollectionAssert.IsEmpty(duplicateIds, "Duplicate robot IDs");

            if (config.mqtt != null && config.mqtt.enablePublishPoses)
            {
                Assert.Greater(config.mqtt.posePublishInterval, 0f, "posePublishInterval must be positive");
            }
        }
    }
}
