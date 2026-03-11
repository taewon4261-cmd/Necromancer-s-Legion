using UnityEngine;

namespace Necromancer
{
    public enum UpgradeStatType
    {
        Health,         // 시작 체력
        AttackDamage,   // 기본 공격력 (본체/미니언)
        MagnetRange,    // 자석 범위
        StartMinionCount, // 시작 시 기본 미니언 수
        MoveSpeed,      // 이동 속도
        GoldGain        // 골드 획득률
    }

    /// <summary>
    /// 로비(타이틀 상점)에서 골드를 소모하여 영구적으로 올릴 수 있는 스탯 강화 데이터입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Necromancer/Lobby Upgrade Data")]
    public class LobbyUpgradeSO : ScriptableObject
    {
        [Header("Upgrade Info")]
        public string upgradeName;
        public UpgradeStatType statType;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Level & Cost")]
        public int currentLevel = 0;
        public int maxLevel = 10;
        
        [Tooltip("고정 비용 (레벨업 시 기본 소모 골드)")]
        public int baseCost = 100;
        
        [Tooltip("레벨당 비용 증가 계수 (기존 비용 + (level * costIncreaseRate))")]
        public float costIncreaseFactor = 1.5f;

        [Header("Stat Values")]
        [Tooltip("기본 증가 수치 (1레벨당 이만큼 증가)")]
        public float valuePerLevel = 1.0f;

        /// <summary>
        /// 현재 레벨에서 다음 레벨로 올리기 위한 비용을 계산합니다.
        /// </summary>
        public int GetUpgradeCost()
        {
            if (currentLevel >= maxLevel) return -1;
            return Mathf.FloorToInt(baseCost * Mathf.Pow(costIncreaseFactor, currentLevel));
        }

        /// <summary>
        /// 실제 인게임에 적용될 총 누적 강화 수치를 반환합니다.
        /// </summary>
        public float GetTotalStatValue()
        {
            return currentLevel * valuePerLevel;
        }
    }
}
