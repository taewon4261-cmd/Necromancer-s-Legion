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
    public WaveManager waveManager;
    public UIManager uiManager;
    public SkillManager skillManager;
    public FeedbackManager feedbackManager;
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
    
    [Header("Game Speed Settings")]
    [Tooltip("현재 인게임 물리 배속 상태 (1.0, 1.5, 2.0, 3.0 등)")]
    public float currentGameSpeed = 1f;

    [Header("Speed Unlock Status (BM)")]
    [Tooltip("광고 시청이나 IAP 구매 시 true로 변경하여 3.0배속을 활성화합니다.")]
    public bool isThreeTimesSpeedAllowed = false; 
    
    // 광고 시청 후 1시간 뒤에 다시 잠그기 위한 타이머 등에 활용 예정 (DateTime 등)
    // private DateTime unlockExpiration;

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

        // 2. 웨이브 시스템 활성화
        if (waveManager != null) waveManager.Init();
        else Debug.LogWarning("[GameManager] WaveManager 가 연결되지 않아 적이 스폰되지 않습니다.");
        
        // 3. UI 매니저 활성화 및 초기화
        if (uiManager != null) uiManager.Init();
        else Debug.LogWarning("[GameManager] UIManager 가 연결되지 않아 레벨업 창이 뜨지 않습니다.");

        // 4. 스킬 매니저 활성화
        if (skillManager != null) skillManager.Init();
        else Debug.LogWarning("[GameManager] SkillManager 가 연결되지 않아 스킬이 나오지 않습니다.");
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
        
        // [밸런스 수정] 초기(1 -> 2렙)는 농부 20마리 분량(100Exp).
        // 이후에는 폭발적으로 늘지 않고, 레벨당 고정 수치(예: 20~50) + 약간의 배수만 추가되도록 변경
        maxExp = 100f + (currentLevel * 30f); 
        
        // UI 숫자 갱신 및 팝업창 열기 (시간 정지 발동)
        if (uiManager != null && skillManager != null)
        {
            uiManager.UpdateExpBar(currentExp, maxExp); // 초과분 반영 갱신
            uiManager.ShowLevelUpPanel();
            
            // 실제 물리 시간 정지
            Time.timeScale = 0f;
            
            // SkillManager를 통해 랜덤 3개 스킬 뽑기 요청
            uiManager.RefreshSkillCards(skillManager.GetRandomSkillsForLevelUp(3));
        }
        
        Debug.Log($"🎉 [GameManager] 레벨 업! 현재 레벨: {currentLevel}");
    }

    /// <summary>
    /// 현업 스타일 배속 토글 버튼 (1.0 -> 1.5 -> 2.0 -> 1.0 순환)
    /// UI의 배속 아이콘/텍스트 변경 함수를 함께 호출합니다.
    /// </summary>
    public void ToggleGameSpeed()
    {
        // 디버깅을 위해 클릭 시점의 현재 속도 즉시 출력
        Debug.Log($"[GameManager] 배속 버튼 클릭됨! 현재 배속: {currentGameSpeed}x");

        // 1.0 -> 1.5 -> 2.0 -> (3.0 체크) -> 1.0 순환
        if (currentGameSpeed <= 1.1f) currentGameSpeed = 1.5f;
        else if (currentGameSpeed <= 1.6f) currentGameSpeed = 2f;
        else if (currentGameSpeed <= 2.1f) 
        {
            // 3배속 해금 여부 체크
            if (isThreeTimesSpeedAllowed)
            {
                currentGameSpeed = 3f;
            }
            else
            {
                // 해금 안됐을 경우: 바로 1배속으로 돌아가거나 나중에 광고 팝업 띄우기
                Debug.Log("[GameManager] 3.0배속은 잠가져 있습니다. 광고를 보면 1시간 해금됩니다!");
                currentGameSpeed = 1f;
                
                // TODO: UIManager.Instance.ShowSpeedUnlockAdPopup(); 등의 호출
            }
        }
        else currentGameSpeed = 1f;

        // 만약 현재 레벨업 창이 떠 있어서 시간이 멈춘 상태라면 
        // 뒷배경 물리 속도만 먼저 바꿔두고 Time.timeScale 자체는 0을 유지합니다.
        if (uiManager != null && uiManager.levelUpPanel != null && uiManager.levelUpPanel.activeSelf)
        {
            Debug.Log($"[GameManager] 배속 예약됨: {currentGameSpeed}x (현재 창 떠있음)");
        }
        else
        {
            Time.timeScale = currentGameSpeed;
            Debug.Log($"[GameManager] 현재 배속 변경됨: {currentGameSpeed}x");
        }

        // UI 버튼의 텍스트 갱신 (예: "x1.5")
        if (uiManager != null)
        {
            uiManager.UpdateSpeedToggleText(currentGameSpeed);
        }
    }

    /// <summary>
    /// 레벨업 후 창을 닫을 때 기존 배속을 기억해서 돌려놓아 줍니다.
    /// </summary>
    public void ResumeGameSpeed()
    {
        Time.timeScale = currentGameSpeed;
    }
}
}
