using UnityEngine;

namespace Necromancer.Data
{
    public enum MinionTier { Bronze, Silver, Gold }

    /// <summary>
    /// [DATA] 미니언 해금 데이터 (ScriptableObject)
    /// 마스터의 지시에 따라 데이터 기반으로 미니언 해금 로직을 이원화합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Minion_Unlock_", menuName = "Necromancer/MinionUnlockData")]
    public class MinionUnlockSO : ScriptableObject
    {
        [Header("Identity")]
        public string minionID;      // 데이터 식별용 고유 ID (예: "Minion_02_Archer")
        public string minionTag;     // PoolManager 호출 시 사용할 프리팹 태그 (자동화 시 무시 가능)
        public Sprite minionIcon;    // 상점 그리드에 표시될 아이콘
        public RuntimeAnimatorController animatorController; // [NEW] 인게임 애니메이션 컨트롤러

        [Header("Unlock Costs")]
        public int unlockCost_Soul;     // 해금에 필요한 소울
        public int unlockCost_Essence;  // 해금에 필요한 정수량
        public string targetEnemyID;    // 어떤 적을 잡아야 이 정수가 나오는지 정의

        [Header("Tier & Description")]
        public MinionTier tier;         // 등급 (Bronze / Silver / Gold)
        public string minionName;       // 표시 이름
        [TextArea]
        public string description;      // 미니언 특징 설명

        [Header("Base Stats")]
        public float baseHp = 50f;
        public float baseDamage = 15f;
        public float baseSpeed = 3f;
        public float attackRange = 1.5f; // 1.5 이하면 근접 공격, 그 이상이면 원거리 공격
        public float baseAttackSpeed = 1.0f; // [NEW] 초당 공격 횟수 (1.0 = 1초에 1번)
        public int targetPriority = 5;   // [NEW] 적의 타겟팅 우선순위 (높을수록 먼저 공격당함)
    }
}
