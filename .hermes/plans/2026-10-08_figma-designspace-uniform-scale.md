# Figma 정합 v3 — 등배수 디자인공간(Design Space) 통일 계획

작성: 2026-10-08 / 트리거: Screenshots/*ui4* 3장(요리 제작·제작대·퀘스트 저널) 실측 — "일치하지 않는 부분이 많다, 대칭성 없는 창, 너무 커서 화면에 안 들어가는 창" / 투명도(글래스 0.85)는 현행 유지 확정.

## 0. 실측 진단 (ui4 픽셀 검출 + Figma 스펙 JSON 대조)

| 창 | 실측(스크린샷) | Figma 진실치 | 판정 |
|---|---|---|---|
| 제작대(무기) 1145×750 | 좌패널 395px(34.5%), 중앙 496px(43.3%), 보관함 **오른쪽 화면 밖(131px만 노출)**, 하단 잘림 | 504/576/504 = 26.25/30/26.25%, (144,84,1632,912) | 스케일링 부재(CraftBenchBaseUTK raw px) |
| 퀘스트 저널 1067×776 | 414px(38.8%) 풀높이, **상하 클립** | 480×984@(144,48) = 25% 폭, 상하 4.4% 여백 | 스케일링 부재(QuestJournalUTK raw px, Show에 적용 없음) |
| 요리 1142×737 | 좌 326(28.5%)/중앙 373(32.7%)/우 295(25.8%) **좌우 비대칭**, 전 패널 풀높이 662(h/w 2.03) | 좌우 504 대칭/중앙 576, 전부 912(h/w 1.81) | 창 박스는 스케일되나 **내부 요소 raw px 잔존**(헤더/슬롯/그리드 81.6) + ScaleRect X/Y 독립 스케일로 비-16:9 창에서 세로 신장 |

## 1. 근본원인 (코드 레벨)

1. **CraftBenchBaseUTK**: `CanvasBounds=(144,84,1632,912)` raw 논리픽셀을 생성자/Show에서 그대로 사용 — FigmaCanvasLayout 미사용. 1920 캔버스 픽셀이 화면에 1:1 논리 렌더 → 작은 게임창에서 오버플로.
2. **QuestJournalUTK**: `style.left=144, top=48`, `WinW=480, WinH=984` raw — 스케일 적용 경로 없음, GeometryChanged 훅 없음.
3. **CookingWindowUTK**: Show에서 `FigmaCanvasLayout.Apply(창)`+`ApplyPanelScale(패널 박스)`는 스케일하지만 패널 **내부**(AddPanelHeader/StorageGrid 81.6슬롯/섹션 raw 좌표)는 무스케일 → 박스 축소 + 내용 원본 → 넘침/비대칭.
4. **FigmaCanvasLayout.ScaleRect**: X/Y 독립 배율 — 게임창 종횡비≠16:9이면 창이 세로/가로로 늘어남(대칭성 파괴). PanelSettings 기준 1440×900과 혼재.

## 2. 통일 계약 — "1920×1080 디자인공간 등배수(min) 스케일"

- `k = min(rootW/1920, rootH/1080)` (root = UIRoot resolvedSize). **등배수(isotropic)** — 종횡비 무관하게 Figma 비율 불변, 항상 화면 안에 수렴.
- 창 박스: `pos = figmaOrigin×k`, `size = figmaSize×k` (좌상단 앵커, 여백도 Figma 비율 유지).
- 내부: 디자인공간 컨테이너(designSpace) = **raw Figma px 그대로** `size=figmaSize`, `style.scale = k`, `transformOrigin=(0,0)` → 내부 요소 전부 무수정(기존 raw 좌표가 그대로 정답이 됨).
- 적용 시점: Show + UIRoot GeometryChangedEvent (기존 Cooking 패턴 재사용).
- k는 **모든 창이 공유** → 창 간 상대 크기 일관.

## 3. Phase

### P0 공용 헬퍼 — FigmaCanvasLayout.cs
- `public static float DesignScale(Vector2 rootSize)` → k=min(rx/1920, ry/1080), 비정상 입력 가드(0 반환).
- `public static bool ApplyDesignSpace(VisualElement window, VisualElement designSpace, Rect figmaBounds, VisualElement canvasRoot)` → 창 left/top/width/height = figma×k, designSpace.width/height = figma size raw, designSpace.scale = new Scale(k,k), transformOrigin 0,0. root unusable 시 false.
- 기존 `ScaleRect/Apply/Place`(X/Y 독립)은 미이행 창 호환용으로 유지(삭제 금지).

### P1 CraftBenchBaseUTK (무기 제작대 + 연금 동시 수혜)
- `_content`는 이미 raw CanvasBounds.size(106-107행) 유지. 생성자 raw left/top은 유지하되 Show에서 스케일 적용으로 덮어씀.
- Show에서 `ApplyDesignSpace(this, _content, CanvasBounds, UIRoot)` + UIRoot GeometryChangedEvent 훅(Cooking의 `_canvasLayoutRoot` 패턴 복제) → 창 1632k×912k @(144k,84k).
- 내부(필터/콤보/북/푸터/스토리지 그리드) 전부 raw 그대로 — 수정 없음.

### P2 QuestJournalUTK
- Show에서 `ApplyDesignSpace(this, _content, new Rect(144,48,480,984), UIRoot)` + GeometryChanged 훅.
- 생성자의 `style.left=144/top=48`(271-272행)은 초기값으로 유지(Show에서 덮어씀). WinW/WinH 상수 유지(테스트 참조).

### P3 CookingWindowUTK
- `ApplyFigmaBounds()`를 ApplyDesignSpace 기반으로 교체: `ApplyDesignSpace(this, _content, FigmaBounds, _canvasLayoutRoot)`.
- **ApplyPanelScale/ApplyPanelBounds(스케일 패널 경로) 삭제** — 패널은 CreatePanel의 raw 오프셋(이미 정확)에 두고 designSpace 스케일에 일임.
- OnCanvasRootGeometryChanged는 새 apply 호출로 유지.

### P4 테스트 갱신 + 게이트
- FigmaCanvasLayoutTests: DesignScale(min/isotropic)·ApplyDesignSpace 수학 단정 추가.
- CraftBenchFigmaPhase4Tests / CookingWindowFigmaPhase4Tests / QuestWindowFigma93Tests(저널부): 새 계약(창=figma×k, 내부 raw, scale 전파)에 맞게 갱신 — 기존 raw좌표 단정은 designSpace 내부라 대부분 유효.
- 게이트: `compile_test.sh` error CS 0 → focused EditMode(위 4클래스) all passed → 커밋(파일지정 add, push는 지시 시).

## 4. 리스크
- `style.scale`/`transformOrigin`은 Unity 6000 UI Toolkit 지원(2022.1+ API) — 포인터 피킹은 transform 역보정 자동 적용.
- ScrollView/드래그/ESC 스택/UTKWindowManager 계약 보존 — Show/Hide 오버라이드 체인 변경 금지.
- 미이행 창(69:4, 84:4, 15:4, 70:4, 93:230 창, 23:6 등)은 기존 X/Y 독립 경로로 잔존 — **다음 Phase에서 동일 계약으로 이관**(이번 범위 외, 회귀 분리).
- 투명도 0.85(사용자 확정) 무변경.
