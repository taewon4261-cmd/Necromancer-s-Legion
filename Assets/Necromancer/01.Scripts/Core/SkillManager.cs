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

            Debug.Log($"[SkillManager] 선택된 스킬: <color=yellow>{selectedSkill.skillName}</color> 적용 중...");

            // TODO: 타입별로 분기하여 PlayerController 또는 MinionAI에 영구 능력치 브로드캐스트
            switch (selectedSkill.type)
            {
                case SkillType.WeaponPowerUp:
                    // 사신의 낫: 뼈 투사체 데미지 일괄 적용
                    Debug.Log("-> 본체 사신의 낫 공격력 및 사거리 적용 완료!");
                    break;
                    
                case SkillType.OrbitalShield:
                    // 부유하는 뼈 바가지: 본체 주변 오라 활성화
                    Debug.Log("-> 본체 주변 부유 뼈다구(가디언) 방어막 마법 활성화 완료!");
                    break;
                    
                default:
                    Debug.Log($"-> 등록되지 않은 스킬 동작입니다: {selectedSkill.type}");
                    break;
            }
        }
    }
}
