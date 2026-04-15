
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;

namespace Necromancer
{
    /// <summary>
    /// 웨이브 매니저 - 0.2s 간격의 최적화 루프로 리스트 체크 부하를 최소화합니다.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Configuration (SO Database)")]
        public WaveDatabase waveDatabase;
        public float spawnRadius = 15f;

        [Header("Survival & Performance")]
        public float despawnRadius = 22.0f;
        public int maxEnemyCount = 300; // 기본 최댓값 제한

        private int currentWaveIndex = 0;
        private bool isSpawning = false;
        public float gameTime = 0f;
        private Transform playerTransform;
        private CancellationTokenSource waveCts;

        public int activeEnemyCount { get; private set; } = 0;
        
        public void OnEnemySpawned() => activeEnemyCount++;
        public void OnEnemyDied() => activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);

        public void Init()
        {
            // [STABILITY] 이전 세션의 루프가 있다면 즉시 중단 (Zombie Loop 방지)
            if (waveCts != null)
            {
                waveCts.Cancel();
                waveCts.Dispose();
            }
            waveCts = new CancellationTokenSource();

            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            if (GameManager.Instance == null) return;

            // [DATA INTEGRITY] 상태값 완전 초기화
            gameTime = 0f;
            currentWaveIndex = 0;
            activeEnemyCount = 0;
            isSpawning = true;

            if (GameManager.Instance.currentStage != null)
            {
                waveDatabase = GameManager.Instance.currentStage.waveDatabase;
            }
            
            if (waveDatabase == null) return;

            playerTransform = GameManager.Instance.playerTransform;
            if (playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                // [STABILITY] 새로운 토큰으로 비동기 루프 시작
                var token = waveCts.Token;
                SpawnLoopAsync(token).Forget();
                WaveProcessLoopAsync(token).Forget();
                
                Debug.Log($"<color=green>[WaveManager]</color> Session Initialized. Time & Count Reset.");
            }
        }

        private void Update()
        {
            // 타이머 업데이트와 UI 브로드캐스트는 매 프레임 수행
            gameTime += Time.deltaTime;

            if (waveDatabase != null && waveDatabase.waveList != null && waveDatabase.waveList.Count > 0)
            {
                int safeIndex = Mathf.Clamp(currentWaveIndex, 0, waveDatabase.waveList.Count - 1);
                GameManager.BroadcastTime(gameTime);
                GameManager.BroadcastWave(safeIndex, waveDatabase.waveList.Count, waveDatabase.waveList[safeIndex].waveName);
            }
        }

        private async UniTaskVoid WaveProcessLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (GameManager.Instance != null && !GameManager.Instance.IsGameOver)
                {
                    CheckWaveProgress();
                    // [NEW] 성능 최적화를 위한 거리 기반 재활용 체크
                    RecycleDistantEnemies();
                }
                
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.25f), cancellationToken: token);
            }
        }

        private void RecycleDistantEnemies()
        {
            if (playerTransform == null || GameManager.Instance == null) return;
            if (GameManager.Instance.unitManager == null || GameManager.Instance.poolManager == null) return;

            var units = GameManager.Instance.unitManager.allUnits;
            float sqrDespawnRadius = despawnRadius * despawnRadius;
            Vector3 playerPos = playerTransform.position;

            // [STABILITY] 리스트 순회 중 제거 방지를 위해 역순 처리
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var unit = units[i];
                if (unit == null || unit is not EnemyAI) continue;

                float sqrDist = (unit.transform.position - playerPos).sqrMagnitude;
                if (sqrDist > sqrDespawnRadius)
                {
                    // [BUG-FIX] 단순히 SetActive(false)가 아니라 풀로 명확히 반납해야 함
                    GameManager.Instance.poolManager.Release("Enemy", unit.gameObject);
                }
            }
        }

        private void CheckWaveProgress()
        {
            if (waveDatabase == null || waveDatabase.waveList == null || waveDatabase.waveList.Count == 0) return;
            
            // 1. [웨이브 가속 시스템] 필드의 적이 0마리이면 즉시 시간 점프
            if (currentWaveIndex < waveDatabase.waveList.Count - 1)
            {
                // [BUG-FIX] 게임 시작 직후 또는 웨이브 전환 직후 찰나에 적이 0명인 것을 '클리어'로 오해하는 현상 방지
                // 필드에 적이 한 명도 없고, 스폰 중이며, 현재 웨이브가 시작된 지 최소 5초는 지나야 가속
                if (activeEnemyCount == 0 && isSpawning && gameTime > 5f)
                {
                    float currentWaveStartTime = waveDatabase.waveList[currentWaveIndex].startTime;
                    if (gameTime > currentWaveStartTime + 5f) // 현재 웨이브 시작 후 최소 5초 경과
                    {
                        float nextStartTime = waveDatabase.waveList[currentWaveIndex + 1].startTime;
                        if (gameTime < nextStartTime)
                        {
                            gameTime = nextStartTime; 
                            Debug.Log($"<color=yellow>[WaveManager]</color> EARLY CLEAR! Jumping to next wave (Time: {gameTime}).");
                            return; // [STABILITY] 한 번의 체크에 한 웨이브만 점프
                        }
                    }
                }

                if (gameTime >= waveDatabase.waveList[currentWaveIndex + 1].startTime)
                {
                    currentWaveIndex++;
                }
            }
            else
            {
                // 2. [최후의 결전] 마지막 웨이브일 때 스폰 중단 체크
                WaveData lastWave = waveDatabase.waveList[currentWaveIndex];
                if (isSpawning && gameTime >= lastWave.startTime + lastWave.duration)
                {
                    isSpawning = false;
                }
            }

            // 3. 스테이지 클리어 조건 (O(1) 체크)
            if (!isSpawning && currentWaveIndex == waveDatabase.waveList.Count - 1 && activeEnemyCount == 0)
            {
                GameManager.Instance.OnStageClear();
            }
        }

        private async UniTaskVoid SpawnLoopAsync(CancellationToken token)
        {
            while (isSpawning && !token.IsCancellationRequested)
            {
                if (waveDatabase != null && waveDatabase.waveList != null && currentWaveIndex < waveDatabase.waveList.Count)
                {
                    // [NEW] 최댓값 제한 체크 (쿼터 초과 시 스폰 지연)
                    if (activeEnemyCount < maxEnemyCount)
                    {
                        WaveData currentWave = waveDatabase.waveList[currentWaveIndex];
                        SpawnEnemy(currentWave);
                        await UniTask.Delay(System.TimeSpan.FromSeconds(currentWave.spawnDelay), cancellationToken: token);
                    }
                    else
                    {
                        // 쿼터가 찼다면 짧게 대기 후 재시도
                        await UniTask.Delay(500, cancellationToken: token);
                    }
                }
                else
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token);
                }
            }
        }

        private void SpawnEnemy(WaveData waveData)
        {
            if (waveData.enemyPoolList == null || waveData.enemyPoolList.Count == 0) return;
            EnemyData selectedEnemyData = waveData.enemyPoolList[Random.Range(0, waveData.enemyPoolList.Count)];
            if (selectedEnemyData == null || playerTransform == null) return;

            Vector2 spawnPos = (Vector2)playerTransform.position + (Random.insideUnitCircle.normalized * spawnRadius);
            
            // [AUTOMATION] 개별 태그 대신 통합 "Enemy" 풀 태그 사용
            GameObject enemyObj = GameManager.Instance.poolManager.Get("Enemy", spawnPos, Quaternion.identity);

            if (enemyObj != null)
            {
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.Setup(selectedEnemyData);
                    OnEnemySpawned(); // [NEW] 카운트 증가
                }
            }
        }

        public void StopSpawning()
        {
            isSpawning = false;
            waveCts?.Cancel();
        }

        private void OnDestroy()
        {
            waveCts?.Cancel();
            waveCts?.Dispose();
        }
    }
}