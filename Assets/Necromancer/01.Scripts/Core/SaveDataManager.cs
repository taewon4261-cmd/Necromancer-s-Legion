using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer.Core
{
    [Serializable]
    public class UpgradeSaveData
    {
        public string key;
        public int level;
    }

    /// <summary>
    /// 게임 내 모든 영속성 데이터를 JSON 형식으로 관리하는 매니저입니다.
    /// </summary>
    [Serializable]
    public class GameSaveData : ISerializationCallbackReceiver
    {
        [Header("Audio Settings")]
        public float masterVolume = 1f;
        public float bgmVolume = 0.6f;
        public float sfxVolume = 0.8f;

        [Header("Resources")]
        public int currentSoul = 0;
        public int unlockedStageLevel = 1;

        [Header("Upgrades")]
        [SerializeField] private List<UpgradeSaveData> upgradeLevels = new List<UpgradeSaveData>();

        // [OPTIMIZATION] 런타임 빠른 조회를 위한 딕셔너리 (JsonUtility는 직렬화 불가하므로 콜백으로 관리)
        public Dictionary<string, int> upgradeDict = new Dictionary<string, int>();

        public void OnBeforeSerialize()
        {
            // Dictionary -> List 동기화 (저장 전)
            upgradeLevels.Clear();
            foreach (var kvp in upgradeDict)
            {
                upgradeLevels.Add(new UpgradeSaveData { key = kvp.Key, level = kvp.Value });
            }
        }

        public void OnAfterDeserialize()
        {
            // List -> Dictionary 동기화 (로드 후)
            upgradeDict.Clear();
            foreach (var item in upgradeLevels)
            {
                upgradeDict[item.key] = item.level;
            }
        }
    }

    public class SaveDataManager : MonoBehaviour
    {
                private string savePath;
        private bool isInitialized = false;

        private GameSaveData currentData;

        public GameSaveData Data => currentData;

        private void Awake()
        {
            // [STABILITY] 초기화되지 않은 상태에서 Save가 호출되는 것을 방지하기 위해 Awake에서 즉시 경로를 설정합니다.
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = Path.Combine(Application.persistentDataPath, "savedata.json");
            }
        }

        public void Init()
        {
            if (isInitialized) return;

            // [STABILITY] 플랫폼 독립적인 저장 경로 강제 할당 (Master's Directive)
            savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            
            // 저장 폴더가 존재하지 않으면 생성
            string directory = System.IO.Path.GetDirectoryName(savePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            Load();
            isInitialized = true;
            Debug.Log($"<color=green>[SaveDataManager]</color> Initialized at: {savePath}");
        }

        /// <summary>
        /// 파일로부터 데이터를 읽어옵니다. 없으면 기본값을 생성합니다.
        /// </summary>
        public void Load()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    currentData = JsonUtility.FromJson<GameSaveData>(json);
                    Debug.Log($"<color=green>[SaveDataManager]</color> Data loaded from {savePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveDataManager] Load failed: {e.Message}");
                    currentData = new GameSaveData();
                }
            }
            else
            {
                currentData = new GameSaveData();
                Save();
            }
        }

        /// <summary>
        /// 현재 메모리의 데이터를 파일에 저장합니다.
        /// </summary>
        public void Save()
        {
            if (currentData == null) currentData = new GameSaveData();
            
            // [STABILITY] 저장 전 경로 재검증 (NullReference 방어)
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            }

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                System.IO.File.WriteAllText(savePath, json);
                // Debug.Log($"<color=green>[SaveDataManager]</color> Data saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] Save failed: {e.Message}");
            }
        }

        private void OnApplicationQuit()
        {
            Save();
            Debug.Log("<color=orange>[SaveDataManager]</color> Final save on ApplicationQuit executed.");
        }

        private void OnDisable()
        {
            // [ATOMIC] 에디터 중단이나 비활성화 시점에 데이터를 즉각 보호
            Save();
            Debug.Log("<color=orange>[SaveDataManager]</color> Final save on OnDisable executed.");
        }

        /// <summary>
        /// 특정 액션을 수행한 뒤 자동 저장합니다.
        /// </summary>
        public void UpdateAndSave(Action<GameSaveData> updateAction)
        {
            updateAction?.Invoke(currentData);
            Save();
        }

        #region Upgrade Helpers
        public int GetUpgradeLevel(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            return currentData.upgradeDict.TryGetValue(key, out int lv) ? lv : 0;
        }

        public void SetUpgradeLevel(string key, int level)
        {
            if (string.IsNullOrEmpty(key)) return;
            currentData.upgradeDict[key] = level;
            Save();
        }
        #endregion
    }
}
