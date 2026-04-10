using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public static class AddGameSceneToBuild
{
    [MenuItem("Necromancer/Fix Build Settings")]
    public static void Fix()
    {
        string scenePath = "Assets/Necromancer/00.Scenes/GameScene.unity";
        var scenes = EditorBuildSettings.scenes.ToList();
        
        if (!scenes.Any(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log("GameScene added to Build Settings!");
        }
        else
        {
            UnityEngine.Debug.Log("GameScene already in Build Settings.");
        }
    }
}