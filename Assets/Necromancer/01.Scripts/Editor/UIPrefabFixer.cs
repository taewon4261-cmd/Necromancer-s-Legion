using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPrefabFixer : Editor
{
    [MenuItem("Necromancer/Fix UpgradeItem Prefab")]
    public static void FixUpgradeItemPrefab()
    {
        string prefabPath = "Assets/Necromancer/03.Prefabs/UI/UpgradeItem.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.LogError("Prefab not found at: " + prefabPath);
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            // Root
            RectTransform rootRt = instance.GetComponent<RectTransform>();
            if (rootRt == null) rootRt = instance.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(960, 220);

            LayoutElement le = instance.GetComponent<LayoutElement>();
            if (le == null) le = instance.AddComponent<LayoutElement>();
            le.minHeight = 220;
            le.preferredHeight = 220;

            // Icon
            FixTransform(instance.transform.Find("Icon"), new Vector2(-350, 0), new Vector2(160, 160), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // Name
            var nameTr = instance.transform.Find("Text_Name");
            if (nameTr != null)
            {
                FixTransform(nameTr, new Vector2(-200, 40), new Vector2(400, 60), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f));
                var tmp = nameTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 40;
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.color = Color.white;
                }
            }

            // Description
            var descTr = instance.transform.Find("Text_Description");
            if (descTr != null)
            {
                FixTransform(descTr, new Vector2(-200, -40), new Vector2(400, 80), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f));
                var tmp = descTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 25;
                    tmp.alignment = TextAlignmentOptions.TopLeft;
                    tmp.color = Color.white;
                }
            }

            // Level
            var levelTr = instance.transform.Find("Text_Level");
            if (levelTr != null)
            {
                FixTransform(levelTr, new Vector2(400, 50), new Vector2(150, 40), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f));
                var tmp = levelTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.fontSize = 30; tmp.alignment = TextAlignmentOptions.Right; tmp.color = Color.green; }
            }

            // Button & Cost
            var btnTr = instance.transform.Find("Button_Upgrade");
            if (btnTr != null)
            {
                FixTransform(btnTr, new Vector2(350, -40), new Vector2(160, 80), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var btnImg = btnTr.GetComponent<Image>();
                if (btnImg == null) btnImg = btnTr.gameObject.AddComponent<Image>();
                btnImg.color = Color.gray;
            }

            var costTr = instance.transform.Find("Text_Cost");
            if (costTr != null)
            {
                FixTransform(costTr, new Vector2(200, -40), new Vector2(150, 80), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f));
                var tmp = costTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.fontSize = 35; tmp.alignment = TextAlignmentOptions.Right; tmp.color = Color.yellow; }
            }

            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            Debug.Log("Successfully fixed UpgradeItem prefab layout.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error fixing prefab: " + e.Message);
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    private static RectTransform FixTransform(Transform t, Vector2 pos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        if (t == null) return null;
        RectTransform rt = t.GetComponent<RectTransform>();
        if (rt == null) rt = t.gameObject.AddComponent<RectTransform>();
        
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        
        // 추가: Z축 평행 이동 방지 및 스케일 초기화
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
        rt.localScale = Vector3.one;
        
        return rt;
    }
}
