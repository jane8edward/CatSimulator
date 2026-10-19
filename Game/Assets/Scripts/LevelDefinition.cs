using System;
using UnityEngine;

namespace CatSimulator
{
    [CreateAssetMenu(menuName = "Cat Simulator/Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public string levelId = "living-room-01";
        public string displayName = "客厅里的小淘气";
        public string[] requiredObjectives = { "spill-cup", "damage-curtain" };
        public string[] objectiveLabels = Array.Empty<string>();
        public Bounds activityBounds = new Bounds(new Vector3(0, 1.5f, 0), new Vector3(7, 4, 6));
        public Vector3 spawnPosition = new Vector3(0, 0.02f, -1.2f);
        public float spawnYaw;
        [Range(.25f,2)] public float playerScale = 1;
        public Color daylightColor = new Color(1, .91f, .78f);
        public Vector3 sunlightEuler = new Vector3(42, -32, 0);
        public LevelArea[] areas = Array.Empty<LevelArea>();
        public bool IsExploration => requiredObjectives.Length == 0;
        public string LocationAt(Vector3 position)
        {
            foreach (var area in areas) if (area.bounds.Contains(position)) return area.name;
            return levelId == "living-room-01" ? (position.x > 4.25f ? "卧室" : "客厅") : displayName;
        }
    }

    [Serializable] public sealed class LevelArea { public string name; public Bounds bounds; }
}
