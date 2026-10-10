using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatSimulator
{
    public sealed class LevelSession : MonoBehaviour
    {
        public LevelDefinition definition;
        readonly HashSet<string> completed = new HashSet<string>();
        readonly HashSet<string> discoveries = new HashSet<string>();
        public event Action Changed;
        public bool IsComplete { get; private set; }
        public int DiscoveryCount => discoveries.Count;
        public int Stars => Mathf.Min(5, 3 + discoveries.Count);
        public string CatName { get; private set; }
        public bool HasObjective(string id) => completed.Contains(id);

        void Awake()
        {
            CatName = PlayerPrefs.GetString("cat-name", "团团");
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.skinWeights = SkinWeights.Unlimited;
        }

        public void CompleteObjective(string id)
        {
            if (Array.IndexOf(definition.requiredObjectives, id) < 0) return;
            if (!completed.Add(id)) return;
            IsComplete = true;
            foreach (var required in definition.requiredObjectives)
                IsComplete &= completed.Contains(required);
            Changed?.Invoke();
        }

        public void Discover(string id)
        {
            if (discoveries.Add(id)) Changed?.Invoke();
        }

        public void Rename(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0) return;
            CatName = trimmed.Substring(0, Mathf.Min(12, trimmed.Length));
            PlayerPrefs.SetString("cat-name", CatName);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public void Replay()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
