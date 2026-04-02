# Editor Tools Guide

- **[InitializeOnLoad] 활용**: 하이라키 자동화 및 명명 규칙 자동 정렬 상시 유지.
- **MenuItem 생성 규칙**: 커스텀 도구는 `Necromancer/` 하위 메뉴에 위치하도록 지정. (예: `Necromancer/Tools/Wave Builder`)
- **Undo 지원**: 에디터 코드에서 수동으로 오브젝트를 조작 시 반드시 `Undo.RecordObject`를 호출하여 실행 취소 가능하도록 설계.
