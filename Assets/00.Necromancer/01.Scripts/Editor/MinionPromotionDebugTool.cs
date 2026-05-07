#if UNITY_EDITOR
using System.Collections.Generic;
using Necromancer.Core;
using UnityEditor;
using UnityEngine;

namespace Necromancer.Editor
{
    public static class MinionPromotionDebugTool
    {
        [MenuItem("Tools/Necromancer/Debug/Full Minion Promotion Test Setup")]
        public static void FullMinionPromotionTestSetup()
        {
            if (!TryGetRuntimeManagers(out var gameManager, out var saveData, out var resources)) return;

            AddTestResourcesInternal(gameManager, saveData, resources);
            UnlockAllMinionsFiveStarsInternal(gameManager, saveData);
            SaveAndRefresh(gameManager, saveData, resources);

            Debug.Log("[Debug] Full minion promotion test setup complete.");
        }

        [MenuItem("Tools/Necromancer/Debug/Unlock All Minions 5 Stars")]
        public static void UnlockAllMinionsFiveStars()
        {
            if (!TryGetRuntimeManagers(out var gameManager, out var saveData, out var resources)) return;

            UnlockAllMinionsFiveStarsInternal(gameManager, saveData);
            SaveAndRefresh(gameManager, saveData, resources);

            Debug.Log("[Debug] All minions unlocked at 5 stars.");
        }

        [MenuItem("Tools/Necromancer/Debug/Add Test Resources")]
        public static void AddTestResources()
        {
            if (!TryGetRuntimeManagers(out var gameManager, out var saveData, out var resources)) return;

            AddTestResourcesInternal(gameManager, saveData, resources);
            SaveAndRefresh(gameManager, saveData, resources);

            Debug.Log("[Debug] Test resources granted.");
        }

        [MenuItem("Tools/Necromancer/Debug/Reset Minion Stars")]
        public static void ResetMinionStars()
        {
            if (!TryGetRuntimeManagers(out var gameManager, out var saveData, out var resources)) return;

            var save = saveData.Data;
            if (save.minionStars == null)
                save.minionStars = new Dictionary<string, int>();
            else
                save.minionStars.Clear();

            save.minionStars["SkeletonWarrior"] = 1;
            SaveAndRefresh(gameManager, saveData, resources);

            Debug.Log("[Debug] Minion stars reset. SkeletonWarrior remains at 1 star.");
        }

        private static bool TryGetRuntimeManagers(out GameManager gameManager, out SaveDataManager saveData, out ResourceManager resources)
        {
            gameManager = GameManager.Instance;
            saveData = gameManager != null ? gameManager.SaveData : null;
            resources = gameManager != null ? gameManager.Resources : null;

            if (!Application.isPlaying || gameManager == null || saveData == null || saveData.Data == null || resources == null)
            {
                Debug.LogError("[Debug] Enter Play Mode and wait for GameManager, SaveData, and ResourceManager initialization.");
                return false;
            }

            return true;
        }

        private static void AddTestResourcesInternal(GameManager gameManager, SaveDataManager saveData, ResourceManager resources)
        {
            var save = saveData.Data;
            save.currentSoul = 999999;
            resources.currentSoul = 999999;

            if (save.minionEssences == null)
                save.minionEssences = new Dictionary<string, int>();

            var minions = gameManager.minionUnlockDataList;
            for (int i = 0; i < minions.Count; i++)
            {
                var minion = minions[i];
                if (minion == null || string.IsNullOrEmpty(minion.targetEnemyID)) continue;
                save.minionEssences[minion.targetEnemyID] = 999999;
            }
        }

        private static void UnlockAllMinionsFiveStarsInternal(GameManager gameManager, SaveDataManager saveData)
        {
            var save = saveData.Data;
            if (save.minionStars == null)
                save.minionStars = new Dictionary<string, int>();

            var minions = gameManager.minionUnlockDataList;
            for (int i = 0; i < minions.Count; i++)
            {
                var minion = minions[i];
                if (minion == null || string.IsNullOrEmpty(minion.minionID)) continue;
                save.minionStars[minion.minionID] = Mathf.Clamp(minion.maxStars, 1, 5);
            }

            if (!save.minionStars.ContainsKey("SkeletonWarrior") || save.minionStars["SkeletonWarrior"] < 1)
                save.minionStars["SkeletonWarrior"] = 1;
        }

        private static void SaveAndRefresh(GameManager gameManager, SaveDataManager saveData, ResourceManager resources)
        {
            saveData.Save();
            gameManager.unitManager?.UpdateUnlockedMinionPool();
            GameManager.BroadcastSoul(resources.currentSoul);
        }
    }
}
#endif
