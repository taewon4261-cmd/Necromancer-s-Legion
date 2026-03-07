namespace Necromancer.Systems
{
    /// <summary>
    /// 게임 전체에서 공통으로 사용되는 UI 관련 상수 및 키 값을 관리합니다.
    /// 하드코딩을 방지하여 유지보수성을 높입니다.
    /// </summary>
    public static class UIConstants
    {
        // DOTween Animation Durations
        public const float DefaultFadeDuration = 0.5f;
        public const float LongFadeDuration = 1.0f;
        public const float ButtonHoverDuration = 0.2f;
        public const float PanelScaleDuration = 0.3f;

        // Scene Names
        public const string TitleSceneName = "TitleScene";
        public const string GameSceneName = "GameScene";

        // Animator Parameters
        public const string AnimParam_IsMoving = "IsMoving";
        public const string AnimParam_Attack = "Attack";
        public const string AnimParam_Die = "Die";
        public const string AnimParam_Hit = "Hit";

        // PlayerPrefs / Data Keys
        public const string Key_TotalGold = "TotalGold";
        public const string Key_HighestWave = "HighestWave";
    }
}
