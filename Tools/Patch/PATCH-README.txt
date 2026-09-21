NeverTheLast 증분 패치
======================

이 패치는 전체 게임을 다시 받지 않고 기존 설치본의 변경 파일만 교체합니다.
게임을 완전히 종료한 뒤 사용하세요.

Windows
1. 패치 ZIP 안의 폴더를 기존 NeverTheLast 게임 폴더 안에 풉니다.
2. 패치 폴더의 Apply-Patch.cmd를 실행합니다.
3. 완료 메시지를 확인한 뒤 게임을 실행합니다.

macOS
1. 패치 ZIP 안의 폴더를 기존 NeverTheLast 게임 폴더 안에 풉니다.
2. 패치 폴더의 Apply-Patch.command를 실행합니다.
3. macOS가 실행 권한을 요구하면 터미널에서 chmod +x Apply-Patch.command를 한 번 실행합니다.
   Apple Silicon 전용 패치에는 ARM64용 xdelta3가 포함되어 있으므로 Intel Mac에는 Universal 패치를 사용하세요.

안전장치
- 적용 전에 기존 파일과 패치 파일의 SHA-256을 모두 검사합니다.
- 버전이나 파일이 맞지 않으면 아무 파일도 바꾸지 않고 중단합니다.
- 교체 전 파일은 게임 폴더의 .ntl-patch-backup-* 폴더에 보관합니다.
