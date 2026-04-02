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
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null) {
                var data = GameManager.Instance.SaveData.Data;
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
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.unlockedStageLevel = id;
                    GameManager.Instance.SaveData.Save();
                }
            } 
        }
        public void AddGold(int a) { 
            currentGold += a; 
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                GameManager.Instance.SaveData.Data.currentGold = currentGold;
                GameManager.Instance.SaveData.Save();
            }
        }
        public bool SpendGold(int a) { 
            if (currentGold >= a) { 
                currentGold -= a; 
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.currentGold = currentGold;
                    GameManager.Instance.SaveData.Save();
                }
                return true; 
            } return false; 
        }
    }
}
