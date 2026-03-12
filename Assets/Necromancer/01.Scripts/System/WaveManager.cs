// File: Assets/Necromancer/01.Scripts/System/WaveManager.cs
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{
    /// <summary>
    /// 게임 시간에 맞춰 설정된 웨이브 데이터를 순차대로 구동하는 메인 스포너
    /// (기존 EnemySpawner.cs를 완전히 대체합니다)
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Configuration (SO Database)")]
        [Tooltip("인스펙터에서 WaveDatabase SO 하나만 끌어다 놓으면 됩니다!")]
        public WaveDatabase waveDatabase;
        
        [Tooltip("적들이 튀어나올 화면 밖(Off-screen) 반지름 거리")]
        public float spawnRadius = 15f;
        
        private int currentWaveIndex = 0;
        private bool isSpawning = false;
        private float gameTime = 0f;
        
        private Transform playerTransform;
        private CancellationTokenSource spawnCts;

        // 최적화: 현재 화면에 떠있는 적 카운트를 추적하기 위함 (PoolManager 등에 의존할 수도 있으나 간단히 구현)
        private int currentEnemyCount = 0;

        public void Init()
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
                gameTime = 0f;
                currentWaveIndex = 0;
                currentEnemyCount = 0; // 초기화
                
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
            if (!isSpawning) return;

            // 배속 시스템 등에 영향받는 Time.deltaTime 누적
            gameTime += Time.deltaTime;

            CheckWaveProgress();
            
            // UI에 실시간 타임워치 및 현재 진행 중인 웨이브 단계 보고
            if (GameManager.Instance != null && GameManager.Instance.uiManager != null && waveDatabase != null && waveDatabase.waveList.Count > 0)
            {
                GameManager.Instance.uiManager.UpdateHUD(gameTime, waveDatabase.waveList[currentWaveIndex].waveName);
            }
        }

        /// <summary>
        /// 타이머 기반으로 다음 웨이브 데이터로 인덱스를 전환
        /// </summary>
        private void CheckWaveProgress()
        {
            if (waveDatabase == null || waveDatabase.waveList == null || waveDatabase.waveList.Count == 0) return;
            
            // 아직 끝 웨이브가 아니고, 현재 시간이 다음 웨이브의 시작시간을 넘었을 때
            if (currentWaveIndex < waveDatabase.waveList.Count - 1 && gameTime >= waveDatabase.waveList[currentWaveIndex + 1].startTime)
            {
                currentWaveIndex++;
                Debug.Log($"🌊 [WaveManager] 새로운 웨이브 교체: {waveDatabase.waveList[currentWaveIndex].waveName}");
            }
        }

        private async UniTaskVoid SpawnLoopAsync(CancellationToken token)
        {
            while (isSpawning && !token.IsCancellationRequested)
            {
                if (waveDatabase != null && waveDatabase.waveList != null && waveDatabase.waveList.Count > 0 && currentWaveIndex < waveDatabase.waveList.Count)
                {
                    WaveData currentWave = waveDatabase.waveList[currentWaveIndex];

                    // 현재 플레이 중인 GameManager와 PoolManager가 있는 경우 스폰 시도
                    if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
                    {
                        // TODO: 실제 풀에 켜져있는 적 갯수를 정확히 트래킹 (1주차는 러프하게 진행)
                        // currentEnemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length; 
                        
                        // 현재 화면에 적이 너무 많지 않을 때만 스폰
                        // (모바일 배터리 타임 및 렉을 막아주는 안전장치)
                        if (true) // 나중에 카운트 제한 로직 활성화
                        {
                            SpawnEnemy(currentWave);
                        }
                    }

                    // 해당 웨이브의 스폰 딜레이 준수
                    await UniTask.Delay(System.TimeSpan.FromSeconds(currentWave.spawnDelay), cancellationToken: token);
                }
                else
                {
                    // 웨이브 세팅이 비어있으면 1초 대기 후 재검사
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token);
                }
            }
        }

        /// <summary>
        /// 이번 웨이브에 할당된 몬스터 풀 중 하나를 골라 무작위 스폰
        /// </summary>
        private void SpawnEnemy(WaveData waveData)
        {
            if (waveData.enemyPoolList == null || waveData.enemyPoolList.Count == 0) return;

            // 랜덤으로 이번 웨이브의 적 하나를 고르기 (비율 조절은 리스트에 많이 넣는 것으로 퉁침 - 하드코딩 탈피)
            EnemyData selectedEnemyData = waveData.enemyPoolList[Random.Range(0, waveData.enemyPoolList.Count)];
            
            if (selectedEnemyData == null || string.IsNullOrEmpty(selectedEnemyData.poolTag)) return;

            // 플레이어 주변 무작위 원 영역 좌표
            Vector2 spawnPos = (Vector2)playerTransform.position + (Random.insideUnitCircle.normalized * spawnRadius);

            // PoolManager에서 가져오기
            GameObject enemyObj = GameManager.Instance.poolManager.Get(selectedEnemyData.poolTag, spawnPos, Quaternion.identity);

            if (enemyObj != null)
            {
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    // 꺼낸 적에게 데이터 주입 및 난이도 배율 적용 (CombatManager 연동)
                    ai.Setup(selectedEnemyData);
                }
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
