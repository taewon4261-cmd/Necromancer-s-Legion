using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Necromancer.UI;

namespace Necromancer.Editor
{
    public class UIAutomationToolV3 : EditorWindow
    {
        [MenuItem("Tools/Necromancer/FIX REAL PREFAB: UpgradeItem")]
        public static void FixRealPrefab()
        {
            // 실제 프로젝트에 존재하는 프리팹 경로
            string prefabPath = "Assets/Necromancer/03.Prefabs/UI/UpgradeItem.prefab";
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (template == null)
            {
                Debug.LogError($"[Fix] {prefabPath} 경로에서 프리팹을 찾을 수 없습니다.");
                return;
            }

            // 1. Root 설정 (Horizontal Layout Group)
            var hlg = template.GetComponent<HorizontalLayoutGroup>() ?? template.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(30, 30, 20, 20);
            hlg.spacing = 40;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var leMain = template.GetComponent<LayoutElement>() ?? template.AddComponent<LayoutElement>();
            leMain.minHeight = 240;
            leMain.preferredWidth = 1550;

            // 2. 물리적 구조 재배치
            
            // Area 1: Icon_Frame
            Transform iconFrame = template.transform.Find("Icon_Frame") ?? template.transform.Find("Icon");
            if (iconFrame != null)
            {
                var le = iconFrame.GetComponent<LayoutElement>() ?? iconFrame.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 180;
                le.minHeight = 180;
            }

            // Area 2: Info_Vertical_Group
            GameObject infoGroup = null;
            var infoTrans = template.transform.Find("Info_Vertical_Group");
            if (infoTrans == null)
            {
                infoGroup = new GameObject("Info_Vertical_Group", typeof(RectTransform), typeof(VerticalLayoutGroup));
                infoGroup.transform.SetParent(template.transform, false);
                infoGroup.transform.SetSiblingIndex(1);
            }
            else infoGroup = infoTrans.gameObject;

            var vlg = infoGroup.GetComponent<VerticalLayoutGroup>() ?? infoGroup.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var leInfo = infoGroup.GetComponent<LayoutElement>() ?? infoGroup.AddComponent<LayoutElement>();
            leInfo.flexibleWidth = 1;

            // 텍스트 이동
            MoveToInfoGroup(template, "Text_Name", infoGroup.transform);
            MoveToInfoGroup(template, "Text_Description", infoGroup.transform);
            MoveToInfoGroup(template, "Text_Level", infoGroup.transform);

            // Area 3: Button_Upgrade
            Transform btn = template.transform.Find("Button_Upgrade") ?? template.transform.Find("Button");
            if (btn != null)
            {
                var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 220;
                le.minHeight = 140;
                btn.SetAsLastSibling();
            }

            // 3. UpgradeUI 참조 갱신
            UpgradeUI upgradeUI = GameObject.FindObjectOfType<UpgradeUI>();
            if (upgradeUI != null)
            {
                upgradeUI.slotPrefab = template;
                EditorUtility.SetDirty(upgradeUI);
            }

            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[UI Fix] REAL Prefab 'UpgradeItem' has been successfully rebuilt!");
        }

        private static void MoveToInfoGroup(GameObject root, string name, Transform targetParent)
        {
            var t = root.transform.Find(name);
            if (t != null) t.SetParent(targetParent, false);
        }
    }
}
