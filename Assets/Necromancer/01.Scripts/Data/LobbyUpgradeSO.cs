using UnityEngine;

namespace Necromancer
{
    public enum UpgradeStatType
    {
        Health,             // 체력 강화
        AttackDamage,       // 공격력 증가 (방어무시/위력)
        MagnetRange,        // 자석 범위
        StartMinionCount,   // 시작 해골 수 증가
        MoveSpeed,          // 이동속도
        SoulGain,           // 영혼 획득
        ExpGain,            // 경험치 획득
        RerollCount,        // 리롤 횟수
        AuraRange,          // 오라 범위
        MinionDamage,       // 미니언 공격력
        MinionSpeed,        // 미니언 속도
        CooldownReduction,   // 재사용 대기시간 감소
        Resurrection
    }

    /// <summary>
    /// 로비(영구 업그레이드)에서 영혼을 소비하여 영구적으로 스탯을 강화하는 데이터 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Necromancer/Lobby Upgrade Data")]
    public class LobbyUpgradeSO : ScriptableObject
    {
        [Header("Upgrade Info")]
        public string upgradeName;
        public string saveKey; // 데이터 저장 키 (자석 업그레이드, 체력 등)
        public UpgradeStatType statType;
        public Sprite icon;
        public Sprite iconFrame; // [DESIGN] 업그레이드 전용 아이콘 프레임 (인게임 스킬과 차별화)
        [TextArea] public string description;

        [Header("Unlock Requirements")]
        public LobbyUpgradeSO requiredUpgrade;
        public int requiredLevel = 0;

        [Header("Level & Cost")]
        public int currentLevel = 0;
        public int maxLevel = 10;

        [Tooltip("기초 비용 (업그레이드 전 초기 비용)")]
        public int baseCost = 100;

        [Tooltip("업그레이드당 비용 증가 계수 (기초 비용 * (costIncreaseFactor ^ currentLevel))")]
        public float costIncreaseFactor = 1.5f;

        [Header("Stat Values")]
        [Tooltip("레벨당 스탯 증가치 (1레벨당 오르는 값)")]
        public float valuePerLevel = 1.0f;

        /// <summary>
        /// 현재 잠금 해제 조건이 만족되었는지 확인합니다.
        /// </summary>
        public bool IsUnlocked()
        {
            if (requiredUpgrade == null) return true;
            return requiredUpgrade.currentLevel >= requiredLevel;
        }

        /// <summary>
        /// 현재 레벨에서 다음 레벨로 올리기 위해 필요한 비용을 계산합니다.
        /// </summary>
        public int GetUpgradeCost()
        {
            if (currentLevel >= maxLevel) return -1;
            return Mathf.FloorToInt(baseCost * Mathf.Pow(costIncreaseFactor, currentLevel));
        }

        /// <summary>
        /// 현재 레벨까지 적용된 총 스탯 증가량을 반환합니다.
        /// </summary>
        public float GetTotalStatValue()
        {
            return currentLevel * valuePerLevel;
        }

        /// <summary>
        /// 저장된 데이터를 기반으로 업그레이드 레벨을 로드합니다.
        /// </summary>
        public void LoadLevel()
        {
            if (string.IsNullOrEmpty(saveKey))
            {
                saveKey = $"Upgrade_{statType}_Lv"; // 기본값 자동 생성
            }
            currentLevel = PlayerPrefs.GetInt(saveKey, 0);
        }
    }
}