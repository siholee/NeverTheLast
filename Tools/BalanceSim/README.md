# 독립 전투 밸런스 시뮬레이터

최종 갱신: **2026-10-30**

.NET 9 SDK만 있으면 Unity Editor·Player·Unity 설치 없이 실행한다. `balance.ps1`은 콘솔 도구만 컴파일한다. NuGet에서 받는 패키지는 없으며 저장소의 `Assets/YamlDotNet.dll`을 게임과 함께 사용한다.

## 실행

저장소 루트에서:

```powershell
# 예제 편성 두 개를 각각 100회 비교
.\Tools\BalanceSim\balance.ps1 run --config Tools/BalanceSim/sample.json

# 시드와 표본 수, 결과 폴더 지정. 결과 폴더는 새 경로여야 한다.
.\Tools\BalanceSim\balance.ps1 run --config Tools/BalanceSim/sample.json --runs 1000 --seed 42 --out Logs/BalanceSim/MyComparison

# 현재 아군·적의 ID·8스탯 기초값/성장·코드 연결, 보상 및 층별 적 전투 예산 확인
.\Tools\BalanceSim\balance.ps1 catalog

# 현재 로스터·적·보상 코드와 혼합 편성의 회귀 실행
.\Tools\BalanceSim\balance.ps1 self-test
```

`catalog`의 `units/enemies[].stats`는 각 스탯의 `baseValue/growth`, `rewards`는 실제 패시브 보상 표, `combatBudget`은 공통 `EnemyCombatBudget` 함수를 호출한 층별 피해·속도 보정이다. 추가 피해 예산이 적용된 최종 전투값과 원래 YAML의 선형 성장값을 구분해 확인할 수 있다.

PowerShell 스크립트 실행이 제한된 환경에서는 같은 도구를 다음처럼 실행한다.

```powershell
dotnet run --project Tools/BalanceSim/BalanceSim.csproj -p:UseSharedCompilation=false -- run --config Tools/BalanceSim/sample.json
```

## 게임 데이터를 바꿔 비교

기본 데이터 원본은 항상 **현재 작업 폴더의 `Assets/Resources/Data/`**다. 과거 커밋의 데이터나 도구 안의 복사본을 기본으로 사용하지 않는다.

```powershell
# 원본 YAML을 별도 실험 폴더로 복사한다. 기존 폴더는 덮어쓰지 않는다.
.\Tools\BalanceSim\balance.ps1 copy-data --out Logs/BalanceSim/MyData

# MyData/10_units.yaml 또는 60_enemies.yaml 등의 수치를 편집한 뒤 실행
.\Tools\BalanceSim\balance.ps1 run --config Tools/BalanceSim/sample.json --data Logs/BalanceSim/MyData --out Logs/BalanceSim/ChangedDataResult
```

`10_units.yaml`·`60_enemies.yaml`의 8스탯/성장/마나/코드 연결과 적 `reworkKit`, `20_codes.yaml`의 실제 런타임 메타데이터를 그대로 읽는다. `96_vfx.yaml`은 겹쳐 날아가는 타격의 판정 정점을 계산하는 호스트에도 필요하다. 나머지 YAML도 복사되므로 현재 게임 자료를 한 폴더에서 편집할 수 있다.

**YAML의 설명 문장을 바꾸는 것으로 C# 스킬 계수가 바뀌지는 않는다.** 고유 스킬·보상 코드가 C# 상수로 정한 계수는 해당 게임 소스를 고친 뒤 도구를 다시 실행해야 한다. 시뮬레이터 전용 계수 사본은 없다. 경제·사건·스테이지 진행 데이터는 이 고정 전투 실행에서 사용하지 않는다.

아군/적 YAML의 모르는 최상위·중첩 필드와 코드 슬롯 이름은 오류로 중단한다. 현행 `10_units.yaml`의 `startingItemIds`는 게임 DTO에도 없는 기존 필드여서 양쪽 모두 전투에 사용하지 않는다. JSON 실험 파일의 알 수 없는 속성도 거부한다. 파일은 실행 시작에 읽고 한 실험 동안 고정한다. 수정한 데이터는 다음 프로세스 실행부터 반영한다.

## 편성·실험 입력

`sample.json`을 복사해서 편집한다. 구조를 최소화하면 다음과 같다.

```json
{
  "runs": 1000,
  "seed": 42,
  "maxActions": 3000,
  "maxCycles": 100,
  "teams": [
    {
      "name": "잔·세이",
      "gold": 1000,
      "members": [
        { "id": 7, "level": 10, "column": 1, "row": 2, "rewards": [501] },
        { "id": 1, "level": 10, "column": 2, "row": 2 }
      ]
    }
  ],
  "encounters": [
    {
      "name": "10단계 늑대",
      "stage": 10,
      "kind": "Normal",
      "enemies": [
        { "id": 1001, "column": 1, "row": 1 },
        { "id": 1001, "column": 1, "row": 2 }
      ]
    }
  ]
}
```

