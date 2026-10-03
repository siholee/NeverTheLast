# Card HUD 런타임 자산

- `liquid_glass_capsule_bright.png`: 7차 A안 하단 글라스 원본. 내부 링이 겹쳐 현행 런타임에서는 읽지 않는 삭제 후보이며, 패널은 `UIShapes.RoundedRect`의 단일 스모크 면·림·스펙큘러로 조립한다.
- `energy_orb_glass_bright.png`: 밝은 안 2용 진영 공용 실버블루 크롬·유리 오브 셸. 9-슬라이스하지 않는다.
- `portrait_to_glass_fade.png`: 6차의 질감형 전이 원본. 세로 얼룩이 생겨 현행 런타임에서는 읽지 않는 삭제 후보이며, 원화 하단은 `UIShapes.VerticalGradient`의 무색 알파 전이로 잇는다.
- `liquid_glass_capsule.png`, `energy_orb_glass.png`: 4차 어두운 판용 원본. 현행 런타임에서는 읽지 않는 삭제 후보이다.
- `element_*_glyph.png`: 오브용 흰 단색 원소 글리프.
- `status_glyph_atlas.png`: 6 × 6, 셀당 **128 × 128px**인 흰 단색 상태 글리프 아틀라스. 행 우선 매핑은 `StatusIcons.AtlasCells`와 보조 기호 메서드가 소유한다.

PNG 임포트 타입은 사용하지 않는다. `Resources.Load<Texture2D>` 후 `Sprite.Create`로 읽으며, `.meta`는 Unity Editor가 생성한다.
