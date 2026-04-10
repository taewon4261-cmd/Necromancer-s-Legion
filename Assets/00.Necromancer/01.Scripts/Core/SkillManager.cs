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

        // --- [공격 효과 레지스트리 (Attack Effect Registry)] ---
        // 모든 스킬 플래그를 삭제하고, 공격 시 적에게 부여할 모디파이어 설계도들을 관리합니다.
        public List<IUnitModifierTemplate> activeAttackModifiers = new List<IUnitModifierTemplate>();

        private void RegisterAttackModifier(IUnitModifierTemplate template)
        {
            // 중복 방지 (같은 아이디면 덮어쓰거나 무시)
            for (int i = 0; i < activeAttackModifiers.Count; i++)
            {
                if (activeAttackModifiers[i].ModifierId == template.ModifierId)
                {
                    // 수치 강화 로직 (선택적)
                    activeAttackModifiers[i] = template;
                    return;
                }
            }
            activeAttackModifiers.Add(template);
        }

        public void ApplyAttackEffects(UnitBase target)
        {
            if (target == null || target.IsDead) return;
            for (int i = 0; i < activeAttackModifiers.Count; i++)
            {
                target.AddModifier(activeAttackModifiers[i].CreateModifier());
            }
        }
        public float globalMinionHpBonusRatio = 1f;
        public float globalMinionDamageBonusRatio = 1f;
        public float globalMinionSpeedBonusRatio = 1f;
        public float globalMinionAttackSpeedBonusRatio = 1f; // [NEW] 미니언 공격 속도 버프
        public int currentMaxMinions = 50;
        public int globalExtraProjectiles = 0; 
        public float vampiricChance = 0f;
        public float vampiricHealAmount = 3f; // [NEW] 레벨업 시 증가할 회복량
        public float minionExplosionDamage = 0f;

        [Header("Upgrade State")]
        public int remainingRerolls = 0;
        public int totalResurrections = 0;

        [Header("Player Weapon Stats")]
        public int playerWeaponLevel = 1;
        public float globalPlayerWeaponDamageRatio = 1f;
        public float globalPlayerWeaponFireRateRatio = 1f;

        /// <summary>
        /// 안전하게 초기화용으로 비워둡니다.
        /// </summary>
        public void Init()
        {
            // [STABILITY] 씬 재시작 시 이전 판의 스킬 효과가 중첩되는 것을 방지
            activeAttackModifiers.Clear();

            if (skillDB == null || skillDB.allSkills.Count == 0)
            {
                Debug.LogWarning("[SkillManager] 통합 스킬 DB가 연결되지 않았거나, 내부 스킬 목록이 비어있습니다!");
            }

            // [추가] 로비 업그레이드 수치를 인게임 초기 버프에 반영
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                var side = GameManager.Instance.Resources;
                // 미니언 업그레이드
                globalMinionDamageBonusRatio = 1f + (side.GetUpgradeValue(UpgradeStatType.MinionDamage) * 0.1f);
                globalMinionSpeedBonusRatio = 1f + (side.GetUpgradeValue(UpgradeStatType.MinionSpeed) * 0.05f);
                globalMinionHpBonusRatio = 1f + (side.GetUpgradeValue(UpgradeStatType.MinionHealth) * 0.1f);
                globalMinionAttackSpeedBonusRatio = 1f + (side.GetUpgradeValue(UpgradeStatType.MinionAttackSpeed) * 0.1f);
                
                // 유틸리티 업그레이드
                currentMaxMinions = 50 + Mathf.FloorToInt(side.GetUpgradeValue(UpgradeStatType.StartMinionCount));
                remainingRerolls = 1; // 리롤은 상시 1회로 기본 제공
                totalResurrections = Mathf.FloorToInt(side.GetUpgradeValue(UpgradeStatType.Resurrection));

                Debug.Log($"[SkillManager] Lobby Upgrades Applied. Rerolls: {remainingRerolls}, Resurrects: {totalResurrections}");
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
        /// 최종 선정된 스킬의 실제 효과를 캐릭터와 시스템에 적용합니다.
        /// </summary>
        /// <param name="data">적용할 스킬 데이터</param>
        public void LearnSkill(SkillData data)
        {
            if (data == null) return;

            PlayerController player = (GameManager.Instance != null && GameManager.Instance.playerTransform != null) 
                ? GameManager.Instance.playerTransform.GetComponent<PlayerController>() 
                : null;

            bool isMinionUpdateNeeded = false;
            bool isPlayerUpdateNeeded = false;

            // 1. 특정 이름 기반 예외 처리 (사용자 요청: 공격력 증가/체력 회복)
            if (data.skillName == "공격력 증가")
            {
                if (player != null) player.bodySlamDamage += 10f; // 공격력 10 증가 (예시 수치)
                isPlayerUpdateNeeded = true;
            }
            else if (data.skillName == "체력 회복")
            {
                if (player != null) player.currentHp = player.maxHp; // 체력을 최대치로 채움
                isPlayerUpdateNeeded = true;
            }

            // 2. 기존 SkillType 기반 로직 실행
            switch (data.type)
            {
                // --- [본체 생존 및 유틸 계열] ---
                case SkillType.ScytheUpgrade:
                    playerWeaponLevel++;
                    globalPlayerWeaponDamageRatio += 0.2f; // [BALANCE] 레벨당 복리 대신 20% 합연산 증가
                    isPlayerUpdateNeeded = true; 
                    break;
                case SkillType.SoulMagnet:
                    GameManager.Instance.magnetRadius *= 1.2f;
                    isPlayerUpdateNeeded = true;
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
                    if (vampiricChance <= 0f) vampiricChance = 0.3f; // 처음 배울 때 30% 고정
                    else vampiricHealAmount += 3f; // 이후 배울 때마다 회복량 3씩 증가 (3 -> 6 -> 9...)
                    
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
                    RegisterAttackModifier(new PoisonModifierTemplate(3f, 2f));
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.FrostDippedWeapon:
                    RegisterAttackModifier(new FrostModifierTemplate(2f, 0.3f));
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.BloodFrenzy:
                    // [REGISTRY] 흡혈 효과 등으로 전환 가능 (Bleeding 예시로 대체하거나 모디파이어 추가)
                    RegisterAttackModifier(new BleedingModifierTemplate());
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.CursedStigma:
                    RegisterAttackModifier(new StigmaModifierTemplate());
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.GiantHunter:
                    // [REGISTRY] 보스 추가 피해 등을 위한 전용 모디파이어 등록
                    RegisterAttackModifier(new StunModifierTemplate()); // 실험용으로 Stun 등록
                    isMinionUpdateNeeded = true;
                    break;
                case SkillType.PiercingBone:
                    globalExtraProjectiles++; // 한 번에 쏘는 발사체 수 1개 증가
                    break;
                    
                default:
                    Debug.LogWarning($"-> 누락된 기능 구현: {data.type}");
                    break;
            }

            // [핵심] 변경된 스탯을 실시간으로 전파하여 필드의 모든 유닛을 즉시 강화함
            if (isMinionUpdateNeeded) OnMinionStatsChanged?.Invoke();
            if (isPlayerUpdateNeeded) OnPlayerStatsChanged?.Invoke();
        }
    }
}
