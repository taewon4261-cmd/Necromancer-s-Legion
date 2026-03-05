using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임의 전체 라이프사이클 및 하위 매니저 중앙 통제
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    public PoolManager poolManager;
    // public EnemySpawner enemySpawner;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitManagers();
    }

    /// <summary>
    /// 하위 매니저 초기화 시퀀스
    /// </summary>
    private void InitManagers()
    {
        if (poolManager != null)
        {
            poolManager.Init();
        }
        else
        {
            Debug.LogError("[GameManager] PoolManager reference is missing.");
        }
    }

    /// <summary>
    /// 적 사망 시 부활 확률 계산 및 미니언 스폰 처리
    /// </summary>
    /// <param name="deathPosition">적 사망 좌표</param>
    public void TryReviveAsMinion(Vector3 deathPosition)
    {
        // TODO: 확률값은 추후 데이터 테이블이나 플레이어 스탯 기반으로 동적 처리 필요
        float reviveChance = 0.3f;
        
        if (Random.value <= reviveChance)
        {
            GameObject minion = poolManager.Get("Minion", deathPosition, Quaternion.identity);
            if (minion != null)
            {
                // TODO: 스폰 이펙트/사운드 시스템 연동
                // Debug.Log("[GameManager] Minion revived successfully.");
            }
        }
    }
}
