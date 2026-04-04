using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace Necromancer.Core {
    public class ResourceManager : MonoBehaviour {
        public int currentSoul;
        public int currentSessionSoul; // 이번 판에서 얻은 실시간 소울 (UI 표현용)
        public int unlockedStageLevel;
        private List<LobbyUpgradeSO> upgradeList = new List<LobbyUpgradeSO>();
        public void Init() {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null) {
                var data = GameManager.Instance.SaveData.Data;
                currentSoul = data.currentSoul;
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

        /// <summary>
        /// 인게임에서 적을 처치하거나 보석을 먹어 소울을 획득할 때 호출
        /// </summary>
        public void AddSoul(int a) { 
            currentSessionSoul += a; 
            // UI에는 세션 소울(이번 판 획득량)만 전달하여 0부터 시작하게 함
            GameManager.BroadcastSoul(currentSessionSoul);
        }

        /// <summary>
        /// 게임이 정상 종료(클리어/실패)될 때 이번 판에서 얻은 소울을 전체 지갑에 합산하고 저장
        /// </summary>
        public void CommitSessionSoul() {
            currentSoul += currentSessionSoul;
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                GameManager.Instance.SaveData.Save();
                Debug.Log($"<color=green>[ResourceManager]</color> Session soul committed: {currentSessionSoul}. Total: {currentSoul}");
            }
            // 세션 소울은 초기화하지 않고 결과창 출력용으로 유지 (필요 시 세션 시작 때 리셋)
        }

        public bool SpendSoul(int a) { 
            if (currentSoul >= a) { 
                currentSoul -= a; 
                GameManager.BroadcastSoul(currentSoul);
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                    GameManager.Instance.SaveData.Save();
                }
                return true; 
            } return false; 
        }
    }
}
