# NeverTheLast — 네버 더 라스트

신화 속 영웅으로 파티를 꾸려 문명별 스테이지를 돌파하는 **자동 전투 로그라이트 육성 RPG**입니다.
전투는 턴제로 자동 진행되고, 플레이어의 선택은 전투 **전에** 이뤄집니다 — 편성, 배치, 장비, 훈련.

## 데모 다운로드 (Windows)

**현재 데모 버전: 0.4.8** (Windows) / **0.4.4** (macOS) — 신규 해금 캐릭터 넷(드레이크·엘리자베스·히폴리테·히미코)과 신규 장비 다섯이 들어왔습니다. 해금 조건이 남은 캐릭터는 선택 화면에 미해금으로 뜨고 조건을 알려 줍니다. 셰익스피어가 초기 서포트로 바뀌고, 신속 태그는 연속으로 합쳐졌습니다. 버전별 변경 사항은 [패치노트](PATCH_NOTES.md)를 보세요.

1. [**Releases 페이지**](https://github.com/siholee/NeverTheLast/releases/latest)로 갑니다.
2. **Assets**에서 `NeverTheLast_Demo_Win64.zip`을 내려받습니다.
3. 압축을 **폴더째** 풉니다. `NeverTheLast.exe` 옆의 `NeverTheLast_Data` 폴더가 함께 있어야 실행됩니다.
4. `NeverTheLast.exe`를 실행합니다.

> Windows 10/11 64비트. 처음 실행하면 SmartScreen이 "Windows의 PC 보호" 창을 띄울 수 있습니다.
> 서명되지 않은 빌드라서 그렇습니다 — **추가 정보 → 실행**을 누르면 됩니다.

저장 데이터는 게임 폴더가 아니라 Windows 레지스트리(`HKEY_CURRENT_USER\Software\DefaultCompany\NeverTheLast`)에 남습니다.
그래서 게임 폴더를 지우거나 새 버전으로 덮어써도 진행과 서포트 카드가 유지됩니다.

## 데모 다운로드 (macOS)

1. [**Releases 페이지**](https://github.com/siholee/NeverTheLast/releases/latest)의 **Assets**에서 `NeverTheLast_Demo_Mac.zip`을 내려받습니다.
2. 압축을 풀면 `NeverTheLast.app`이 나옵니다. **응용 프로그램** 폴더 등 원하는 곳으로 옮깁니다.
3. **처음 한 번만** 터미널을 열고 아래를 실행합니다(앱을 옮긴 경로에 맞게 바꾸세요).

   ```bash
   xattr -dr com.apple.quarantine /Applications/NeverTheLast.app
   ```

4. `NeverTheLast.app`을 엽니다.

> 인텔 Mac · 애플 실리콘 모두 지원하는 유니버설 앱, macOS 12 이상. Apple 개발자 서명·공증을 받지 않은 앱이라
> 3번을 건너뛰면 "손상되었기 때문에 열 수 없습니다"라는 경고가 뜹니다.
> 3번 뒤에도 애플 실리콘 Mac에서 열리지 않으면 아래를 한 번 더 실행해 주세요.
>
> ```bash
> codesign --force --deep -s - /Applications/NeverTheLast.app
> ```

저장 데이터는 `~/Library/Preferences/unity.DefaultCompany.NeverTheLast.plist`에 남아 앱을 새 버전으로 바꿔도 유지됩니다. Windows 저장과는 이어지지 않습니다.

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
- **macOS 데모 빌드:** Unity Hub에서 **Mac Build Support (Mono)** 모듈을 설치한 뒤 `Tools > NeverTheLast > Build Mac Demo`, 또는 명령줄

  ```
  Unity.exe -batchmode -quit -projectPath . -buildTarget OSXUniversal -executeMethod DemoBuild.BuildMac -logFile Logs/DemoBuildMac.log
  ```

  결과물은 `Builds/NeverTheLast_Demo_Mac/NeverTheLast.app`입니다. Windows에서 zip으로 묶을 때 **실행 권한(`Contents/MacOS/*` 755)을 보존**해야 Mac에서 열립니다.
