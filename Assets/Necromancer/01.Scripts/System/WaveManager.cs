using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가

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

        private void Awake()
        {
            if (GameManager.Instance != null) GameManager.Instance.waveManager = this;
        }

        public void Init()
        {
            // [STABILITY] 씬 가드: GameScene이 아닐 경우 초기화 방지
            if (SceneManager.GetActiveScene().name != "GameScene") return;

            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (GameManager.Instance.currentStage != null && GameManager.Instance.currentStage.waveDatabase != null)
                {
                    waveDatabase = GameManager.Instance.currentStage.waveDatabase;
                }

                gameTime = 0f;
                currentWaveIndex = 0;
                activeEnemies.Clear(); 
                isSpawning = true;
                
                spawnCts?.Cancel();
                spawnCts?.Dispose();
                spawnCts = new CancellationTokenSource();
                
                SpawnLoopAsync(spawnCts.Token).Forget();
                Debug.Log($"✅ [WaveManager]: 글로벌 웨이브 시스템 가동 시작!");
            }
            else
            {
                Debug.LogError("🔥 [WaveManager] 플레이어 Transform을 찾을 수 없어 스폰을 중단합니다!");
            }
        }

        private void Update()
        {
            // [CRITICAL] 씬 가드: 타이틀 씬 등에서 불필요한 연산 즉시 중단
            if (SceneManager.GetActiveScene().name != "GameScene") return;
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
                    Debug.Log($"🌊 [WaveManager] 새로운 웨이브 교체: {waveDatabase.waveList[currentWaveIndex].waveName}");
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
                // [CRITICAL] 루프 내 씬 가드 재검사
                if (SceneManager.GetActiveScene().name != "GameScene") break;

                if (waveDatabase != null && waveDatabase.waveList != null && waveDatabase.waveList.Count > 0 && currentWaveIndex < waveDatabase.waveList.Count)
                {
                    WaveData currentWave = waveDatabase.waveList[currentWaveIndex];
                    if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
                    {
                        SpawnEnemy(currentWave);
                    }
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
            if (selectedEnemyData == null || string.IsNullOrEmpty(selectedEnemyData.poolTag)) return;
            if (playerTransform == null) return;

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
            spawnCts?.Cancel();
        }

        private void OnDestroy()
        {
            spawnCts?.Cancel();
            spawnCts?.Dispose();
        }
    }
}
