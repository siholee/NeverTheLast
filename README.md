# NeverTheLast — 네버 더 라스트

신화 속 인물로 파티와 코드 빌드를 구성하는 서브컬처 로그라이트 자동전투 RPG입니다.
편성·배치·보상·골드·회복 운영을 선택하고 턴제 자동 전투로 결과를 확인합니다.

## 개발 기준

**main**은 현행 Rework입니다. 이전 main은 [legacy](https://github.com/siholee/NeverTheLast/tree/legacy)에 보관하며 새 개발에 사용하지 않습니다.
훈련·장비/중량·서포트 카드 계승은 현행 시스템에 포함하지 않습니다.

- [현행 기획서](Assets/Docs/Design/README.md): 게임 방향·규칙·현재 콘텐츠.
- [서사 설정](Assets/Docs/Narrative/README.md) · [미구현 제안](Assets/Docs/Proposals/Unimplemented_Characters.md).
- [남은 결정과 검증](Assets/Docs/Design/Design_Backlog.md): 장기 밸런스·전체 QA·미술 승인 상태.
- [문서 보관소](Docs/Archive/README.md): 이전 사양·초안·작업 기록.

## 플레이

해금 캐릭터로 파티를 구성하고 준비 단계에서 배치·활성 코드·상점·휴식을 정합니다.
전투 사이 체력·생사·자원이 이어지며 테마별 라운드와 사건·엘리트·보스를 진행합니다.
보상 코드는 개인 활성 칸과 보관함을 사용하고 준비 중 Drag & Drop으로 교환합니다.
세부 수치와 현재 캐릭터·테마 목록은 기획서의 해당 상세 사양을 확인해 주세요.

| 입력 | 동작 |
| --- | --- |
| 마우스 | 선택·배치·코드 Drag & Drop·용어/유닛 툴팁 |
| TAB | 파티·코드·보급 확인 |
| ESC | 메뉴 |
| 화면 배속/정지 버튼 | 1·2·4배속 및 별도 일시정지 |

## 다운로드

배포된 Windows 데모는 [GitHub Releases](https://github.com/siholee/NeverTheLast/releases/latest)에서 받습니다.
현재 배포된 0.5.1과 main의 최신 개발 소스는 구분합니다. 이번 문서/브랜치 정리는 새 릴리스를 만들지 않습니다.
압축을 폴더째 풀고 실행 파일과 Data 폴더를 함께 유지해 주세요. 기존 macOS 배포는 legacy 버전입니다.
[패치노트](PATCH_NOTES.md)는 배포 이력입니다.

## 개발·검증

엔진은 Unity 6000.4.9f1이며 정확한 버전은 ProjectSettings/ProjectVersion.txt를 따릅니다.
시작 씬은 MainMenu → Game, 실제 빌드 씬은 EditorBuildSettings를 확인합니다.
작업 지침은 [AGENTS.md](AGENTS.md), Claude 안내는 [CLAUDE.md](CLAUDE.md)입니다.

빌드는 Editor 메뉴 `Tools/NeverTheLast/Build Windows Demo` 또는 다음 진입점을 사용합니다.

```text
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
```

해당 Unity 버전과 Build Support가 필요합니다. 출력은 Builds에 생성되며 저장소에 올리지 않습니다.
검증은 실제 Unity Play Mode·VerifyHarnessRunner·필요한 Player 실행으로 수행하고 사용자 저장을 격리합니다.
게임 전체 최종 QA/상용 미술 판정은 아직 진행 중이며 개발 브랜치 승격을 검수 합격으로 취급하지 않습니다.

구매 Asset Store 팩과 임시 음성은 .gitignore에 따라 공개 소스에 포함되지 않습니다.
개발 환경에는 소유한 원본 팩/음성을 기존 GUID와 리소스 경로로 유지해야 합니다.
