using UnityEngine;
using UnityEditor;
using System.IO;

namespace Necromancer.Editor
{
    /// <summary>
    /// 스테이지 데이터를 한 번에 대량으로 생성해주는 에디터 툴입니다.
    /// </summary>
    public class StageDataGenerator : EditorWindow
    {
        [MenuItem("Necromancer/Tools/Generate 50 Stages (Unique Waves)")]
        public static void GenerateStages()
        {
            string rootPath = "Assets/00.Necromancer/02.Data/Stages";
            
            // 1. 루트 폴더 생성
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
                AssetDatabase.Refresh();
            }

            for (int i = 1; i <= 50; i++)
            {
                // 각 스테이지별 폴더 생성 (데이터 관리 용이성)
                string stageFolderName = $"Stage_{i:D2}";
                string stageFolderPath = Path.Combine(rootPath, stageFolderName);
                if (!Directory.Exists(stageFolderPath))
                {
                    Directory.CreateDirectory(stageFolderPath);
                }

                // 2. 스테이지 SO 생성
                StageDataSO stage = ScriptableObject.CreateInstance<StageDataSO>();
                stage.stageID = i;
                stage.stageName = $"Stage {i}";
                stage.stageDescription = $"제 {i}구역: " + (i <= 10 ? "숲의 입구" : i <= 25 ? "어둠의 동굴" : "공포의 성");
                
                // 3. 스테이지별 고유 WaveDatabase 생성
                WaveDatabase stageWaveDb = ScriptableObject.CreateInstance<WaveDatabase>();
                stageWaveDb.waveList = new System.Collections.Generic.List<WaveData>();
                string waveDbPath = Path.Combine(stageFolderPath, $"WaveDatabase_Stage_{i:D2}.asset");
                AssetDatabase.CreateAsset(stageWaveDb, waveDbPath);

                // 4. 웨이브 동적 생성 (난이도 비례)
                int waveCount = 1 + (i / 10); // 10스테이지마다 웨이브 1개씩 추가 (최대 6개)
                if (waveCount > 6) waveCount = 6;

                for (int w = 1; w <= waveCount; w++)
                {
                    WaveData wave = ScriptableObject.CreateInstance<WaveData>();
                    wave.waveName = $"Wave {w}";
                    wave.startTime = (w - 1) * 60f;
                    wave.duration = 60f;
                    
                    // 스테이지가 높을수록 스폰 속도가 빨라짐
                    wave.spawnDelay = Mathf.Max(0.1f, 1.5f - (i * 0.02f) - (w * 0.1f));
                    
                    // 스테이지가 높을수록 동시 등장 적 수 증가
                    wave.maxEnemiesTotal = 30 + (i * 5) + (w * 10);
                    
                    // 에셋 저장 및 DB 등록
                    string waveAssetPath = Path.Combine(stageFolderPath, $"Wave_{w:D2}.asset");
                    AssetDatabase.CreateAsset(wave, waveAssetPath);
                    stageWaveDb.waveList.Add(wave);
                }

                stage.waveDatabase = stageWaveDb;

                // 5. 난이도 배율 설계 (QA 요청대로 i에 비례)
                float multiplier = 1.0f + (i - 1) * 0.15f; 
                stage.enemyHpMultiplier = multiplier;
                stage.enemyDamageMultiplier = multiplier;
                stage.goldGainMultiplier = 1.0f + (i - 1) * 0.08f; 

                // 6. 스테이지 에셋 최종 저장
                string fullPath = Path.Combine(stageFolderPath, $"StageData_{i:D2}.asset");
                AssetDatabase.CreateAsset(stage, fullPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[StageGenerator]</b> 50개의 스테이지와 고유 웨이브 데이터 생성 완료! (Path: 00.Necromancer/02.Data/Stages)</color>");
        }
    }
}
