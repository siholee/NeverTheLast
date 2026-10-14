# NeverTheLast 작업 안내

최종 갱신: **2026-10-14**

Unity/C# 신화 기반 로그라이트 자동전투 RPG다. 엔진 버전은 ProjectSettings/ProjectVersion.txt를 확인한다.
현행 개발은 main, 구 main은 읽기용 legacy다.

구현 지침은 [AGENTS.md](../AGENTS.md), 기획은 [Design README](../Assets/Docs/Design/README.md)를 먼저 읽는다.
캐릭터 사양은 Design/Characters, 미구현 제안은 Proposals, 서사는 Narrative에 있다. Docs/Archive는 현행 근거가 아니다.

실행 코드·YAML·씬 직렬화가 현재 동작의 기준이다. 8스탯·AV·보상 활성/보관·소환·효과 명중/저항의 공통 API를 재사용한다.
옛 훈련·장비/중량·SynergyManager·5스탯·없는 상태 YAML을 도입하지 않는다.
매니저가 모두 싱글턴/MonoBehaviour라고 가정하지 않는다. 이벤트 컨텍스트와 StopCode/OnRemove 구독 정리를 확인한다.
데이터·CodeFactory·표시 설명·아트/SFX/VFX·해당 상세 문서를 함께 갱신한다.
실제 Unity Play Mode와 VerifyHarnessRunner로 변경 범위를 검증하며 사용자 저장·기존 변경·에셋 GUID를 보존한다.
