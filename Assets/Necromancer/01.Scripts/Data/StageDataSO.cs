using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// 스테이지별 난이도 배율과 환경 설정을 정의하는 데이터 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_", menuName = "Necromancer/Stage Data")]
    public class StageDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        public int stageID;
        public string stageName;
        [TextArea] public string stageDescription;
        public Sprite stageThumbnail;

        [Header("Difficulty Multipliers")]
        [Tooltip("기본 적 체력에 곱해지는 배율")]
        public float enemyHpMultiplier = 1.0f;
        
        [Tooltip("기본 적 공격력에 곱해지는 배율")]
        public float enemyDamageMultiplier = 1.0f;
        
        [Tooltip("스테이지 클리어 시 획득하는 골드 배율")]
        public float goldGainMultiplier = 1.0f;

        [Header("Wave Settings")]
        [Tooltip("해당 스테이지에서 등장할 몬스터 웨이브 테이블")]
        public WaveDatabase waveDatabase; 
    }
}
