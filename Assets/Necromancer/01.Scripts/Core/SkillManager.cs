// File: Assets/Necromancer/01.Scripts/Core/SkillManager.cs
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// 게임 내 특성(스킬)을 총괄하는 관리자 (GameManager 하위에 부착).
    /// 보유한 모든 SkillData 중 무작위 3개를 뽑아 반환하는 역할을 수행합니다.
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        [Header("Skill Database Reference")]
        [Tooltip("모든 스킬 목록이 담긴 단 하나의 통합 DB 에셋을 여기에 연결합니다.")]
        public SkillDatabase skillDB;

        // --- [이벤트 시스템: 스탯 변경 알림용] ---
        public static System.Action OnMinionStatsChanged;
        public static System.Action OnPlayerStatsChanged;

        public bool hasToxicBlade = false;
        public bool hasFrostWeapon = false;
        public bool hasBloodFrenzy = false;
        public bool hasCursedStigma = false;
        public bool hasGiantHunter = false;

        [Header("Global Stats")]
        public float globalMinionHpBonusRatio = 1f;
        public float globalMinionDamageBonusRatio = 1f;
        public float globalMinionSpeedBonusRatio = 1f;
        public int currentMaxMinions = 50;
        public float vampiricChance = 0f;
        public float minionExplosionDamage = 0f;

        /// <summary>
        /// 안전하게 초기화용으로 비워둡니다.
        /// </summary>
        public void Init()
        {
            if (skillDB == null || skillDB.allSkills.Count == 0)
            {
                Debug.LogWarning("[SkillManager] 통합 스킬 DB가 연결되지 않았거나, 내부 스킬 목록이 비어있습니다!");
            }

            // [추가] 로비 업그레이드 수치를 인게임 초기 버프에 반영
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                var side = GameManager.Instance.Resources;
                globalMinionHpBonusRatio = 1f + side.GetUpgradeValue(UpgradeStatType.Health) * 0.1f; // 레벨당 10%
                globalMinionDamageBonusRatio = 1f + side.GetUpgradeValue(UpgradeStatType.AttackDamage) * 0.1f;
                globalMinionSpeedBonusRatio = 1f + side.GetUpgradeValue(UpgradeStatType.MoveSpeed) * 0.05f;
                currentMaxMinions = 50 + Mathf.FloorToInt(side.GetUpgradeValue(UpgradeStatType.StartMinionCount));
                
                Debug.Log($"[SkillManager] Lobby Upgrades Applied. HP Ratio: {globalMinionHpBonusRatio}, DMG Ratio: {globalMinionDamageBonusRatio}");
            }
        }

        /// <summary>
        /// 등록된 전체 스킬 풀(Pool)에서 중복을 허용하지 않고 무작위로 3개의 카드를 뽑습니다.
        /// (리프레시 광고 버튼 클릭 시에도 똑같이 이 함수를 호출합니다.)
        /// </summary>
        /// <returns>뽑힌 3개의 스킬 데이터 리스트</returns>
        public List<SkillData> GetRandomSkillsForLevelUp(int count = 3)
        {
            List<SkillData> result = new List<SkillData>();

            if (skillDB == null || skillDB.allSkills.Count < count)
            {
                Debug.LogError($"[SkillManager] 스킬 DB가 없거나, 등록된 스킬 개수가 뽑을 개수({count})보다 적습니다.");
                return skillDB != null ? skillDB.allSkills : result; // 에러 방지용 전체 반환
            }

            // 원본 리스트 손상을 막기 위해 임시 복사본 생성 후 셔플(Fisher-Yates) 방식으로 무작위 3개 추출
            List<SkillData> tempPool = new List<SkillData>(skillDB.allSkills);
            
            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, tempPool.Count);
                result.Add(tempPool[randomIndex]);
                tempPool.RemoveAt(randomIndex); // 중복 등장 방지
            }

            return result;
        }

        /// <summary>
        /// 플레이어가 UI 카드 버튼을 클릭하여 스킬을 선택했을 때 도달하는 최종 함수.
        /// (UIManager에서 카드 클릭 시 이 함수를 호출할 예정)
        /// </summary>
        /// <param name="selectedSkill">사용자가 고른 스킬 데이터</param>
        public void ApplySkill(SkillData selectedSkill)
        {
            if (selectedSkill == null) return;

            Debug.Log($"[SkillManager] 선택된 스킬: <color=yellow>{selectedSkill.skillName}</color> 적용 완료!");

            PlayerController player = (GameManager.Instance != null && GameManager.Instance.playerTransform != null) 
                ? GameManager.Instance.playerTransform.GetComponent<PlayerController>() 
                : null;

            bool isMinionUpdateNeeded = false;
            bool isPlayerUpdateNeeded = false;

            switch (selectedSkill.type)
            {
                // --- [본체 생존 및 유틸 계열] ---
                case SkillType.ScytheUpgrade:
                    isPlayerUpdateNeeded = true; 
                    break;
                case SkillType.SoulMagnet:
                    GameManager.Instance.magnetRadius *= 1.2f;
                    break;
                case SkillType.LightStep:
                    if (player != null) player.moveSpeed *= 1.1f;
                    isPlayerUpdateNeeded = true;
                    break;
                case SkillType.SturdySkeleton:
                    if (player != null)
                    {
                        float hpAdd = 20f;
                        player.maxHp += hpAdd;
                        player.currentHp += hpAdd;
                    }
                    isPlayerUpdateNeeded = true;
                    break;
                case SkillType.RegeneratingBone:
                    // 본체 초당 재생 0.5% 추가 (PlayerController에서 처리하도록 플래그만 변경 가능)
                    if (player != null) player.AddRegen(1f); 
                    break;
                case SkillType.AuraOfDeath:
                    // 본체 주변 죽음의 오라 활성화
                    if (player != null) player.EnableDeathAura(true);
                    break;
                case SkillType.PhantomEvasion:
                    // 회피 확률 10% 추가
                    if (player != null) player.dodgeChance += 0.1f;
                    break;

                // --- [군단 유틸 및 방어 계열] ---
                case SkillType.Leadership:
                    currentMaxMinions += 5;
                    // 최대 수치는 즉시 반영되므로 별도 이벤트 불필요할 수 있으나 전달
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.ToughHide:
                    globalMinionHpBonusRatio += 0.2f;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.SwiftMarch:
                    globalMinionSpeedBonusRatio += 0.15f;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.EchoOfResurrection:
                    GameManager.Instance.baseReviveChance += 10f;
                    break;
                case SkillType.VampiricTeeth:
                    vampiricChance += 0.02f;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.ChainExplosion:
                    minionExplosionDamage += 15f;
                    isMinionUpdateNeeded = true;
                    break;

                // --- [군단 공격 및 상태이상 계열] ---
                case SkillType.BoneGrindingStrike:
                    globalMinionDamageBonusRatio += 0.15f;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.ToxicBlade:
                    hasToxicBlade = true;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.FrostDippedWeapon:
                    hasFrostWeapon = true;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.BloodFrenzy:
                    hasBloodFrenzy = true;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.PiercingBone:
                    // 궁수 발사체 관통 +1 (전용 플래그 설정)
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.CursedStigma:
                    hasCursedStigma = true;
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.GiantHunter:
                    hasGiantHunter = true;
                    isMinionUpdateNeeded = true;
                    break;
                    
                default:
                    Debug.LogWarning($"-> 누락된 기능 구현: {selectedSkill.type}");
                    break;
            }

            // [핵심] 변경된 스탯을 실시간으로 전파하여 필드의 모든 유닛을 즉시 강화함
            if (isMinionUpdateNeeded) OnMinionStatsChanged?.Invoke();
            if (isPlayerUpdateNeeded) OnPlayerStatsChanged?.Invoke();
        }
    }
}