| 항목 | 의미 |
| --- | --- |
| `runs`, `seed` | 각 편성×적 조합을 `seed`부터 연속된 시드로 반복한다. 편성마다 동일한 시드 목록을 사용한다. 서로 다른 편성은 난수 소비 횟수가 달라질 수 있다. |
| `maxActions`, `maxCycles` | 행동 수/누적 AV÷100 상한. 상한 도달은 패배로 숨기지 않고 `draw/limit`로 기록한다. |
| `members` | 아군 1~5명. 같은 캐릭터 중복 편성 불가. 입력 순서도 보존하므로 호루스 후계자 등에 영향을 준다. |
| `column`, `row` | 양 진영 공통: 열 1=전열, 2=후열. 행 1~4. 같은 칸 중복 불가. 열당 최대 4명. |
| `level` | 생략/null이면 아군 1레벨, 적은 `stage`레벨. 적에 명시하면 성장 레벨만 덮어쓴다. |
| `stage`, `kind` | 적 기본 전투 예산·금력·처치 골드의 기준. 종류는 `Normal`, `Elite`, `MidBoss`, `Boss`, `Event`. `Event`는 공통 적 예산 제외. |
| `gold` | 개전 보유 골드. 처치 골드는 같은 전투 안에서 증가해 쿠베라 금력에도 반영된다. |
| `hpRatio`, `resource` | 개전 HP 비율(기본 1), 초월 자원(기본 0, 게임 API의 상한 적용). 상시형/중첩형도 기존 코드가 처리한다. |
| `rewards` | 활성 보상 코드 ID, 최대 7개. 중복·상하위 동시 활성 금지. |
| `rewardInstances` | 활성 축전 코드의 상태. 예: `[{"codeId": 999, "charge": 10}]`. 실제 축전 코드 ID와 상한은 `20_codes.yaml`로 확인한다. 보관 코드는 전투 효과가 없어 입력하지 않는다. |
| `stacks` | 게임의 영구 중첩 키와 값. 예: `[{"key":"실제 코드의 키", "value":10, "fraction":0}]`. 임의 이름으로 새 효과가 생기지는 않는다. |
| `nectar` | 게임 `StatType` 이름과 영구 보정. 예: `[{"stat":"Attack", "amount":10}]`. `MaxHp/Attack/SpecialAttack/Defense/SpecialDefense/Speed/CritRate/CritDamage`. 치명 스탯 보정은 0.1=10%p다. |

모든 편성과 모든 적 조합을 교차 실행한다. 각 전투는 독립적으로 초기화하며 전투 결과를 다음 전투의 HP/마나/중첩으로 넘기지 않는다. 자연 런 승률·상점 선택 정책·보상 획득 확률을 측정하는 캠페인 시뮬레이터는 아니다.

## Unity와 같은 조건으로 대조

Windows에서 해당 버전 Unity Editor가 설치된 환경은 다음 명령으로 저장이 격리된 실제 Play Mode와 콘솔을 차례로 실행할 수 있다. 일반 밸런스 시뮬레이션에는 Unity가 필요하지 않으며 이 명령은 동등성 검증 전용이다.

```powershell
.\Tools\BalanceSim\verify-parity.ps1 -Config Tools/BalanceSim/sample.json -Out Logs/BalanceSim/MyParity
.\Tools\BalanceSim\verify-parity.ps1 -Config Tools/BalanceSim/Parity/command.json -Out Logs/BalanceSim/MyCommandParity
.\Tools\BalanceSim\verify-parity.ps1 -Config Tools/BalanceSim/Parity/complex.json -Out Logs/BalanceSim/MyComplexParity
# 기본 설치 경로가 다르면 -UnityExe 'C:/.../Editor/Unity.exe' 지정
```

현재 프로젝트의 Resources YAML을 양쪽에서 사용한다. Unity는 `VerifyHarnessRunner`의 `simulation_parity` 시나리오에서 실제 유닛·코루틴·연출 판정 콜백을 실행한다. 고정 시간 간격은 0.02초이며, unscaled time 카메라 대기가 가상 시간보다 지나치게 느려지지 않게 배치 프레임의 최소 실시간을 5ms로 둔다. 렌더/오디오의 미술 승인은 수행하지 않는다. 입력 `stage/kind`는 실제 Unity 스테이지 종류와 일치해야 한다.

실험 정의, 소스/데이터 manifest, 개별 전투 결과와 최초 불일치 주변 사건은 지정한 결과 폴더에 보존한다. 원시 사건 로그·요청·진행 파일은 작업 소유 임시 폴더에서 삭제한다. 실행 중 소스/데이터가 바뀌면 대조를 승인하지 않는다. 이미 같은 프로젝트가 에디터에서 실행 중이면 먼저 사용자가 검수 가능한 상태로 종료한 뒤 별도 배치 검사를 실행한다. 이 도구는 사용자의 기존 에디터를 종료하지 않는다.

기존 결과끼리의 대조만 수행할 때:

```powershell
.\Tools\BalanceSim\balance.ps1 compare --unity unity-battles.jsonl --sim console-battles.jsonl --out Logs/BalanceSim/MyComparison
```

