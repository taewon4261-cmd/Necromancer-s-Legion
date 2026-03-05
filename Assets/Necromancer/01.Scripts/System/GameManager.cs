// File: Assets/Necromancer/01.Scripts/System/GameManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 게임의 전체 라이프사이클 및 하위 매니저 중앙 통제
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    public PoolManager poolManager;
    public EnemySpawner enemySpawner;
    public UIManager uiManager;
    public float baseReviveChance = 30f;
    public string minionTag = "Minion";

    [Header("Player Tracking & Stats")]
    [Tooltip("Hierarchy의 Player 오브젝트를 드래그해서 연결해주세요.")]
    public Transform playerTransform;
    
    [Tooltip("보석이 플레이어에게 끌려오는 자석 반경")]
    public float magnetRadius = 3f;

    [Header("Level System")]
    public int currentLevel = 1;
    public float currentExp = 0f;
    public float maxExp = 100f; // 레벨업 필요 경험치

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
        // 1. 창고(객체 풀)부터 활성화해야 스폰이 에러나지 않음
        if (poolManager != null) poolManager.Init();
        else Debug.LogError("[GameManager] PoolManager reference is missing.");

        // 2. 적 생성기 활성화
        if (enemySpawner != null) enemySpawner.Init();
        else Debug.LogWarning("[GameManager] EnemySpawner 가 연결되지 않아 적이 스폰되지 않습니다.");
        
        // 3. UI 매니저 활성화 및 초기화
        if (uiManager != null) uiManager.Init();
        else Debug.LogWarning("[GameManager] UIManager 가 연결되지 않아 레벨업 창이 뜨지 않습니다.");
    }

    /// <summary>
    /// 몹이 죽었을 때 해골로 부활시킬지 판정하는 주사위 롤
    /// </summary>
    public void TryReviveAsMinion(Vector3 deathPosition)
    {
        float roll = Random.Range(0f, 100f);
        if (roll <= baseReviveChance)
        {
            if (poolManager != null)
            {
                GameObject minion = poolManager.Get(minionTag, deathPosition, Quaternion.identity);
                if (minion != null)
                {
                    // Debug.Log($"[GameManager] 운명적 부활! {deathPosition} 위치에 미니언 소환 완료.");
                }
            }
        }
    }

    /// <summary>
    /// 경험치 획득 및 레벨업 판정
    /// </summary>
    public void AddExp(float amount)
    {
        currentExp += amount;
        Debug.Log($"[GameManager] 경험치 획득: {currentExp} / {maxExp}");

        if (uiManager != null)
        {
            uiManager.UpdateExpBar(currentExp, maxExp);
        }

        if (currentExp >= maxExp)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentExp -= maxExp; // 초과분 유지
        currentLevel++;
        maxExp *= 1.5f; // 다음 레벨업 요구량 1.5배 증가 (임시 기획)
        
        // UI 숫자 갱신 및 팝업창 열기 (시간 정지 발동)
        if (uiManager != null)
        {
            uiManager.UpdateExpBar(currentExp, maxExp); // 초과분 반영 갱신
            uiManager.ShowLevelUpPanel();
        }
        
        Debug.Log($"🎉 [GameManager] 레벨 업! 현재 레벨: {currentLevel}");
    }
}
}
