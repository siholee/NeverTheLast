# 자연 런의 의도별 파티 비교

## 시 기준 파티의 후속 측정

현행 기준 편성은 `baseline-party.json`의 시·세이·히미코·찬드라·오르페우스, **시 화력 우선** 정책 하나다. 기본 전열은 세이·오르페우스다. 시작 시드 6101601~6101610으로 각 조건 10회를 측정한다. 히미코 맹화 폭발은 게임 공통 코드의 특공 계수를 사용하고, 자동 스탯 코드 평가는 실제 flatStats/percentStats에서 속도=특공>방어를 판별한다.

목표는 현재 파수거신·군세장 및 이후 군세장 반복을 유지하고 신규 고정보스를 추가하지 않는 조건에서 최고 200층 도달이다. `shi200-party.json`은 현재 수치와 시 공격 성장·파티 체력 성장의 가상 변경을 비교하는 진단 전용 계획이다. `study: shi200`인 QA 런만 실제 `GameManager.unitDataList`의 성장치를 변경하고 종료 때 복원한다. 기초 스탯·EXP·적·출현 확률·가격·코드 성능은 바꾸지 않는다. 진단 성장 배율을 게임 데이터에 채택했다는 뜻은 아니다.

`run-baseline.mjs <Player> <새 임시 폴더> <결과 폴더> [동시 실행 수=8] [관찰 상한=340]`의 마지막 인자에 `200`을 주어 목표 차수를 측정한다. `fight_diagnostics.jsonl`은 입장 스탯·행동 횟수·피해·사망 순서와 전투 종료 전 개시 적 잔여 HP를 기록한다. `last-fight-input.json`은 작업 중 대조에만 사용하는 임시 저장본이므로 필요한 실험 조건을 추출한 뒤 삭제한다.

새 결과 폴더에 `baseline-party.json`을 `plan.json`으로 복사하고, 전용 Player로 `node Tools/BalanceSim/Campaign/run-baseline.mjs <Player> <새 임시 실행 폴더> <결과 폴더> [동시 실행 수=8]`를 실행한다. Player는 `CompositionCampaignBuild.BuildBaselineStudy`와 `NTL_PARTY_STUDY_BUILD`로 빌드한다. 원본 에디터가 열려 있으면 소스·데이터가 같은 별도 복사 프로젝트를 사용하며 사용자 에디터를 종료하지 않는다.

측정에 사용한 데이터의 표시 카탈로그는 `node Tools/BalanceSim/Campaign/export-code-catalog.mjs <20_codes.yaml> <결과 폴더/code-catalog.json>`으로 만든다. 보상은 passive 슬롯의 500~999 중 양수 tier 항목만 포함한다. 일반행동 등 다른 슬롯의 동일 ID를 합치면 이름·티어가 잘못 표시되므로 슬롯을 구분한다.

`analyze-baseline.mjs <실행 상위 폴더> <결과 폴더>`는 해당 plan.json의 각 설정×10회와 시드·피해·코드 제시/수령·활성 슬롯을 확인한다. 코드 이름·티어·상하위 검증에는 결과 폴더의 code-catalog.json을 사용한다. 완료 후 `archive-baseline.mjs`로 결과만 보존하고 같은 분석기로 보존본을 다시 대조한다. 제시·수령·활성/보관은 각각 `reward_offers.jsonl`, `reward_choices.jsonl`, `loadouts.jsonl`이며 반복 상점 노출을 무작위 드랍률로 해석하지 않는다.

`report-baseline.mjs <결과 폴더>`는 2026-10-30 측정 묶음용 보고서 작성기다. 최초 정책 비교 50회에 더해 `followup/analysis/results.json`의 역할 평가 수정 후 기준 10회, 별도 고정 조건 `laboratory/analysis.json`, `parity-comparison/comparison.json`이 모두 필요하다. 최신 자동 선택은 양수 flatStats/percentStats가 있는 스탯 코드에만 히미코 우선순위를 적용하고, 연격·반격처럼 스탯이 없는 코드는 원래 역할 평가를 사용한다. 최초 50회와 수정 후 10회는 정책 버전이 달라 별도로 집계한다. 이후 자연 진행만 재측정할 때는 분석·보존 도구를 사용하고 해당 차수에 맞게 보고서를 작성한다.

아래는 이전 전체 편성 비교의 실행 방법이다.

`intent-parties.json`은 현재 추천·캐릭터 역할과 편성 문서에서 정리한 32개 실험 편성이다. 전후열, 칸, 선택 순서를 고정하고 각 편성에 같은 10개 시작 시드를 사용한다. 42개 현행 캐릭터를 포함하며 가능한 모든 5인 조합을 열거하는 검사는 아니다. 실험용 파일이므로 게임의 추천·수치를 바꾸지 않는다.

도달층 측정은 실제 게임의 `composition_campaign`과 `intent_study` 프로필을 사용하는 전용 QA Player에서 진행한다. 상점·보상·경험치·영구 성장·휴식·사건·테마 이동을 함께 실행한다. 개별 고정 전투를 Unity 없이 반복할 때는 상위 폴더의 .NET CLI를 사용한다.

