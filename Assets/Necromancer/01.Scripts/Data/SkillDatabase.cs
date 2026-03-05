// File: Assets/Necromancer/01.Scripts/Data/SkillDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// 게임 내 모든 스킬(SkillData)을 한 곳에서 통합 관리하는 데이터베이스 에셋.
    /// (기획자 요청: 현업의 모듈화 방식 적용)
    /// </summary>
    [CreateAssetMenu(fileName = "New Skill Database", menuName = "Necromancer/Skill Database")]
    public class SkillDatabase : ScriptableObject
    {
        [Tooltip("게임 내 존재하는 전체 20종의 스킬(ScriptableObject) 모음")]
        public List<SkillData> allSkills = new List<SkillData>();
    }
}
