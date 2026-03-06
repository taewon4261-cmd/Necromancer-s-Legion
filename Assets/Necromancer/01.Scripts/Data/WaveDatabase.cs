// File: Assets/Necromancer/01.Scripts/Data/WaveDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
    [CreateAssetMenu(fileName = "WaveDatabase", menuName = "Necromancer/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        [Tooltip("1웨이브부터 순서대로 리스트에 할당합니다.")]
        public List<WaveData> waveList;
    }
}
