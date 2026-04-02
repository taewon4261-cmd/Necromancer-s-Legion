# Execution Hooks

- **On Task Start**: 새 작업 착수 시 `Assets/Necromancer` 경로 하위의 모든 디렉토리 구조를 먼저 파악하여 전반적인 맵(폴더 구조)을 로드할 것.
- **On Code Generation**: `/code` 실행 요청을 받으면 프로젝트의 안정성 원칙과 `skills/ui_standard.md`의 UI 구조 원칙을 먼저 반영한 클래스 구조를 제안할 것.
- **On Scene Change**: 씬 간 전환이나 특정 씬 로직 수정 시 `agent.md`의 'Sleep Policy(매니저 생명주기 분칙)'가 위반되지 않도록 검증할 것.
- **On Log Inspection**: 콘솔 에러나 경고를 분석할 때 방어적 코딩 여부를 우선 확인하여 잠재적 버그를 개선할 것.
