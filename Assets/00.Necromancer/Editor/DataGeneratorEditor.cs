// File: Assets/00.Necromancer/Editor/DataGeneratorEditor.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Necromancer; // 네임스페이스 가져오기

public class DataGeneratorEditor
{
    [MenuItem("Necromancer/Generate Enemy & Wave Data")]
    public static void GenerateData()
    {
        string dataPath = "Assets/00.Necromancer/01.Scripts/Data/Generated";
        
        // 폴더가 없으면 자동 생성
        if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer/01.Scripts/Data"))
            AssetDatabase.CreateFolder("Assets/00.Necromancer/01.Scripts", "Data");
            
        if (!AssetDatabase.IsValidFolder(dataPath))
            AssetDatabase.CreateFolder("Assets/00.Necromancer/01.Scripts/Data", "Generated");

        // 1. 적(Enemy) 데이터 10종 SO 일괄 생성
        List<EnemyData> enemyList = new List<EnemyData>();
        string[] enemyNames = { "농부", "징집병", "훈련병", "정규군 검사", "견습 궁수", "장창병", "하급 성기사", "정예 궁수", "기마병", "성단장" };
        bool[] isRanged = { false, false, false, false, true, false, false, true, false, false };
        bool[] isElite = { false, false, false, false, false, false, true, true, true, true }; // 7, 8, 9, 10단계는 엘리트/보스 판정
        float[] baseHp = { 10, 15, 25, 40, 20, 35, 150, 60, 200, 1000 };
        float[] baseSpd = { 1.5f, 1.8f, 1.6f, 2.0f, 1.2f, 2.2f, 1.5f, 1.3f, 3.5f, 2.0f };
        float[] baseDmg = { 5, 8, 12, 15, 10, 18, 25, 20, 30, 50 };

        for (int i = 0; i < 10; i++)
        {
            EnemyData ed = ScriptableObject.CreateInstance<EnemyData>();
            ed.enemyName = enemyNames[i];
            ed.poolTag = "Enemy"; // 추후 적 프리팹 분화 시 수정 (지금은 1주차 단일 모델)
            ed.maxHp = baseHp[i];
            ed.moveSpeed = baseSpd[i];
            ed.attackDamage = baseDmg[i];
            ed.dropExpAmount = 5f + (i * 2f); // 갈수록 드랍율(영혼석) 증가
            ed.isRanged = isRanged[i];
            ed.isElite = isElite[i];

            string assetPath = $"{dataPath}/Enemy_{i + 1}_{enemyNames[i]}.asset";
            AssetDatabase.CreateAsset(ed, assetPath);
            enemyList.Add(ed);
        }

        // 2. 웨이브(Wave) 데이터 10종 생성 및 방금 만든 Enemy를 리스트에 삽입
        List<WaveData> waveList = new List<WaveData>();
        for (int w = 0; w < 10; w++)
        {
            WaveData wd = ScriptableObject.CreateInstance<WaveData>();
            wd.waveName = $"Wave {w + 1}";
            wd.startTime = w * 90f; // 1.5분(90초)마다 웨이브 교체 -> 총 15분
            wd.spawnDelay = Mathf.Max(0.2f, 1.0f - (w * 0.08f)); // 웨이브 지날수록 적 생성 속도 급격 증가
            wd.maxEnemiesTotal = 100 + (w * 20); // 화면 안 허용 적 개체수

            // 혼합 스폰: 자기 라운드와 그 이전 라운드 적들이 비율적으로 섞여 나옴
            wd.enemyPoolList = new List<EnemyData>();
            for (int e = 0; e <= w; e++)
            {
                wd.enemyPoolList.Add(enemyList[e]);
                // 가장 최근 혹은 자기 라운드의 새 몬스터는 확률을 높이기 위해 2~3개씩 더 넣음 (비율 가중치)
                if (e >= w - 1) 
                {
                    wd.enemyPoolList.Add(enemyList[e]); 
                    wd.enemyPoolList.Add(enemyList[e]); 
                }
            }

            string assetPath = $"{dataPath}/Wave_{w + 1}.asset";
            AssetDatabase.CreateAsset(wd, assetPath);
            waveList.Add(wd);
        }

        // 3. WaveDatabase 마스터 SO 생성 후 방금 만든 10 웨이브 리스트 통째로 꽂기
        WaveDatabase db = ScriptableObject.CreateInstance<WaveDatabase>();
        db.waveList = waveList;
        AssetDatabase.CreateAsset(db, $"{dataPath}/Master_WaveDatabase.asset");

        // 변경사항 디스크에 쓰고 프로젝트 새로고침
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ [DataGenerator] 10종 적, 10웨이브, Database SO 일괄 생성 및 연결 완료!");
    }
}
#endif
