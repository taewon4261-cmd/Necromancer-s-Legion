# Data Sync Guide

- **ScriptableObject(SO) 넘버링 기반 동기화**: 모든 데이터 테이블(업그레이드, 적, 웨이브 등)은 SO 고유 번호를 기반으로 동기화 함.
- **LobbyUpgradeSO 구조**: 로비 업그레이드 13종의 동기화 및 업그레이드 로직 처리 방식 정의.
- **Save Integrity**: 로드 시 데이터 검증 및 기본값 초기화 방어 코드 포함.
