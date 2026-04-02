using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace Necromancer.Core {
    public class ResourceManager : MonoBehaviour {
        public int currentGold;
        public int currentSoul;
        public int unlockedStageLevel;
        private List<LobbyUpgradeSO> upgradeList = new List<LobbyUpgradeSO>();
        public void Init() {
            if (SaveDataManager.Instance != null && SaveDataManager.Instance.Data != null) {
                var data = SaveDataManager.Instance.Data;
                currentGold = data.currentGold;
                unlockedStageLevel = data.unlockedStageLevel;
            }
            upgradeList = Resources.LoadAll<LobbyUpgradeSO>("Upgrades").ToList();
            // 업그레이드 레벨도 추후 SaveData에 추가할 수 있도록 구조화할 필요 있음
            foreach (var u in upgradeList) if (u != null) u.currentLevel = PlayerPrefs.GetInt(u.saveKey, 0);
        }
        public float GetUpgradeValue(UpgradeStatType t) => upgradeList?.Where(u => u != null && u.statType == t).Sum(u => u.GetTotalStatValue()) ?? 0f;
        public bool IsStageUnlocked(int id) => id <= unlockedStageLevel;
        public void UnlockLevel(int id) { 
            if (id > unlockedStageLevel) { 
                unlockedStageLevel = id; 
                if (SaveDataManager.Instance != null) {
                    SaveDataManager.Instance.Data.unlockedStageLevel = id;
                    SaveDataManager.Instance.Save();
                }
            } 
        }
        public void AddGold(int a) { 
            currentGold += a; 
            if (SaveDataManager.Instance != null) {
                SaveDataManager.Instance.Data.currentGold = currentGold;
                SaveDataManager.Instance.Save();
            }
        }
        public bool SpendGold(int a) { 
            if (currentGold >= a) { 
                currentGold -= a; 
                if (SaveDataManager.Instance != null) {
                    SaveDataManager.Instance.Data.currentGold = currentGold;
                    SaveDataManager.Instance.Save();
                }
                return true; 
            } return false; 
        }
    }
}
