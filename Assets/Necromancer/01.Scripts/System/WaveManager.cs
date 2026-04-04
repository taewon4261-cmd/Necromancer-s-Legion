using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;

namespace Necromancer
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Configuration (SO Database)")]
        public WaveDatabase waveDatabase;
        public float spawnRadius = 15f;

        private int currentWaveIndex = 0;
        private bool isSpawning = false;
        public float gameTime = 0f;
        private Transform playerTransform;
        private CancellationTokenSource spawnCts;

        public List<EnemyAI> activeEnemies = new List<EnemyAI>();

        public void Init()
        {
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            Debug.Log($"<color=orange>[WaveManager]</color> Init triggered. Scene: {SceneManager.GetActiveScene().name}");

            if (GameManager.Instance == null) return;

            // [STABILITY] 데이터 로드와 플레이어 참조 분리
            // 1. 스테이지 데이터 먼저 확보
            if (GameManager.Instance.currentStage != null)
            {
                waveDatabase = GameManager.Instance.currentStage.waveDatabase;
                Debug.Log($"<color=green>[WaveManager]</color> Stage data loaded: {GameManager.Instance.currentStage.stageName}");
            }
            
            if (waveDatabase == null)
            {
                Debug.LogError("<color=red>[WaveManager]</color> WaveDatabase is missing! Please assign it in StageData or Inspector.");
                return;
            }

            // 2. 플레이어 참조 확보
            playerTransform = GameManager.Instance.playerTransform;
            if (playerTransform == null)
            {
                Debug.LogWarning("[WaveManager] Player Transform is currently NULL. Searching...");
                var player = GameObject.FindWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                gameTime = 0f;
                currentWaveIndex = 0;
                activeEnemies.Clear();
                isSpawning = true;

                spawnCts?.Cancel();
                spawnCts?.Dispose();
                spawnCts = new CancellationTokenSource();

                SpawnLoopAsync(spawnCts.Token).Forget();
                Debug.Log($"<color=green>[WaveManager]</color> Spawn Loop Started.");
            }
            else
            {
                Debug.LogError("<color=red>[WaveManager]</color> Initialization failed: Player Transform NOT FOUND in Scene!");
            }
        }

        private void Update()
        {
            // [수정] 스폰이 끝났더라도(isSpawning = false), 
            // 마지막 적을 처치해서 스테이지 클리어 조건을 체크해야 하므로 Update는 계속 돌아야 합니다.
            
            // 1. 게임 타이머 업데이트 (스폰 중일 때만 시간이 흐르게 하거나, 클리어 전까지 계속 흐르게 할지 선택)
            // 여기선 클리어 전까지 계속 흐르게 하여 '플레이 타임'에 기록되도록 합니다.
            gameTime += Time.deltaTime;

            // 2. 웨이브 진행 및 클리어 조건 상시 체크
            CheckWaveProgress();

            // 3. UI 브로드캐스트
            if (waveDatabase != null && waveDatabase.waveList != null && waveDatabase.waveList.Count > 0)
            {
                int safeIndex = Mathf.Clamp(currentWaveIndex, 0, waveDatabase.waveList.Count - 1);
                GameManager.BroadcastTime(gameTime);
                GameManager.BroadcastWave(safeIndex, waveDatabase.waveList.Count, waveDatabase.waveList[safeIndex].waveName);
            }
        }

        private void CheckWaveProgress()
        {
            if (waveDatabase == null || waveDatabase.waveList == null || waveDatabase.waveList.Count == 0) return;
            
            // [추가] 이미 스테이지가 종료(클리어/실패)되었다면 체크 중단
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
            
            // 1. [웨이브 가속 시스템] 필드의 적이 0마리이면 즉시 다음 웨이브로 시간 점프
            // (마지막 웨이브가 아닐 때만 발동)
            if (currentWaveIndex < waveDatabase.waveList.Count - 1)
            {
                if (activeEnemies.Count == 0 && isSpawning)
                {
                    // 다음 웨이브 시작 시간으로 게임 타임 조정
                    float nextStartTime = waveDatabase.waveList[currentWaveIndex + 1].startTime;
                    if (gameTime < nextStartTime)
                    {
                        gameTime = nextStartTime;
                        Debug.Log($"<color=yellow>[WaveManager]</color> EARLY CLEAR! Jumping to next wave: {waveDatabase.waveList[currentWaveIndex + 1].waveName}");
                    }
                }

                // 일반적인 웨이브 전진 체크
                if (gameTime >= waveDatabase.waveList[currentWaveIndex + 1].startTime)
                {
                    currentWaveIndex++;
                    Debug.Log($"[WaveManager] Wave advanced: {waveDatabase.waveList[currentWaveIndex].waveName}");
                }
            }
            else
            {
                // 2. [최후의 결전] 마지막 웨이브일 때 스폰 중단 체크
                // 스폰 중단 이후에도 '전멸'할 때까지 클리어를 시키지 않습니다.
                WaveData lastWave = waveDatabase.waveList[currentWaveIndex];
                if (isSpawning && gameTime >= lastWave.startTime + lastWave.duration)
                {
                    isSpawning = false;
                    Debug.Log("[WaveManager] FINAL WAVE SPAWN ENDED. Eliminate all remaining enemies!");
                }
            }

            // 3. 스테이지 클리어 조건: 마지막 웨이브이고, 스폰이 끝났으며, 활성 적이 0마리일 때
            if (!isSpawning && currentWaveIndex == waveDatabase.waveList.Count - 1 && activeEnemies.Count == 0)
            {
                GameManager.Instance.OnStageClear();
            }
        }

        private async UniTaskVoid SpawnLoopAsync(CancellationToken token)
        {
            while (isSpawning && !token.IsCancellationRequested)
            {

                if (waveDatabase != null && waveDatabase.waveList != null && waveDatabase.waveList.Count > 0 && currentWaveIndex < waveDatabase.waveList.Count)
                {
                    WaveData currentWave = waveDatabase.waveList[currentWaveIndex];
                    if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
                    {
                        SpawnEnemy(currentWave);
                    }
                    else
                    {
                        Debug.LogWarning("[WaveManager] PoolManager missing or GameManager null.");
                    }
                    await UniTask.Delay(System.TimeSpan.FromSeconds(currentWave.spawnDelay), cancellationToken: token);
                }
                else
                {
                    Debug.LogWarning($"[WaveManager] WaveData invalid. DB: {waveDatabase != null}, Count: {waveDatabase?.waveList?.Count}");
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token);
                }
            }
        }

        private void SpawnEnemy(WaveData waveData)
        {
            if (waveData.enemyPoolList == null || waveData.enemyPoolList.Count == 0) return;
            EnemyData selectedEnemyData = waveData.enemyPoolList[Random.Range(0, waveData.enemyPoolList.Count)];
            if (selectedEnemyData == null || string.IsNullOrEmpty(selectedEnemyData.poolTag)) return;
            if (playerTransform == null) return;

            Vector2 spawnPos = (Vector2)playerTransform.position + (Random.insideUnitCircle.normalized * spawnRadius);
            GameObject enemyObj = GameManager.Instance.poolManager.Get(selectedEnemyData.poolTag, spawnPos, Quaternion.identity);

            if (enemyObj != null)
            {
                Debug.Log($"[WaveManager] Spawned enemy from tag: {selectedEnemyData.poolTag}");
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai != null) ai.Setup(selectedEnemyData);
            }
            else
            {
                Debug.LogError($"[WaveManager] Failed to spawn enemy from tag: {selectedEnemyData.poolTag}. Is it in the Pooled List?");
            }
        }

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
    }
}