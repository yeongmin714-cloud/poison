# 계획 v4 — 아레나 실내 품질 계층 이식 (2026-10-10)

사용자 승인("괜찮은 거 같아 다음 계획 진행") — 성 내부 H1~H5(9차)에서 확정된 품질 언어를
아레나 실내 2종에 동일 적용. 목적: 실내 씬 전체의 시각 언어 통일.

## 배경

- H1~H5 품질 계층(벽 톱면 캡/블롭 섀도우/존 틴트/섀도우 OFF/마모 스탬프)은
  `PlayerCastleInteriorBuilder`(성 내부)에만 적용됨.
- 아레나 실내 2종은 별도 빌더: 베팅 로비 `ArenaInteriorBuilder`(60×60×8, IndoorBuilder.CreateRoom
  쿼드 셸) + 투기장 `ArenaPitInteriorBuilder`(56×56×8, 중앙 원형 무대 r=5·관중석 3링×4면).
- IndoorScene.unity 화광(FlameLight) 3개가 Soft 실시간 섀도우(m_Type: 2) — 성내부 H4와
  정반대 설정으로 점무늬 디더링 근원.

## Phase I1 — 벽 톱면 돌 캡

- `IndoorQualityLayers.AddQuadWallCaps` — 쿼드 벽 4면(부재 시 스킵) 상단에 얇은 돌 박스
  (길이×0.5×0.15, y=roomHeight+0.075). 프리미티브 큐브+mainTextureScale 틸링
  (length/2.2, 0.45) — 긴 캡에 돌 격자 반복.
- 로비 입구 대체 벽 2개(ArenaSouthEntry_Left/Right)도 AddBoxCap으로 캡 연속.
- 머티리얼: `CreateWallStoneLower` 재용 + 마감 톤 (0.78,0.76,0.73) — 성 내부 H1과 동일.
- 텍스처 미투입 시 스킵(선택 강화 규약 유지).

## Phase I2 — 접지 블롭 섀도우

- `IndoorQualityLayers.AddBlobAt(parent, pos, rx, rz, y)` — 타원 지원(성 카탈로그는 원형).
  룸 기준 배치(부모 비균등 스케일 영향 회피).
- 로비: 베팅 데스크(7.0×2.6), GLB 모델(2.5×2.5 — NormalizeArenaModel 후).
- 투기장: 관중석 발판 12개(3링×4면, 발판 크기 기반 rx/rz) — "떠 있는 판"→"놓인 발판".

## Phase I3 — 마모 스탬프

- 로비 6장(입구 동선 2+중앙 개방부 2+북측 2, 2.0m, y=0.02).
- 투기장: 무대 위 4장(±3,±3 — 무대 반경 5m 내부 유지, y=0.17=무대 톱 0.15 위)+
  무대 주변 바닥 4장(±12 축상, 2.2m, y=0.02). 색 변화 없음(금지 원칙 준수).

## Phase I4 — IndoorScene 화광 섀도우 None

- FlameLight 3개 m_Shadows.m_Type 2→0 — 성내부 H4(builder lights None)와 정합.
  실내씬 7/8 반복 지적 디더링의 남은 근원 제거. 포그 라이트샤프트는 쿠키/포그 셰이더
  기반이라 영향 없음.

## 검증

- 컴파일 error CS 0 + focused 7/7(ArenaQualityLayersTests 신설) + 전체 EditMode 신규 회귀 0.
- 캡처 승인 게이트 — 사용자 Play 확인 대기.
- 금지 원칙 계승: 천장/공중 구조물·벽 없는 장식선·바닥 색 교체 금지 — 전부 렌더러 전용.
