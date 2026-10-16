using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class SecondLevelReleaseBuilder
    {
        // The caller backs up the authored scene and build before running this migration.
        public static void ApplyAndBuild()
        {
            var scene = EditorSceneManager.OpenScene(FullGameBuilder.ExplorationPath);
            var sofa = UnityEngine.Object.FindObjectsByType<NewHouseItem>(FindObjectsSortMode.None)
                .Single(i => i.placement.Contains("LIV_NH007"));
            var before = sofa.Bounds;
            // Inner faces of the living-room wall and fixed balcony wall after expansion.
            // Move the whole assembly so pillows, soft contacts and frame stay aligned.
            const float wallX = -5.112f, balconyZ = -1.992f, clearance = .10f;
            sofa.transform.position += new Vector3(wallX - clearance - before.max.x, 0,
                balconyZ - clearance - before.max.z);
            var after = sofa.Bounds;
            if (Mathf.Abs(wallX - after.max.x - clearance) > .002f ||
                Mathf.Abs(balconyZ - after.max.z - clearance) > .002f)
                throw new InvalidOperationException("Sofa corner clearance is incorrect");
            var level = UnityEngine.Object.FindFirstObjectByType<LevelSession>().definition;
            level.levelId = "home-02"; level.displayName = "温馨大宅 · 自由探索";
            EditorUtility.SetDirty(level);
            PlayerSettings.bundleVersion = "0.3.3";
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../.tools/level-two");
            File.WriteAllText("../.tools/level-two/sofa-placement.txt", "before=" + before + "\nafter=" + after);
            NewHouseUpgradeBuilder.BakeVisibilityAndBuild();
        }
    }
}
