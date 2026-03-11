# 🧛 Necromancer's Legion: Next Session Briefing

이 문서는 다음 작업(AI)이 프로젝트 현황을 1분 만에 파악하고 즉시 개발에 착수하기 위한 핸드오버 가이드입니다.

---

## ✅ 현재 완료된 작업 (Today's Progress)

### 1. 타이틀 화면 프리미엄 자동화 시스템
*   **스크립트:** `TitleUIController.cs`
*   **핵심 기능:** 
    *   **모바일 최적화 애니메이션:** 버튼이 밑에서 올라오는 등의 어색한 이동을 제거하고, 제자리에서 **Scale-up(0.95->1.0) & Fade-in** 되는 깔끔한 연출 적용.
    *   **패널 관리 시스템:** 스테이지 선택, 로비 업그레이드, 세팅 패널을 열고 닫는 `ShowPanel`, `BackToMainMenu` 로직 및 버튼 자동 바인딩 완료.
    *   **프리미엄 로고:** AI로 생성한 `NecromancerLegion_Logo.png`를 적용하여 비주얼 퀄리티 확보.

### 2. 데이터 구조 및 자동화 (`01.Scripts/Data/`)
*   **Skill Data 자동화:** `SkillDataGenerator.cs`를 통해 `04.Sprites/SkillIcons/`의 이미지를 SO에 자동 바인딩하는 기능 완료.
*   **스테이지 데이터 (`StageDataSO.cs`):** 난이도 배율(HP, Damage, Gold)을 SO화하여 로그라이크식 난이도 조절 기반 마련.
*   **로비 업그레이드 (`LobbyUpgradeSO.cs`):** 영구 능력치 상승(HP, Atk, Magnet 등) 및 레벨별 비용 계산 로직 구현.

### 3. FSM 및 애니메이션 연동
*   플레이어, 미니언, 몬스터의 **이동/공격/죽음** 상태에 따른 이미지 변환 로직 및 FSM이 모두 연결됨.

---

## 🚀 다음 세션 작업 로드맵 (Immediate Action)

### 1단계: 타이틀 서브 패널 UI 구현 (디자인 및 연결)
*   **작업:** `StageSelectPanel`, `UpgradePanel` 내부의 실제 UI 요소(슬롯, 스탯 텍스트, 업그레이드 버튼)를 만들고 스크립트 연결.
*   **데이터 연동:** 생성된 `StageDataSO`와 `LobbyUpgradeSO`를 실제 UI에 바인딩하여 레벨업 및 스테이지 선택 기능 활성화.

### 2단계: 스테이지 해금 및 미니언 스폰 로직
*   **작업:** 스테이지 클리어 시 다음 스테이지와 새로운 미니언이 해금되는 `UnlockManager` 구현.
*   **기믹:** 적 처치 시 해금된 미니언 풀(Pool)에서 확률적으로 부활하게 하는 로직 고도화.

### 3단계: 캐릭터 1x4 에셋 애니메이터 최종 설정
*   FSM 로직은 준비되었으나, 실제 `Animator Controller` 에셋 내에서의 상태 전환 및 파라미터 체크가 필요한 유닛들이 있는지 확인 및 마무리.

### 4단계: 씬 전환 및 데이터 전달
*   타이틀에서 스테이지 선택 시, 선택한 `StageDataSO`를 인게임 씬(`GameScene`)으로 넘겨 웨이브 매니저가 난이도 배율을 적용하게 하기.

---

## ⚠️ 주의사항
*   `TitleUIController`의 버튼 애니메이션 시 **위치 이동 로직은 금지** (사용자의 명확한 요청사항). 제자리 페이드/크기 조절만 유지할 것.
*   콘솔에 뜨는 `Missing Script` 오류는 유니티 에디터에서 정리 필요.
