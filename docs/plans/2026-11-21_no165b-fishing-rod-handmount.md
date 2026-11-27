# 165b차 계획 — 낚싯대 손장착 배선 (165차 수용 GLB 기반)

## 배경
- 165차: fishing_rod GLB 수용 완료 (Polyfork propped-fishing-rod-072978 + UserProvided/fishing_rod.glb 복제)
- 68차 P3 인프라 존재: ItemHoldManager(핫바 손들기) + GblItemIconRenderer.ResolveModelKey(관례후보1)
- 목표: 낚시 시작 시 손에 낚싯대 → 종료 시 해제. 거치대 은닉 + 스케일/그립 보정 + 아이콘 정화

## 실측 사실 (구현 근거)
1. **FishingSystem 훅 포인트** (Assets/Scripts/Systems/FishingSystem.cs, 615행):
   - TryStartFishing: HasItem("fishing_rod") 게이트 306~310 → 상태세팅 312~314 → **314 직후 Hold 훅**
   - 종료 3경로가 각각 상태 리셋(공통 수렴 아님): StopFishing(337~343), FinishMinigame(456~463), TryCatch(514~521) — **3곳 모두 Unhold 훅**
2. **GLB 노드 구조** (UserProvided/fishing_rod.glb):
   - root "propped-fishing-rod" → "rod" → { "rod-mesh"(mesh0), "reel-crank"→"reel-crank-mesh", **"stick"(mesh2 = Y자 거치대)** }
   - 거치대 bbox: X 0.907~0.962, Y 0~0.607 — 중간 지점 세로 기둥
   - **줄+찌는 mesh0에 베이크**(정점 실측: X>1.85에서 Y≈0~0.2 클러스터 342정점) — 정점 수술 금지 계약 → 유지, Play 판정
   - 로드 대각선: X 2.227 × Y 1.116 (≈26.6° 사선, 그립(코르크)=저X측 끝)
3. **WeaponEquipManager 그립테이블 선례** (_gripTable): LocalPos/LocalEuler/TargetLen + bounds 그립 정렬(최하단부=그립부 앵커 착지) — ItemHoldManager에 동일 패턴의 per-key config로 미러링
4. **GblItemIconRenderer**: TryMount에서 Instantiate(prefab) 후 renderer bounds 정규화 — **은닉 노드 미적용 시 아이콘에 거치대 포함** → 아이콘 베이크에도 동일 hide 적용 필요
5. **ItemHoldManager 현재 로직**: bounds 최대변 0.22m 캡 + localPosition (0,0.02,0), 회전 없음 — 2.227m 낚싯대가 0.22m로 축소됨(장난감 막대) → per-key 오버라이드 필요

## 구현

### Phase B — ItemHoldManager per-key 구성 (Assets/Scripts/Systems/ItemHoldManager.cs)
신규 타입/테이블 추가 (기존 로직 개조 없음 — 오버라이드 분기만):
```csharp
public struct HoldConfig
{
    public float MaxDim;            // bounds 최대변 목표. 0 = 기본 0.22 규약
    public string[] HideNodeNames;  // Instantiate 후 SetActive(false)할 자식 노드명
    public Vector3 LocalEuler;      // 손 본 기준 초기 회전 (Play 실측 트림 대상)
    public bool GripAlign;          // true = 최장축 그립 끝(bounds min측)을 손 원점에 정렬
}
static readonly Dictionary<string, HoldConfig> _holdConfigs = new()
{
    // [165b차] propped 사선(26.6°) 베이크 보정: Z=27도로 평탄화 후 손 본 위임 — 각도는 Play 실측 트림.
    { "fishing_rod", new HoldConfig { MaxDim = 1.0f, HideNodeNames = new[]{ "stick" }, LocalEuler = new Vector3(0f, 0f, 27f), GripAlign = true } },
};
public static HoldConfig? GetHoldConfig(string modelKey);
public static string[] GetHideNodeNames(string modelKey);   // GblItemIconRenderer용 — 없으면 null
```
Hold() 적용 지점:
- 스케일 블록(84~98행): MaxDim 오버라이드 — `float cap = cfg?.MaxDim > 0 ? cfg.MaxDim : 0.22f`
- Instantiate 직후(59~70행 사이): HideNodeNames 각각 `held.transform.Find(name)?.gameObject.SetActive(false)` (파괴 금지 — EditMode 안전 + 아이콘 경로 재사용)
- localPosition(100행) 앞: LocalEuler 적용(`localRotation = Quaternion.Euler(cfg.LocalEuler)`) + GripAlign: 렌더러 bounds 합산 → 최장축 성분 그립 끝(최소측)이 손 원점에 오도록 localPosition 차감(WeaponEquipManager "GLB bounds 자동 그립 정렬" 선례), 기본 (0,0.02,0)에 더함
- 그립 정렬 후 `Debug.Log("[ItemHoldManager] 그립 정렬 ...")` — Play 트림용 실측 로그 (WeaponEquipManager "[Weapon] 그립 정렬" 선례)

