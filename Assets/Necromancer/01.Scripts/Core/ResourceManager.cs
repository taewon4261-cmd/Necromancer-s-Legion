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
            currentGold = PlayerPrefs.GetInt(Necromancer.Systems.UIConstants.Key_TotalGold, 0);
            unlockedStageLevel = PlayerPrefs.GetInt("UnlockedStageLevel", 1); 
            upgradeList = Resources.LoadAll<LobbyUpgradeSO>("Upgrades").ToList();
            foreach (var u in upgradeList) if (u != null) u.currentLevel = PlayerPrefs.GetInt(u.saveKey, 0);
        }
        public float GetUpgradeValue(UpgradeStatType t) => upgradeList?.Where(u => u != null && u.statType == t).Sum(u => u.GetTotalStatValue()) ?? 0f;
        public bool IsStageUnlocked(int id) => id <= unlockedStageLevel;
        public void UnlockLevel(int id) { if (id > unlockedStageLevel) { unlockedStageLevel = id; PlayerPrefs.SetInt("UnlockedStageLevel", id); } }
        public void AddGold(int a) { currentGold += a; Save(); }
        public bool SpendGold(int a) { if (currentGold >= a) { currentGold -= a; Save(); return true; } return false; }
        private void Save() { PlayerPrefs.SetInt(Necromancer.Systems.UIConstants.Key_TotalGold, currentGold); PlayerPrefs.Save(); }
    }
}
