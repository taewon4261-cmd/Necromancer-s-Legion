
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

        private int currentWaveIndex = 0;
        private bool isSpawning = false;
        public float gameTime = 0f;
        private Transform playerTransform;
        private CancellationTokenSource waveCts;

        public List<EnemyAI> activeEnemies = new List<EnemyAI>();

        public void Init()
        {
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            if (GameManager.Instance == null) return;

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
                gameTime = 0f;
                currentWaveIndex = 0;
                activeEnemies.Clear();
                isSpawning = true;

                var token = gameObject.GetCancellationTokenOnDestroy();
                SpawnLoopAsync(token).Forget();
                WaveProcessLoopAsync(token).Forget(); // [NEW] 최적화 주기 루프 시작
                
                Debug.Log($"<color=green>[WaveManager]</color> Optimized Wave Process Loop Started.");
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

        /// <summary>
        /// [OPTIMIZATION] 매 프레임 리스트와 클리어 조건을 체크하는 대신, 0.2초마다 수행합니다.
        /// </summary>
        private async UniTaskVoid WaveProcessLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (GameManager.Instance != null && !GameManager.Instance.IsGameOver)
                {
                    CheckWaveProgress();
                }
                
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f), cancellationToken: token);
            }
        }

        private void CheckWaveProgress()
        {
            if (waveDatabase == null || waveDatabase.waveList == null || waveDatabase.waveList.Count == 0) return;
            
            // 1. [웨이브 가속 시스템] 필드의 적이 0마리이면 즉시 시간 점프
            if (currentWaveIndex < waveDatabase.waveList.Count - 1)
            {
                // activeEnemies.Count 체크는 이제 0.2초마다 실행되어 부하가 적습니다.
                if (activeEnemies.Count == 0 && isSpawning)
                {
                    float nextStartTime = waveDatabase.waveList[currentWaveIndex + 1].startTime;
                    if (gameTime < nextStartTime)
                    {
                        gameTime = nextStartTime; 
                        Debug.Log($"<color=yellow>[WaveManager]</color> EARLY CLEAR! Jumping to next wave.");
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

            // 3. 스테이지 클리어 조건
            if (!isSpawning && currentWaveIndex == waveDatabase.waveList.Count - 1 && activeEnemies.Count == 0)
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
                    WaveData currentWave = waveDatabase.waveList[currentWaveIndex];
                    SpawnEnemy(currentWave);
                    await UniTask.Delay(System.TimeSpan.FromSeconds(currentWave.spawnDelay), cancellationToken: token);
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
            if (selectedEnemyData == null || string.IsNullOrEmpty(selectedEnemyData.poolTag) || playerTransform == null) return;

            Vector2 spawnPos = (Vector2)playerTransform.position + (Random.insideUnitCircle.normalized * spawnRadius);
            GameObject enemyObj = GameManager.Instance.poolManager.Get(selectedEnemyData.poolTag, spawnPos, Quaternion.identity);

            if (enemyObj != null)
            {
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai != null) ai.Setup(selectedEnemyData);
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