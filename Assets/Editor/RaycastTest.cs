using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class RaycastTest
{
    [MenuItem("Tools/Test Tutorial Button Raycast")]
    public static void TestRaycast()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/00.Necromancer/03.Prefabs/UI/Panel_Tutorial.prefab");
        if (prefab == null) { Debug.LogError("Prefab not found"); return; }
        
        var canvasObj = new GameObject("TestCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        var eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();
        
        var instance = PrefabUtility.InstantiatePrefab(prefab, canvasObj.transform) as GameObject;
        instance.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        
        var btnClose = instance.transform.Find("Btn_Close").GetComponent<RectTransform>();
        Vector3 worldCenter = btnClose.position; // in ScreenSpaceOverlay, position is screen pixel coordinate usually
        
        // Wait, RectTransformUtility.WorldToScreenPoint might be needed.
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, btnClose.position);
        
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPoint };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        
        Debug.Log($"Raycast at {screenPoint} hit {results.Count} objects.");
        foreach (var res in results)
        {
            Debug.Log($"- Hit: {res.gameObject.name} (Parent: {res.gameObject.transform.parent?.name})");
        }
        
        Object.DestroyImmediate(canvasObj);
        Object.DestroyImmediate(eventSystemObj);
    }
}
