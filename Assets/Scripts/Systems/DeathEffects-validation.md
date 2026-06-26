# DeathEffects.cs 구문·참조·네임스페이스·코드 스멜 검증 보고서

**파일**: `Assets/Scripts/Systems/DeathEffects.cs`  
**검증 일자**: 2026-06-27  
**타겟**: Systems/DeathEffects.cs (`DeathEffectController` 클래스, 471줄)

---

## 1. ✅ 구문 (Syntax)

- 모든 중괄호(`{}`) 정상 매칭
- 모든 메서드 시그니처 유효
- `using` 지시문 구문 오류 없음
- C# 6.0+ 기능(`?.Invoke()`, `$""` 문자열 보간) 정상 사용
- **이상 없음**

## 2. ✅ 네임스페이스 (Namespace)

| 선언 | 상태 |
|------|------|
| `namespace ProjectName.Systems` | 프로젝트 내 `ProjectName.Core`, `ProjectName.Core.UI`, `ProjectName.Systems.*` 컨벤션과 일치 |
| `using ProjectName.Core` | `PlayerHealth` 클래스가 `ProjectName.Core`에 존재 → ✅ |
| `using UnityEngine` | 표준 Unity 네임스페이스 → ✅ |
| `using System.Collections.Generic` | 표준 C# → ✅ |

## 3. ✅ 참조 검증 (Reference Validation)

| 참조 대상 | 위치 | 상태 |
|-----------|------|------|
| `PlayerHealth.Instance` | L117, L141 | `PlayerHealth.cs`에 `public static PlayerHealth Instance { get; private set; }` 존재 → ✅ |
| `PlayerHealth.OnPlayerDied` (event) | L78 in PlayerHealth.cs | `public static event System.Action OnPlayerDied` → ✅ |
| `PlayerHealth.OnPlayerRespawned` (event) | L80 in PlayerHealth.cs | `public static event System.Action OnPlayerRespawned` → ✅ |
| `PlayDeathEffects` → event handler | L171 | 시그니처 `void PlayDeathEffects()` = `System.Action` → ✅ |
| `StartFadeIn` → event handler | L189 | 시그니처 `void StartFadeIn()` = `System.Action` → ✅ |
| `Camera.main` | L130, L237 | Unity 표준 → ✅ |
| `GameObject.CreatePrimitive(PrimitiveType.Sphere)` | L214 | Unity 표준 API → ✅ |
| `Rigidbody.linearVelocity` | L227 | Unity 6 (`6000.4.10f1`) 신규 API → ✅ (하위 호환 주의, 아래 참조) |
| `DontDestroyOnLoad` | L31, L108 | Unity 표준 → ✅ |
| `GUI.DrawTexture`, `GUI.Label`, `GUIStyle` | OnGUI 내 | Unity IMGUI 표준 → ✅ |

## 4. ⚠️ 코드 스멜 (Code Smell) 및 발견된 문제

### 4.1 🔴 `DeathEffectController.Instance` 미사용 (데드 코드 / 잠재적 버그)

**심각도: 중간**

`DeathEffectController.Instance` 싱글톤 게터가 정의되어 있지만 (L20-34), **프로젝트 내 어디서도 호출되지 않음**. 검색 결과:

```
DeathEffectController 참조:
  - 본인 파일: 11개 (정의 및 내부 사용)
  - PlayerHealth.cs: 주석에만 언급 (5곳)
  - 다른 코드: 0개
```

**영향**: 누군가 `Instance`를 호출하지 않으면 컴포넌트가 생성되지 않으며, `Awake()`가 실행되지 않아 이벤트 구독이 이루어지지 않음. 이 경우 사망 VFX/UI가 전혀 작동하지 않음.

**해결 방안**: 
- (a) 씬에 수동 배치되었다면 싱글톤 게터를 단순 `{ get => _instance; }`로 축소하거나
- (b) `PlayerHealth.Die()` 내에서 `DeathEffectController.Instance?.PlayDeathEffects()` 식으로 직접 호출 추가
- (c) `[RuntimeInitializeOnLoadMethod]`로 자동 생성 (PlayerHealth 패턴 참고)

### 4.2 🟡 `#if UNITY_EDITOR` 비대칭 구조

**심각도: 낮음**

