using UnityEngine;
using UnityEditor;
using Necromancer.UI;

[CustomEditor(typeof(UpgradeUI))]
public class UpgradeUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Auto Bind References (Performance Opt)"))
        {
            UIAutoBinder.BindUpgradeUI((UpgradeUI)target);
        }
    }
}

[CustomEditor(typeof(UpgradeItemUI))]
public class UpgradeItemUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Auto Bind References (Performance Opt)"))
        {
            UIAutoBinder.BindUpgradeItemUI((UpgradeItemUI)target);
        }
    }
}

[CustomEditor(typeof(SettingUI))]
public class SettingUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Auto Bind References (Performance Opt)"))
        {
            UIAutoBinder.BindSettingUI((SettingUI)target);
        }
    }
}
