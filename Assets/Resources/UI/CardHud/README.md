# 카드 HUD 자산

최종 갱신: **2026-10-14**

현재 레이아웃·카드/충전 원/상태 표시는 [UI 사양](../../../Docs/Design/Detail_07_UI_Tech.md)과 CardView·UIShapes·UIIcons·StatusIcons 구현을 따른다.
리소스 파일의 존재만으로 현행 런타임 사용을 단정하지 않는다. 크롬/글리프는 현재 로드 경로를 확인한다.
Texture2D를 Sprite.Create로 읽는 자산이 있으므로 임포트 타입을 임의 변경하지 않는다. 기존 alpha와 .meta GUID를 보존한다.
이전 시안별 기록은 문서 보관소에 있으며 게임 UI의 현재 원본이 아니다.
