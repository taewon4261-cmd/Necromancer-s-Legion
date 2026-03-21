using UnityEngine;

namespace Necromancer.Core
{
    /// <summary>
    /// 로비 골드, 인게임 영혼 등 게임 내 모든 자원을 관리합니다.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        [Header("Currencies")]
        public int currentGold;      // 영구 재화 (로비용)
        public int currentSoul;      // 일시 재화 (인게임용)

        public int unlockedStageLevel; // 어디 스테이지까지 열렸는지 (1~50)

        [Header("Lobby Upgrades")]
        public LobbyUpgradeSO[] upgrades; // 인스펙터에서 모든 업그레이드 SO 연결

        public void Init()
        {
            // 데이터 로드 로직
            currentGold = PlayerPrefs.GetInt("TotalGold", 0);
            unlockedStageLevel = PlayerPrefs.GetInt("UnlockedStageLevel", 1); // 기본 1
            currentSoul = 0;

            // 로비 업그레이드 레벨 복구
            if (upgrades != null)
            {
                foreach (var upgrade in upgrades)
                {
                    if (upgrade != null)
                    {
                        upgrade.currentLevel = PlayerPrefs.GetInt("Upgrade_" + upgrade.statType.ToString(), 0);
                    }
                }
            }
            
            Debug.Log($"[ResourceManager] Initialized. Unlocked Stage: {unlockedStageLevel}, Gold: {currentGold}");
        }

        /// <summary>
        /// 특정 스탯 타입에 대해 로비에서 업그레이드된 모든 누적 수치를 가져옵니다.
        /// </summary>
        public float GetUpgradeValue(UpgradeStatType type)
        {
            float total = 0f;
            if (upgrades == null) return 0f;

            foreach (var upgrade in upgrades)
            {
                if (upgrade != null && upgrade.statType == type)
                {
                    total += upgrade.GetTotalStatValue();
                }
            }
            return total;
        }

        public bool IsStageUnlocked(int stageId)
        {
            return stageId <= unlockedStageLevel;
        }

        public void UnlockLevel(int stageId)
        {
            if (stageId > unlockedStageLevel)
            {
                unlockedStageLevel = stageId;
                PlayerPrefs.SetInt("UnlockedStageLevel", unlockedStageLevel);
            }
        }

        public void AddGold(int amount)
        {
            currentGold += amount;
            PlayerPrefs.SetInt("TotalGold", currentGold);
        }

        public bool SpendGold(int amount)
        {
            if (currentGold >= amount)
            {
                currentGold -= amount;
                PlayerPrefs.SetInt("TotalGold", currentGold);
                return true;
            }
            return false;
        }

        public void AddSoul(int amount)
        {
            currentSoul += amount;
        }

        public bool SpendSoul(int amount)
        {
            if (currentSoul >= amount)
            {
                currentSoul -= amount;
                return true;
            }
            return false;
        }
    }
}
