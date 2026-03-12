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
        [MenuItem("Necromancer/Tools/Generate 50 Stages")]
        public static void GenerateStages()
        {
            string folderPath = "Assets/Necromancer/05.Data/Stages";
            
            // 1. 폴더가 없으면 생성
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            for (int i = 1; i <= 50; i++)
            {
                string assetName = $"Stage_{i:D2}.asset";
                string fullPath = Path.Combine(folderPath, assetName);

                // 이미 존재하면 건너뛰거나 덮어쓰기 (여기선 새로 생성)
                StageDataSO stage = ScriptableObject.CreateInstance<StageDataSO>();
                
                // 데이터 채우기
                stage.stageID = i;
                stage.stageName = $"Stage {i}";
                stage.stageDescription = $"이곳은 제 {i}구역입니다. 점점 더 강력한 적들이 등장합니다.";
                
                // 난이도 배율 설계: 1스테이지(1.0) ~ 50스테이지(약 5.0)
                float multiplier = 1.0f + (i - 1) * 0.1f; // 0.1씩 증가
                stage.enemyHpMultiplier = multiplier;
                stage.enemyDamageMultiplier = multiplier;
                stage.goldGainMultiplier = 1.0f + (i - 1) * 0.05f; // 골드는 조금씩 더 줌

                // 에셋 저장
                AssetDatabase.CreateAsset(stage, fullPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[StageGenerator]</b> 50개의 스테이지 데이터 생성 완료!</color>");
        }
    }
}
