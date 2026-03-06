// File: Assets/Necromancer/01.Scripts/Data/EnemyData.cs
using UnityEngine;

namespace Necromancer
{
    [CreateAssetMenu(fileName = "New EnemyData", menuName = "Necromancer/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Enemy Identification")]
        public string enemyName = "농부";
        
        [Tooltip("오브젝트 풀에서 꺼내올 때 사용할 태그/이름 (예: Enemy_Peasant)")]
        public string poolTag = "Enemy_Peasant";
        
        [Tooltip("적 프리팹 (필요시 시각적 참고용)")]
        public GameObject enemyPrefab;

        [Header("Base Stats")]
        public float maxHp = 10f;
        public float moveSpeed = 2f;
        
        [Tooltip("플레이어 타격 시 입히는 피해량")]
        public float attackDamage = 10f;
        
        [Tooltip("적 처치 시 드랍하는 경험치 보석의 가치량")]
        public float dropExpAmount = 5f;

        [Header("AI & Logic")]
        [Tooltip("원거리 몬스터인지 여부 (정규군 검사/농부=false, 궁수=true)")]
        public bool isRanged = false;
        
        [Tooltip("보스 또는 엘리트 몹 등급 여부 (거인사냥꾼 스킬 대응용)")]
        public bool isElite = false;
    }
}