### Phase C — FishingSystem 훅 (Assets/Scripts/Systems/FishingSystem.cs)
- TryStartFishing 314행 직후: `ItemHoldManager.Hold("fishing_rod", _player);` — [165b차] 마커 주석
- private static helper 추가:
```csharp
/// <summary>[165b차] 낚시 종료 — 홀드 중이 낚싯대일 때만 해제(중간 무기 장착 등 타 홀드 보존).</summary>
private static void UnholdRodIfHeld()
{
    if (ItemHoldManager.HeldModelKey == "fishing_rod") ItemHoldManager.Unhold();
}
```
- 호출 3곳: StopFishing(라인 해제 블록 341~343 직후), FinishMinigame(라인 해제 461~463 직후), TryCatch(상태 리셋 514~521 블록 직후)
- 계약: 낚시 로직(코루틴/미니게임/확률) 무손상 — 훅 추가만. 무기 장착 중 시작 시 Hold가 내부에서 Unequip(기존 계약) 그대로 위임

### Phase D — 아이콘 은닉 (Assets/Scripts/UI/GblItemIconRenderer.cs)
- TryMount: `job.node = Instantiate(prefab);`(466행) 직후 — `foreach (var n in ItemHoldManager.GetHideNodeNames(job.modelKey) ?? System.Array.Empty<string>()) job.node.transform.Find(n)?.gameObject.SetActive(false);`
- SyncBakeFromGlbPath(물고기 전용)는 무관 — 수정 금지

### Phase E — 테스트 (신규 파일 1개)
Assets/Scripts/Tests/EditMode/FishingRodHoldContractTests.cs — HotbarHoldKeyTests 패턴 준수:
1. `HoldConfig_FishingRod` — GetHoldConfig("fishing_rod") 존재 + MaxDim≈1.0 + HideNodeNames "stick" 포함 + 기본 키(예: "herb_blue")는 null
2. `FishingSystem_StartHoldsRod` — 소스 어서션: FishingSystem.cs에 `ItemHoldManager.Hold("fishing_rod"` + `[165b차]` 마커 존재
3. `FishingSystem_AllExitsUnhold` — 소스 어서션: StopFishing/FinishMinigame/TryCatch 각 블록에 UnholdRodIfHeld 호출 존재 (helper 정의 1회)
4. `IconRenderer_HidesStandNodes` — 소스 어서션: GblItemIconRenderer.cs에 `GetHideNodeNames` 호출 존재
5. `Hold_NoPlayerInEditMode_GracefulNoOp` 등 기존 계약 회귀는 기존 HotbarHoldKeyTests가 담당 — 중복 금지

### 검증
- EditMode 전체: qa165b-run.sh (qa165-run.sh 패턴 동일 — 결과 XML/로그 TestOutput/qa165b-*)
- 목표: **2623 total / 2613 passed / 10 failed baseline 1:1 유지** (신규 테스트 +5 → total 2628/pass 2618 예상 — failed 수 증가 금지), CS=0
- 리포트: TestOutput/qa165b_handmount_report.md (구현 diff 요약 + 테스트 결과 + Play 판정 대기 항목)

## 금지
- Play 실행 금지, git commit 금지
- FishingSystem 낚시 로직(코루틴/미니게임/확률/사운드) 개조 금지 — 훅 추가만
- 기존 테스트 파일 개조 금지 — 신규 파일 1개만
- GLB 정점/텍스처 수정 금지 (줄+찌 베이크분 유지)
- WeaponEquipManager/그립테이블 수정 금지 — ItemHoldManager per-key로 미러링만

## Play 판정 대기 항목 (승인 게이트)
1. 손 그립 각도/위치 — LocalEuler(0,0,27) 초기값 + 그립정렬 로그 실측 → 상수 트림
2. 스케일 1.0m 체감 — 지면/신체 클리핑 여부
3. 거치대 은닉 + 빌트인 줄+찌 유지의 미감 (74차 캐스트 라인과 병존)
4. 낚시 캐스트 애니 부재(현황) — 별도 Phase 후보
