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
        private float gameTime = 0f;
        private Transform playerTransform;
        private CancellationTokenSource spawnCts;

        public List<EnemyAI> activeEnemies = new List<EnemyAI>();

        public void Init()
        {
            // [STABILITY] 씬 이름 대신 '플레이어 검색' 또는 씬 이름으로 체크 (더 견고함)
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            Debug.Log($"<color=orange>[WaveManager]</color> Init triggered. Scene: {SceneManager.GetActiveScene().name}");

            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (GameManager.Instance.currentStage != null && GameManager.Instance.currentStage.waveDatabase != null)
                {
                    waveDatabase = GameManager.Instance.currentStage.waveDatabase;
                }
                
                // [자가 치유] 데이터베이스가 여전히 없다면 프로젝트 내 기본 데이터 탐색
                if (waveDatabase == null)
                {
                    waveDatabase = Resources.Load<WaveDatabase>("Data/WaveDatabase_Default");
                    if (waveDatabase == null) waveDatabase = Resources.Load<WaveDatabase>("WaveDatabase");
                }

                gameTime = 0f;
                currentWaveIndex = 0;
                activeEnemies.Clear();
                isSpawning = true;

                spawnCts?.Cancel();
                spawnCts?.Dispose();
                spawnCts = new CancellationTokenSource();

                SpawnLoopAsync(spawnCts.Token).Forget();
                Debug.Log($"<color=green>[WaveManager]</color> Initialized for {SceneManager.GetActiveScene().name}.");
            }
            else
            {
                // 게임 씬인데 플레이어 트랜스폼이 없는 경우에만 경고
                Debug.LogWarning("[WaveManager] Initialization failed: Player Transform is missing in GameScene.");
            }
        }

        private void Update()
        {
            if (!isSpawning) return;

            gameTime += Time.deltaTime;
            CheckWaveProgress();

            if (waveDatabase != null && waveDatabase.waveList.Count > 0)
            {
                GameManager.BroadcastTime(gameTime);
                GameManager.BroadcastWave(currentWaveIndex, waveDatabase.waveList[currentWaveIndex].waveName);
            }
        }

        private void CheckWaveProgress()
        {
            if (waveDatabase == null || waveDatabase.waveList == null || waveDatabase.waveList.Count == 0) return;
            if (currentWaveIndex < waveDatabase.waveList.Count - 1)
            {
                if (gameTime >= waveDatabase.waveList[currentWaveIndex + 1].startTime)
                {
                    currentWaveIndex++;
                    Debug.Log($"[WaveManager] Wave advanced: {waveDatabase.waveList[currentWaveIndex].waveName}");
                }
            }
            else
            {
                if (isSpawning && gameTime >= waveDatabase.waveList[currentWaveIndex].startTime + 30f)
                {
                    isSpawning = false;
                    GameManager.Instance.OnStageClear();
                }
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