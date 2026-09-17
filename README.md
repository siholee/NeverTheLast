# NeverTheLast — 네버 더 라스트

신화 속 영웅으로 파티를 꾸려 문명별 스테이지를 돌파하는 **자동 전투 로그라이트 육성 RPG**입니다.
전투는 턴제로 자동 진행되고, 플레이어의 선택은 전투 **전에** 이뤄집니다 — 편성, 배치, 장비, 훈련.

## 데모 다운로드 (Windows)

**현재 데모 버전: 0.3** — 일본 해안 전선과 아베노 세이메이·스사노오 합류, 적 처치 드랍 보상이 추가되었습니다(천공 전선 0.2 포함). 버전별 변경 사항은 [패치노트](PATCH_NOTES.md)를 보세요.

1. [**Releases 페이지**](https://github.com/siholee/NeverTheLast/releases/latest)로 갑니다.
2. **Assets**에서 `NeverTheLast_Demo_Win64.zip`을 내려받습니다.
3. 압축을 **폴더째** 풉니다. `NeverTheLast.exe` 옆의 `NeverTheLast_Data` 폴더가 함께 있어야 실행됩니다.
4. `NeverTheLast.exe`를 실행합니다.

> Windows 10/11 64비트. 처음 실행하면 SmartScreen이 "Windows의 PC 보호" 창을 띄울 수 있습니다.
> 서명되지 않은 빌드라서 그렇습니다 — **추가 정보 → 실행**을 누르면 됩니다.

저장 데이터는 게임 폴더가 아니라 Windows 레지스트리(`HKEY_CURRENT_USER\Software\DefaultCompany\NeverTheLast`)에 남습니다.
그래서 게임 폴더를 지우거나 새 버전으로 덮어써도 진행과 서포트 카드가 유지됩니다.

## 데모에서 해 볼 수 있는 것

| 단계 | 내용 |
| --- | --- |
| **1. 육성 모드 완주** | 메인 캐릭터 1명 + 서포터 카드 4장으로 1스테이지부터 **100스테이지**까지 올라갑니다. 10스테이지마다 테마(문명)가 바뀌고 10번째 스테이지에 보스가 섭니다. |
| **2. 서포트 카드 획득** | 완주한 메인 캐릭터는 그때의 능력치와 코드를 담은 **서포트 카드**로 남습니다. 첫 완주에는 라부아지에가 해금됩니다. |
| **3. 다시 육성** | 다음 여정에서 그 카드를 서포터로 편성하면 훈련 보너스·우정·코드 힌트를 줍니다. |
| **4. 무한 모드** | **서로 다른 캐릭터 5명**을 완주해 카드 5장을 모으면 메인 메뉴의 **무한 모드**가 열립니다. 카드 5장으로 끝없이 이어지는 스테이지에 도전합니다. |

같은 캐릭터를 다시 완주하면 카드가 새 기록으로 **교체**됩니다. 카드 수를 늘리려면 다른 캐릭터로 완주하세요.

## 조작

| 입력 | 동작 |
| --- | --- |
| 마우스 | 메뉴 선택, 유닛 드래그 배치, 보상·사건 선택 |

전투 중에는 조작하지 않습니다. 준비 페이즈에서 배치·장비·훈련을 정하고 전투를 시작하세요.

## 개발

- **엔진:** Unity 6 (6000.4.9f1)
- **설계 문서:** [`Assets/Docs/Design/`](Assets/Docs/Design/) — 기획의 기준이며 데이터(`Assets/Resources/Data/*.yaml`)와 함께 갱신됩니다.
- **데모 빌드:** Editor 메뉴 `Tools > NeverTheLast > Build Windows Demo`, 또는 명령줄

  ```
  Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
  ```

  결과물은 `Builds/NeverTheLast_Demo_Win64/`에 생기며 저장소에는 올리지 않습니다(`.gitignore`). 배포는 이 폴더를 zip으로 묶어 Releases에 올립니다.
