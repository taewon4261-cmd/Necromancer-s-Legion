// File: Assets/Necromancer/01.Scripts/Data/WaveData.cs
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
    [CreateAssetMenu(fileName = "New WaveData", menuName = "Necromancer/Wave Data")]
    public class WaveData : ScriptableObject
    {
        public string waveName;
        [Tooltip("이 웨이브가 시작되는 게임 경과 시간 (초)")]
        public float startTime;
        [Tooltip("스폰 주기 (초)")]
        public float spawnDelay = 1f;

        [Tooltip("현재 화면에 존재할 수 있는 최대 적 숫자")]
        public int maxEnemiesTotal = 300;
        
        [Tooltip("이 웨이브에 등장할 적 데이터 목록 (스폰 시 무작위 추출)")]
        public List<EnemyData> enemyPoolList;
    }
}
