using System;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// [SRP] 플레이어의 레벨과 경험치를 전담 관리합니다.
    /// GameManager 하위 오브젝트에 부착하며, AddExp()와 Reset()으로만 상태를 변경합니다.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static event Action<float, float> OnExpChanged;
        public static event Action<List<SkillData>> OnLevelUp;

        public int currentLevel { get; private set; } = 1;
        public float currentExp { get; private set; } = 0f;
        public float maxExp { get; private set; } = 200f;

        public void AddExp(float amount)
        {
            currentExp += amount;
            OnExpChanged?.Invoke(currentExp, maxExp);
            if (currentExp >= maxExp)
            {
                currentExp -= maxExp;
                currentLevel++;
                maxExp = 200f + (currentLevel * 50f);
                var gm = GameManager.Instance;
                if (gm != null && gm.skillManager != null)
                {
                    var options = gm.skillManager.GetRandomSkillsForLevelUp(3);
                    if (options != null && options.Count > 0)
                    {
                        OnLevelUp?.Invoke(options);
                        gm.SetPause(PauseSource.LevelUp, true);
                    }
                }
            }
        }

        /// <summary>세션 시작 혹은 TitleScene 복귀 시 레벨/경험치를 초기화합니다.</summary>
        public void Reset()
        {
            currentLevel = 1;
            currentExp = 0f;
            maxExp = 200f;
        }
    }
}