승패·종료 이유·행동/턴 수·유닛별 HP/피해/회복/보호막·행동/피해 사건 해시는 정확히 비교한다. 누적 AV만 Mono/.NET JIT의 float 중간 정밀도 차이인 최대 4 ULP를 허용한다. 전투의 정수 결과 차이나 사건 순서 차이를 이 허용치로 무시하지 않는다. 난수 상태를 포함한 원시 추적이 필요한 디버깅에서는 `run --traces <새 임시 폴더>`를 사용하고 진단 후 정리한다.

## 결과 파일

기본 출력은 `Logs/BalanceSim/<UTC 시각>/`이다. 기존 폴더/파일을 덮어쓰지 않는다.

- `summary.csv`, `summary.json`: 승/패/무/오류, 승률과 Wilson 95% 구간, 정상 승패 전투의 평균 행동 수·AV 주기·생존 인원.
- `units.csv`: 유닛·배치별 실제 피해·유효 회복·실제 부여 보호막·생존율·자기 턴 수 평균. 오류 전투는 제외한다.
- `battles.jsonl`: 성공·패배·상한·예외를 모두 포함한 개별 전투 결과. 실제 피해는 기존 `Unit.AnyDamageDealt`, 회복/보호막은 기존 이벤트에서 집계한다.
- `experiment.json`: 실행에 사용한 편성·레벨·배치·보상·시드 조건.
- `manifest.json`: 입력 데이터와 게임/호스트 소스 SHA-256, 경로와 런타임. Git의 미커밋 변경도 해시에 포함한다.

승률의 분모는 승+패+무다. 오류를 패배로 치환하지 않으며, 오류가 하나라도 있으면 종료 코드 2다. 입력/읽기 오류는 1, 정상 실행은 0이다. 무승부나 오류 비중을 함께 보고 비교해야 한다. 생존은 HP>0인 본체 기준이고 소환수는 전멸 판정에 포함하지 않는다. 전투 중 증원된 적도 본체 통계에 포함한다. 개별 결과의 입력 정의와 `eventDigest`는 조건 재현 및 행동/피해 사건 순서 비교에 사용한다.

## 공유 코어와 검증 범위

`Core/NeverTheLast.Combat.csproj`는 게임의 `Unit`, `UnitStats`, 상태 컨트롤러, `Codes/**`, `Combat/**`, 효과·DTO·`ActionScheduler` **원본 소스**를 직접 컴파일하는 독립 라이브러리다. 전투 공식을 도구에 재작성하지 않는다. Unity 프로젝트는 같은 소스를 계속 컴파일한다. `CombatRoundLifecycle`, 처치 골드와 열 중앙 정렬 계산은 게임과 도구가 같은 진입점을 사용한다.

`Ports/`는 엔진의 객체 수명·파일 로드·코루틴·표시 타입을 대체한다. `UnityEngine` 이름의 작은 호환 타입은 콘솔 라이브러리 안에만 있으며 Unity DLL/네이티브 엔진을 참조하지 않는다. 렌더러·SFX·사용자 저장은 실행하지 않는다. 논리 코루틴을 가상 50Hz로 진행하므로 실제 시간의 배속을 올리거나 대기할 필요가 없다. 스케줄러 AV는 원본 계산을 그대로 쓴다. 판정·행동 watchdog 경고는 콘솔에서 오류로 승격한다.

`CombatRandom.BeginSeed`는 호스트에 독립적인 수열을 제공한다. 전투의 치명·대상·저항 등의 난수가 이 경로를 공유한다. 게임에서 시드 세션을 열지 않으면 기존 Unity 난수 사용을 유지한다. 자연 런 운영 난수와 시뮬레이터 시드의 결과를 동일시하지 않는다.

`self-test`는 현재 로스터·적·구현된 보상 코드의 실행, 혼합 편성, 시드 재현, 상한 처리와 전투 간 상태 정리를 검사한다. 이는 각 캐릭터의 모든 경계 조건을 검증했다는 뜻이 아니다. 실제 Unity 동등성은 `Parity/command.json`·`Parity/complex.json` 조건으로 대조하며, 결과와 수정 근거는 `Logs/BalanceSim/ParityVerification20261027.md`에 기록한다. 표시 호스트는 픽셀 대신 YAML 정점·타격 대기·완료 대기를 진행한다. `CombatSweepTiming`과 피격 시작 지연 `CombatPresentationWait`는 양 호스트가 공유한다. 검사를 통과한 고정 조건 밖의 모든 시드·상태 조합을 승인하지 않는다. 새 연출·겹치는 타격·배속 변경은 같은 조건의 사건 대조를 추가한다. Play Mode·실제 입력·저장·미술·오디오·실기기 성능 검증은 기존 검증 도구로 수행한다.

새 스킬·효과는 소스 glob에 자동 포함된다. 새 표시 API를 호출하면 콘솔도 컴파일에 실패하므로 해당 포트를 추가해야 한다. 기존 표시 API 안에서 판정 콜백의 횟수/순서를 바꾸면 컴파일만으로 잡히지 않으므로 포트와 동등성 검사를 함께 갱신한다.
