using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 시간에 따라 맵 외곽에서 무작위로 적을 쏟아내는 웨이브 생성기
/// GameManager에 종속되어 시작(Init) 시그널을 받습니다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("적들이 튀어나올 화면 밖(Off-screen) 반지름 거리")]
    public float spawnRadius = 15f;
    
    [Tooltip("초당 생성되는 적의 마릿수 (후반부로 갈수록 난이도 상승 연동)")]
    public float baseSpawnDelay = 1.2f;

    [Tooltip("스폰될 때 부를 이름표 (PoolManager의 태그와 일치해야 함)")]
    public string enemyTag = "Enemy";

    private bool isSpawning = false;
    private Transform playerTransform;
    private CancellationTokenSource spawnCts;

    /// <summary>
    /// GameManager에서 게임 시작 직후 호출하는 스위치 온
    /// </summary>
    public void Init()
    {
        // 🚨 최적화: 오브젝트 파인드 대신 컴포넌트 간 변수 참조 활용
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
            isSpawning = true;
            
            spawnCts?.Cancel();
            spawnCts?.Dispose();
            spawnCts = new CancellationTokenSource();
            
            SpawnRoutineAsync(spawnCts.Token).Forget();
            Debug.Log($"✅ [EnemySpawner]: 정상 가동! (생성 거리: {spawnRadius})");
        }
        else
        {
            Debug.LogError("🔥 [EnemySpawner] 플레이어 Transform을 찾을 수 없어 스폰을 중단합니다! (GameManager 인스펙터 확인)");
        }
    }

    /// <summary>
    /// 게임 종료 또는 포즈 시 스폰 중지 외부 호출용
    /// </summary>
    public void StopSpawning()
    {
        isSpawning = false;
        spawnCts?.Cancel();
    }
    
    private void OnDestroy()
    {
        spawnCts?.Cancel();
        spawnCts?.Dispose();
    }

    /// <summary>
    /// 무한 웨이브 (일정 시간 간격으로 반복)
    /// </summary>
    private async UniTaskVoid SpawnRoutineAsync(CancellationToken token)
    {
        while (isSpawning && !token.IsCancellationRequested)
        {
            // 아직 GameManager.Instance가 구동 중이고 맵에 적 쏟아낼 여유가 있는지 체크
            if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
            {
                SpawnEnemy();
            }

            // 난이도 상승 공식: 시간이 지날수록 delay가 짧아지게 (1주차는 고정 딜레이로 진행)
            // 추후 수정 예정: baseSpawnDelay - (경과시간 / 보정값)
            await UniTask.Delay(System.TimeSpan.FromSeconds(baseSpawnDelay), cancellationToken: token);
        }
    }

    /// <summary>
    /// 실제 스폰 로직 (현재 플레이어 중심 원 둘레 무작위 1지점)
    /// </summary>
    private void SpawnEnemy()
    {
        // 1. 수학 공식을 이용한 플레이어 주변 360도 무작위 좌표 생성
        Vector2 spawnPosition = (Vector2)playerTransform.position + (Random.insideUnitCircle.normalized * spawnRadius);

        // 2. Instantiate 대신 든든한 PoolManager에게 "적 1명 보내줘!" 요청
        GameObject newEnemy = GameManager.Instance.poolManager.Get(enemyTag, spawnPosition, Quaternion.identity);

        if (newEnemy != null)
        {
            // TODO: 생성 연출 (흙먼지, 바닥 깨짐 등 파티클 재생)
            // Debug.Log("[EnemySpawner] 적 군단 스폰 완료.");
        }
    }
}
