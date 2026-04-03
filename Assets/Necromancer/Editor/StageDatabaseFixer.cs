using UnityEngine;
using UnityEditor;
using Necromancer;
using System.IO;

namespace Necromancer.Editor
{
    public class StageDatabaseFixer : EditorWindow
    {
        [MenuItem("Necromancer/Tools/Fix All Stage Databases")]
        public static void FixDatabases()
        {
            // 1. 데이터베이스 찾기
            string dbPath = "Assets/Necromancer/02.Data/Generated/Master_WaveDatabase.asset";
            WaveDatabase db = AssetDatabase.LoadAssetAtPath<WaveDatabase>(dbPath);

            if (db == null)
            {
                Debug.LogError($"[Fixer] Master WaveDatabase NOT FOUND at {dbPath}");
                return;
            }

            // 2. 모든 스테이지 파일 찾기
            string stagesFolder = "Assets/Necromancer/02.Data/Stages";
            string[] guids = AssetDatabase.FindAssets("t:StageDataSO", new[] { stagesFolder });

            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StageDataSO stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(path);

                if (stage != null)
                {
                    stage.waveDatabase = db;
                    EditorUtility.SetDirty(stage);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>[Fixer] SUCCESS!</b></color> Assigned Master WaveDatabase to {count} stages.");
        }
    }
}
