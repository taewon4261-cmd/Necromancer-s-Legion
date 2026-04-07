using UnityEditor;
using UnityEngine;

public class ForceAssetRefresh
{
    [MenuItem("Necromancer/Force Asset Refresh")]
    public static void Refresh()
    {
        Debug.Log("[ForceAssetRefresh] Starting ForceUpdate...");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("[ForceAssetRefresh] Refresh Complete.");
    }
}
