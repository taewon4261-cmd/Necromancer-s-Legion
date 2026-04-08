using UnityEngine;

namespace Necromancer.UI
{
    /// <summary>
    /// [DEPRECATED] 이제 ESC 입력은 GameManager에서 통합 관리합니다!
    /// 이 스크립트는 설정창을 수동으로 연결하거나 참조를 유지하기 위한 용도로만 사용됩니다.
    /// 실제 로직은 GameManager.cs -> Update() -> UIManager.ToggleSettings()를 따릅니다.
    /// </summary>
    public class SettingKeyManager : MonoBehaviour
    {
        [Header("연결할 설정창 오브젝트")]
        public GameObject settingPanel;

        // [ARCHITECTURAL PURITY] 모든 ESC 입력 로직은 GameManager로 이관되었습니다.
        // 이 스크립트의 Update()는 더 이상 사용되지 않습니다.
    }
}
