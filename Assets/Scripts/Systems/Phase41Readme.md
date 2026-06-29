# Phase 41: 🌙 밤/날씨 효과 활용 시스템

## 개요

기존 DayNightCycle / WeatherManager / StealthSystem 을 게임플레이에 적극 활용하기 위한 확장 시스템입니다.
시간대별 효과, 날씨별 효과, NPC 날씨 반응을 추가하여 게임에 깊이 있는 환경 메커니즘을 제공합니다.

## 파일 구조

```
Assets/Scripts/Systems/
├── DayNightCycle.cs         # (기존) 주야간 조명/환경 제어
├── WeatherManager.cs        # (기존) 날씨 전환 관리
├── TimeManager.cs           # (기존) 게임 시간 관리
├── StealthSystem.cs         # (기존) 은신 시스템
├── TimeOfDayEffects.cs      # [신규] 시간대별 게임플레이 효과
├── WeatherEffects.cs        # [신규] 날씨별 플레이어 효과
├── NPCWeatherBehavior.cs    # [신규] NPC 날씨 반응
└── Phase41Readme.md         # 문서
```

---

## 41.1 — TimeOfDayEffects (시간대별 효과)

### TimeOfDay 열거형

| 시간대   | 시간 범위   | 설명 |
|----------|------------|------|
| Dawn     | 04:00~05:59 | 새벽, 시야 감소 |
| Day      | 06:00~17:59 | 낮, 기본 상태 |
| Evening  | 18:00~19:59 | 저녁, NPC 귀가 |
| Night    | 20:00~03:59 | 밤, 은신 최대 |

### 시간대별 효과

| 효과 | Dawn | Day | Evening | Night |
|------|------|-----|---------|-------|
| 은신 보너스 | +20% | 0% | +10% | +40% |
| 시야 배율 | 0.7× | 1.0× | 0.9× | 0.5× |
| NPC 활동 | 0.5× | 1.0× | 0.7× | 0.3× |
| 몬스터 출현 | 1.3× | 1.0× | 1.0× | 2.0× |

### 이벤트

- `OnTimeOfDayChanged(TimeOfDay)` — 시간대 변경 시 발생

### TimeManager 연동

TimeManager의 `OnTimeChanged` 이벤트를 구독하여 시간대 변경을 감지합니다.
DayProgress 대신 Hour 기반으로 시간대를 정확히 계산합니다.

---

## 41.2 — WeatherEffects (날씨별 효과)

### WeatherType 매핑

WeatherManager의 기존 `WeatherType` 열거형을 그대로 사용합니다:

| WeatherManager | 설명 | 매핑 효과 |
|----------------|------|-----------|
| Clear          | 맑음 | 효과 없음 |
| Rain           | 비   | 이동속도 -10%, 화염 피해 -50%, 확산 2× |
| Snow           | 눈   | 이동속도 -20%, 발자국 활성화, 시야 0.6× |
| Fog            | 안개 | 시야 0.4×, 은신 +30%, 명중률 -20% |
| StrongWind     | 강풍 | 이동속도 -50%, 명중률 -40%, 외출 데미지 |

### 날씨별 상세 효과

| 효과 | Rain | Snow | Fog | StrongWind |
|------|------|------|-----|------------|
| 이동속도 | -10% | -20% | 0% | -50% |
| 은신 보너스 | +10% | +5% | +30% | 0% |
| 시야 배율 | 0.85× | 0.6× | 0.4× | 1.0× |
| 원거리 명중률 | 0% | 0% | -20% | -40% |
| 화염 피해 | -50% | 0% | 0% | 0% |
| 독/안개 확산 | 2× | 1× | 1× | 1× |
| 발자국 | - | 활성화 | - | - |
| 외출 데미지 | - | - | - | 5 HP/s |

### 이벤트

- `OnWeatherEffectApplied(WeatherType)` — 날씨 효과 적용 시 발생

### PlayerMovement 연동

PlayerMovement의 `_speedModifier` 필드와 연동하여 이동속도를 조정합니다.
WeatherEffects.Instance.CurrentSpeedModifier 값을 PlayerMovement에서 읽도록 설계되었습니다.

---

## 41.3 — NPCWeatherBehavior (NPC 날씨 반응)

### NPC 감지 범위 수정

날씨와 시간대에 따라 NPC의 시야/감지 범위가 변경됩니다:

| 조건 | 감지 범위 배율 |
|------|---------------|
| 맑음/낮 | 1.0× (기본) |
| 비 | 0.7× |
| 눈 | 0.6× |
| 안개 | 0.5× |
| 강풍 | 0.3× |
| 밤 (모든 날씨 추가 보정) | 0.7× |

### NPC 은신처 이동

악천후 시 NPC가 가장 가까운 건물로 이동합니다:

- **비**: 은신처 이동 (기본 ON)
- **눈**: 은신처 이동 (기본 ON)
- **강풍**: 은신처 이동 (기본 ON)
- **안개**: 은신처 이동 X (기본 OFF)

ShelterTags 배열을 통해 건물 태그를 설정할 수 있습니다
(기본: "Building", "House", "Shelter", "Shop", "Interior").

### NPCAwarenessSystem 연동

NPCAwarenessSystem의 `ApplyFogEffectToAll()` 정적 메서드를 활용하여
안개 날씨 시 모든 NPC의 시야를 감소시킵니다.

NPCWeatherBehavior.Instance.GetCombinedDetectionMultiplierForNPC() 메서드를 통해
각 NPC 시스템이 현재 종합 감지 범위 배율을 참조할 수 있습니다.

---

## 확장 가이드

### 새로운 시간대 효과 추가

TimeOfDayEffects.cs의 `TimeOfDay` enum에 새 값을 추가하고,
각 Getter 메서드(`GetStealthBonus`, `GetVisionMultiplier` 등)에 case를 추가하세요.
새로운 SerializedField도 추가하면 Inspector에서 바로 튜닝 가능합니다.

### 새로운 날씨 효과 추가

WeatherEffects.cs에 새 SerializedField를 추가하고,
각 Getter 메서드에 case를 추가하세요.
NPCWeatherBehavior.cs에도 감지 범위 배율을 추가해야 합니다.

### PlayerMovement 연동 예시

```csharp
// PlayerMovement.cs에서 WeatherEffects 연동
private void UpdateSpeed()
{
    float weatherMod = 1f;
    if (WeatherEffects.Instance != null)
        weatherMod = 1f + WeatherEffects.Instance.CurrentSpeedModifier;
    
    _currentSpeed = _walkSpeed * _speedModifier * weatherMod;
}
```

### StealthSystem 연동 예시

```csharp
// StealthSystem.cs에서 시간대/날씨 은신 보너스 적용
private float GetTotalStealthBonus()
{
    float bonus = 1f;
    if (TimeOfDayEffects.Instance != null)
        bonus += TimeOfDayEffects.Instance.CurrentStealthBonus;
    if (WeatherEffects.Instance != null)
        bonus += WeatherEffects.Instance.CurrentStealthBonus;
    return bonus;
}
```

---

## 의존성

| 시스템 | 의존 |
|--------|------|
| TimeOfDayEffects | TimeManager |
| WeatherEffects | WeatherManager |
| NPCWeatherBehavior | WeatherManager, WeatherEffects, TimeOfDayEffects, NPCAwarenessSystem |

---

## 버전

- Unity 6000.4.10f1
- URP 17.4.0
- Phase 41
