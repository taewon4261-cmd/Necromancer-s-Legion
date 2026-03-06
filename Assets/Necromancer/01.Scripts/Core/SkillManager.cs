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

        [Header("Global Minion Buffs")]
        [Tooltip("현재 살아있거나 앞으로 태어날 모든 미니언에게 합산될 추가 체력 %")]
        public float globalMinionHpBonusRatio = 1f; // 1.0 = 100% (기본값)
        
        [Tooltip("미니언 추가 이동 속도 %")]
        public float globalMinionSpeedBonusRatio = 1f;
        
        [Tooltip("미니언 추가 공격력 %")]
        public float globalMinionDamageBonusRatio = 1f;

        [Tooltip("통솔력 (동시 유지 가능 미니언 최대 수)")]
        public int currentMaxMinions = 50; // 임시 기본값
        
        [Tooltip("미니언 흡혈 확률 (0~1)")]
        public float vampiricChance = 0f;
        
        [Tooltip("미니언 사망 시 광역 폭발 데미지 (0이면 폭발 안함)")]
        public float minionExplosionDamage = 0f;

        // 추가 상태이상 등은 임시로 boolean이나 enum 리스트로 관리 (1주차 스펙용)
        public bool hasToxicBlade = false;
        public bool hasFrostWeapon = false;
        public bool hasBloodFrenzy = false;
        public bool hasCursedStigma = false;
        public bool hasGiantHunter = false;

        /// <summary>
        /// 안전하게 초기화용으로 비워둡니다.
        /// </summary>
        public void Init()
        {
            if (skillDB == null || skillDB.allSkills.Count == 0)
            {
                Debug.LogWarning("[SkillManager] 통합 스킬 DB가 연결되지 않았거나, 내부 스킬 목록이 비어있습니다!");
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

            PlayerController player = GameManager.Instance.playerTransform.GetComponent<PlayerController>();

            switch (selectedSkill.type)
            {
                // --- [본체 생존 및 유틸 계열] ---
                case SkillType.ScytheUpgrade:
                    var weapon = FindObjectOfType<PlayerWeapon_BoneWand>();
                    if (weapon != null)
                    {
                        weapon.baseDamage *= 1.1f;
                        weapon.detectionRadius *= 1.1f;
                    }
                    break;
                case SkillType.SoulMagnet:
                    GameManager.Instance.magnetRadius *= 1.2f;
                    break;
                case SkillType.LightStep:
                    if (player != null) player.moveSpeed *= 1.1f;
                    break;
                case SkillType.SturdySkeleton:
                    if (player != null)
                    {
                        player.maxHp += 20;
                        player.currentHp += 20;
                    }
                    break;
                case SkillType.RegeneratingBone:
                    // TODO: 정기적 힐링 코루틴 혹은 InvokeRepeating 추가 (추후 구현)
                    break;
                case SkillType.AuraOfDeath:
                    // TODO: 본체 자식 오브젝트로 오라 장판 Enable
                    break;
                case SkillType.PhantomEvasion:
                    // TODO: PlayerController 피격 로직에서 확률 회피 추가
                    break;

                // --- [군단 유틸 및 방어 계열] ---
                case SkillType.Leadership:
                    currentMaxMinions += 5;
                    break;
                case SkillType.ToughHide:
                    globalMinionHpBonusRatio += 0.2f;
                    break;
                case SkillType.SwiftMarch:
                    globalMinionSpeedBonusRatio += 0.15f;
                    break;
                case SkillType.EchoOfResurrection:
                    GameManager.Instance.baseReviveChance += 10f;
                    break;
                case SkillType.VampiricTeeth:
                    vampiricChance += 0.01f;
                    break;
                case SkillType.ChainExplosion:
                    minionExplosionDamage += 10f;
                    break;

                // --- [군단 공격 및 상태이상 계열] ---
                case SkillType.BoneGrindingStrike:
                    globalMinionDamageBonusRatio += 0.15f;
                    break;
                case SkillType.ToxicBlade:
                    hasToxicBlade = true;
                    break;
                case SkillType.FrostDippedWeapon:
                    hasFrostWeapon = true;
                    break;
                case SkillType.BloodFrenzy:
                    hasBloodFrenzy = true;
                    break;
                case SkillType.PiercingBone:
                    // TODO: 해골 궁수 발사체 관통 횟수 1 증가 (궁수 구현 시 연동)
                    break;
                case SkillType.CursedStigma:
                    hasCursedStigma = true;
                    break;
                case SkillType.GiantHunter:
                    hasGiantHunter = true;
                    break;
                    
                default:
                    Debug.LogWarning($"-> 아직 누락된 스킬 연동입니다: {selectedSkill.type}");
                    break;
            }
        }
    }
}