```csharp
// Awake() L115-125
#if UNITY_EDITOR
    if (PlayerHealth.Instance != null) { 구독 }  // null가드 있음
#else
    구독  // null가드 없음
#endif

// OnDestroy() L140-146
#if UNITY_EDITOR
    if (PlayerHealth.Instance != null)  // null가드 있음
#endif
    { 구독해제 }
```

- **Editor**에서는 `PlayerHealth`가 없어도 안전하게 스킵
- **Build**에서는 무조건 구독/구독해제 시도
- PlayerHealth는 `[RuntimeInitializeOnLoadMethod]`로 자동생성되므로 build에서 null일 확률은 낮지만, 파괴 순서(OnDestroy 타이밍)에 따라 editor에서 누수 가능성 존재
- **권장**: `#if` 없이 양쪽 모두 null-safe하게 통일

### 4.3 🟡 `_isPlaying` 플래그 복원 지점 단일 의존

**심각도: 낮음**

`_isPlaying`은 `Update()` 내 `_isFadingIn` 완료 시에만 `false`로 리셋됨 (L338):

```csharp
if (_isFadingIn) {
    ...
    if (elapsed >= FadeInDuration) {
        _isPlaying = false;  // 여기서만 리셋
    }
}
```

- `StartFadeIn()`이 호출되지 않으면 **영원히 `_isPlaying = true`** → 재사망 시 효과 미출력
- PlayerHealth.Respawn()에서 `OnPlayerRespawned?.Invoke()` 호출 → `StartFadeIn` 구독 → 정상 경로에서는 문제없으나, 외부에서 강제로 여러 번 죽이면 두 번째 사망 효과가 무시됨

### 4.4 🟢 `linearVelocity` — Unity 6 API 확인 완료

**심각도: 없음**

- `rb.linearVelocity` (L227)는 Unity 6 (`6000.0+`)에서 도입
- 프로젝트 `ProjectVersion.txt`: `6000.4.10f1` → ✅ 정상
- **만약 Unity 2022 LTS 이하로 다운그레이드 시** → `rb.velocity`로 변경 필요

### 4.5 🟢 `WaitForSeconds` vs unscaled time (타 프로젝트 코드)

**심각도: 없음** (참고사항)

- `DeathEffects.cs` — 모든 시간을 `Time.unscaledTime` 사용 (의도: SlowMo 영향 배제)
- `PlayerHealth.cs` L226 — `yield return new WaitForSeconds(_respawnDelay)` — **스케일드 타임** 사용
- SlowMo 활성화 시 (`Time.timeScale = 0.3`) 리스폰 코루틴이 3s → 약 10s로 지연됨
- DeathEffects의 의도된 동작과 상충. **PlayerHealth 측 수정 필요** (`WaitForSecondsRealtime`으로 변경 권장)

---

## 5. 최종 점수표

| 항목 | 상태 | 비고 |
|------|------|------|
| 구문 (Syntax) | ✅ 통과 | 오류 없음 |
| 참조 (References) | ✅ 통과 | 모든 참조 정상 확인 |
| 네임스페이스 (Namespace) | ✅ 통과 | 컨벤션 일치 |
| 코드 스멜 (Code Smell) | ⚠️ 발견 | 3건 (1중간, 2낮음) |
| Unity 버전 호환성 | ✅ 통과 | 6000.4.10f1 기준 |

---

## 6. 요약

**DeathEffects.cs**는 전반적으로 잘 작성된 파일입니다. 구문, 참조, 네임스페이스 모두 정상이며 PlayerHealth와의 이벤트 연동도 올바르게 되어 있습니다. 주요 발견사항:

1. **🔴 `DeathEffectController.Instance` 싱글톤 게터가 프로젝트 어디서도 호출되지 않음** — 컴포넌트가 씬에 수동 배치되지 않았다면 아무 효과도 작동하지 않습니다. 가장 시급히 확인해야 할 사항입니다.
2. **🟡 `#if UNITY_EDITOR` 불일치** — Editor/Build 간 null-safe 처리가 비대칭적입니다.
3. **🟡 `_isPlaying`이 `StartFadeIn` 완료에만 의존** — 에지 케이스에서 데드락 가능성.
4. **🟢 Unity 6 API(`linearVelocity`) 정상 확인** — 하위 호환성만 주의.