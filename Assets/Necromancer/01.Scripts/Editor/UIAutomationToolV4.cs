using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.Editor
{
    public class UIAutomationToolV4 : EditorWindow
    {
        [MenuItem("Tools/Necromancer/Final Mobile Optimization (v4)")]
        public static void FinalOptimization()
        {
            // 1. Canvas Scaler (UI.md 4)
            GameObject uiRoot = GameObject.Find("UI_Root");
            if (uiRoot != null)
            {
                var scaler = uiRoot.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1.0f; // [UI.md 4] Match Height (9:16 최적화)
                }
            }

            // 2. Content Pivot & Padding (UI.md 3-2)
            GameObject contentObj = GameObject.Find("Content");
            if (contentObj != null)
            {
                RectTransform rt = contentObj.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 1f);

                var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.padding = new RectOffset(40, 40, 50, 300); // 하단 패딩 300
                    vlg.spacing = 30;
                }
            }

            // 3. UpgradeItem Prefab Height (UI.md 3-1)
            string prefabPath = "Assets/Necromancer/03.Prefabs/UI/UpgradeItem.prefab";
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (template != null)
            {
                var le = template.GetComponent<LayoutElement>() ?? template.AddComponent<LayoutElement>();
                le.minHeight = 240; // 높이 240 확장
                
                EditorUtility.SetDirty(template);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[UI.md Final] 9:16 Mobile Optimization Complete!");
        }
    }
}
