using System;
using System.IO;
using UnityEngine;

namespace Necromancer.Core
{
    /// <summary>
    /// 게임 내 모든 영속성 데이터를 JSON 형식으로 관리하는 매니저입니다.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        [Header("Audio Settings")]
        public float masterVolume = 1f;
        public float bgmVolume = 0.6f;
        public float sfxVolume = 0.8f;

        [Header("Resources")]
        public int currentSoul = 0;
        public int unlockedStageLevel = 1;

        // 필요 시 더 많은 데이터 추가 가능 (예: 업그레이드 레벨 등)
    }

    public class SaveDataManager : MonoBehaviour
    {
        private string savePath;
        private GameSaveData currentData;

        public GameSaveData Data => currentData;

        public void Init()
        {
            savePath = Path.Combine(Application.persistentDataPath, "savedata.json");
            Load();
            Debug.Log("<color=green>[SaveDataManager]</color> Initialized by GameManager.");
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
            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(savePath, json);
                Debug.Log($"<color=green>[SaveDataManager]</color> Data saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// 특정 액션을 수행한 뒤 자동 저장합니다.
        /// </summary>
        public void UpdateAndSave(Action<GameSaveData> updateAction)
        {
            updateAction?.Invoke(currentData);
            Save();
        }
    }
}
