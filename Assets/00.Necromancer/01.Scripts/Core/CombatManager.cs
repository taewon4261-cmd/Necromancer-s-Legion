using UnityEngine;

namespace Necromancer.Core
{
    /// <summary>
    /// 인게임 전투 데이터와 배율, 진행 상태를 관리합니다.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [Header("Difficulty Modifiers")]
        public float enemyHpMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;
        public float goldGainMultiplier = 1f;

        public void Init()
        {
            ResetModifiers();
            Debug.Log("[CombatManager] Initialized.");
        }

        /// <summary>
        /// 스테이지 데이터에 따라 인게임 난이도 배율을 설정합니다.
        /// </summary>
        public void SetupStageModifiers(StageDataSO stage)
        {
            if (stage == null)
            {
                ResetModifiers();
                return;
            }

            enemyHpMultiplier = stage.enemyHpMultiplier;
            enemyDamageMultiplier = stage.enemyDamageMultiplier;
            goldGainMultiplier = stage.goldGainMultiplier;
            
            Debug.Log($"[CombatManager] Stage Modifiers Applied: {stage.stageName}");
        }

        public void ResetModifiers()
        {
            enemyHpMultiplier = 1f;
            enemyDamageMultiplier = 1f;
            goldGainMultiplier = 1f;
        }
    }
}