## 실행

저장소 루트에서 전용 Player를 새 임시 폴더에 빌드한다. 빌드 메서드는 원래 ProjectSettings 바이트를 보존하고 복구한다.

```powershell
$env:NTL_PARTY_STUDY_BUILD = "$PWD/.codex-temp/MyPartyStudy/Player"
& 'C:/Program Files/Unity 6000.4.9f1/Editor/Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Tools.Editor.CompositionCampaignBuild.BuildIntentStudy -logFile "$PWD/.codex-temp/MyPartyStudy/build.log"

.\Tools\BalanceSim\Campaign\run-study.ps1 -Player .codex-temp/MyPartyStudy/Player/CompositionQA.exe -Out Logs/MyPartyStudy -Scratch .codex-temp/MyPartyStudy/Runs -Workers 8 -Runs 10 -StageCap 340
node Tools/BalanceSim/Campaign/analyze-study.mjs --input Logs/MyPartyStudy --out Logs/MyPartyStudy/analysis --runs 10 --cap 340
```

Unity 버전은 `ProjectSettings/ProjectVersion.txt`와 맞춘다. 사용자가 열어 둔 에디터·저장을 종료하거나 덮어쓰지 않는다. 기본 Player 제품명은 이 측정 전용 `NeverTheLast Party Study 20261027`이며, `DebugMode.BeginSession()`으로 사용자 저장과 분리한다. 모든 작업용 Player·임시 파일은 결과를 추출한 뒤 소유 경로를 확인하여 정리한다.

## 조건과 해석

- 반복 시드: `6101600 + 시도 번호`, 1부터 10까지. 같은 시작 시드에서도 파티별 난수 소비가 달라 이후 테마·보상·적이 항상 같지는 않다.
- 실제 신규 런에서 시작한다. 레벨·골드·보상 코드·초월 자원을 임의 주입하지 않는다. `partyLevel` 요청 필드는 이 캠페인에서만 반복 수를 뜻한다.
- 기존 공통 운영 정책이 합법적인 구매·보상 수령·휴식·무료 코드 교환을 결정한다. 특정 파티에 사후 구제 선택을 더하지 않는다. 이 정책이 인간의 최적 플레이를 뜻하지는 않는다.
- 고정 50Hz, 1배속으로 콜백을 실행한다. unscaled 카메라 대기로 가상 시계만 앞서지 않도록 프레임당 최소 실시간 5ms를 둔다. 벽시계 실행 시간은 전투 효율의 지표로 사용하지 않는다.
- `reached`는 전투에 진입한 최고 누적 위치, `cleared`는 승리한 최고 위치다. 누적 위치는 휴식·사건 슬롯을 포함하고 HUD의 테마 번호와 구분한다.
- 상한에서 살아 있는 런은 우측 검열이다. 이 경우 평균/최고 도달층은 실제 전멸 한계의 하한이다. 오류·검증 watchdog은 패배나 0층으로 치환하지 않는다.
- 피해는 게임 `DamageMeter` 기준 실제 HP/보호막 차감량이다. 소환수 피해는 소환자에게 합산한다. 아군 자해는 가한 피해에 포함하지 않는다.
- 런 누적 피해 평균과 전체 전투 기준 평균 피해를 함께 제공한다. AV 주기당 피해는 총 가한 피해를 누적 AV/100 합으로 나눈 값이며, 실제 시간 DPS와 구분한다.
- 받은 피해는 아군 대상 실제 피해 사건의 합계, 회복은 아군의 실제 유효 회복, 보호막은 실제 부여량이다. 아군 소환수 대상 사건도 포함한다. HP 직접 비용과 초과 회복은 별개다. 생존자 수는 heroList의 파티원이며 별도 summonList의 소환수는 포함하지 않는다.

`analysis/validation.json`에서 누락·중복·시드·전투 개수·개별 피해 합계와 파티 피해의 일치를 확인한다. 모든 시도와 실패 요약, 편성·조건·소스/데이터 manifest를 결과와 함께 보존한다.

감독·Player가 모두 종료된 중단 실험은 `analyze-study.mjs`로 임시 루트의 `../live`에 먼저 집계한 뒤 `resume-study.mjs <Runs 경로> <결과 경로> <Player 경로> [동시 실행 수=8]`로 미완료 시드만 신규 런으로 다시 실행할 수 있다. 기존 감독과 동시에 실행하지 않는다. 이 복구 프로필은 위의 10회·340층 실험 조건을 사용한다. `.done` 예약 표시는 결과가 아니며 실제 `runs.jsonl`만 집계한다.

`archive-study.mjs <Runs 경로> <결과 경로>`는 완료된 분석과 실행 요약을 보존하고, `report-study.mjs <결과 경로>`는 편성·피해·지원·보스·실패·재실행 대조 결과를 보고서로 정리한다. `recovery-prefix-validation.json`은 중단 전후 공통 완료 구간의 재현성을 별도로 기록한다. 원시 콘솔·행동 추적·재현용 저장·전용 빌드는 결과를 추출한 후 정리한다.
