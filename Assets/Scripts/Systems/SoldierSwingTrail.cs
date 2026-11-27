using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 26-ST1] 병사 스윙 휘적 리본 — 인스턴스형 트레일(플레이어 WeaponSwingTrail static 파사드와 별개 인스턴스).
    ///
    /// 구조(WeaponSwingTrail.Attach/ApplyWeaponConfig 선례 준용):
    ///   - GuardVisualAttachSystem이 병사 무기 GLB 인스턴스 부착 직후 <see cref="AttachTo"/> 호출 →
    ///     무기 자식 GO "SoldierSwingTrail"에 TrailRenderer 생성(호스트 컴포넌트 동일 GO — TrailPulseRunner 선례).
    ///   - 팁 앵커 = renderer bounds 합산 최장축의 그립 반대편 끝(WeaponEquipManager.ApplyBoundsGripAlignment
    ///     수식 선례 재사용 — 최장축 판정/끝면 중심/축방향 팁 산출 동일), 무기 로컬 변환은
    ///     WeaponSwingTrail.Attach의 InverseTransformPoint 방식 그대로(scale/rotation 보정 포함).
    ///   - HumanoidClipDriver(mode==DriveMode.Soldier)가 콤보 진입/종료에 SetEmittingLocal 토글 —
    ///     발화 전 트레일은 emitting=false 대기.
    ///
    /// 트레일 사양(BOTW 규격 동일 — WeaponSwingTrail 45차 P4 선례):
    ///   - width 3키 곡선 (0,1.0)/(0.6,0.65)/(1,0) — 뿌리 굵고 끝이 빠지는 부채꼴, time 0.22s
    ///   - minVertexDistance 0.05, numCapVertices 4, View alignment, 그림자 off
    ///   - 머티리얼 = WeaponSwingTrail.CreateTrailMaterialShared(1.0f) — [ST2] 중복 구현 금지(공개 팩토리 위임 재사용)
    ///   - 색 계약: 청백 시작색 (0.75, 0.92, 1.0) — 패망 청 계열(병사 파랑 국가색 무손상), endColor 알파 0
    ///   - [ST5] 정적 동시 상한 가드: 활성 리본 MaxActive(16) 초과 시 새 부착 조용히 무시(기존 것 유지)
    /// </summary>
    public class SoldierSwingTrail : MonoBehaviour
    {
        // ── 사양 상수 ──
        /// <summary>잔상 지속(초) — BOTW 규격 동일(WeaponSwingTrail TrailTime 0.22s 선례).</summary>
        private const float TrailTime = 0.22f;
        /// <summary>정점 최소 간격(m) — 무거운 회전에서도 부드러운 곡선(선례 동일).</summary>
        private const float MinVertexDistance = 0.05f;
        /// <summary>폭 배율(팁 기준 최대, m) — 플레이어 검 트레일 TipWidth(0.09) 규격 동일.</summary>
        private const float TipWidth = 0.09f;
        /// <summary>[ST1 색 계약] 청백 시작색 — 패망 청 계열(병사 파랑 국가색 무손상). 알파 1 고정.</summary>
        private static readonly Color StartColorPaleBlue = new Color(0.75f, 0.92f, 1f, 1f);

        /// <summary>[ST5] 활성 병사 리본 동시 상한 — 초과 부착 조용히 무시(23-X2 과도 스폰 방지 선례의 인스턴스판).</summary>
        private const int MaxActive = 16;
        private static int _activeCount;

        /// <summary>트레일 GO 이름 — 재부착 시 기존 인스턴스 탐색/파괴 키(WeaponSwingTrail GO명 선례 준용).</summary>
        private const string TrailGoName = "SoldierSwingTrail";

        private TrailRenderer _trail;
        private bool _emitting;
        /// <summary>[ST5] 활성 카운트 반영 여부 — 동기 반납/OnDestroy 양쪽 경로 중복 차감 방지.</summary>
        private bool _counted;

        // ================================================================
        // 정적 API
        // ================================================================

        /// <summary>
        /// [ST1] 병사 무기 GLB 인스턴스에 트레일 부착 — GuardVisualAttachSystem(무기 부착 직후) 전용 진입.
        ///
        /// gripRatio: 그립 앵커의 최장축 정규화 위치(0=최소축 끝, 1=최대축 끝). 기본 0.0 = 자동 판정 —
        /// WeaponEquipManager 그립 휴리스틱 선례(손=부모 본/피벗에 가까운 끝면이 그립)를 따르고 팁은 그 반대편 끝.
        /// 0~1 내부값 지정 시 그 위치를 그립으로 간주해 반대편 끝을 팁으로 삼는다.
        /// weapon null 가드 — 조용히 무시. 기존 트레일 인스턴스는 파괴 후 재생성(WeaponSwingTrail.Attach 선례).
        /// [ST5] 활성 상한 초과 시 새 부착 조용히 무시(기존 것 유지).
        /// </summary>
        public static void AttachTo(GameObject weapon, float gripRatio = 0.0f)
        {
            if (weapon == null) return;                      // null 가드 — 조용히 무시
            if (_activeCount >= MaxActive) return;           // [ST5] 동시 상한 — 초과 부착 무시(기존 것 유지)

            // 기존 인스턴스 파괴 후 재생성(WeaponSwingTrail.Attach 선례) — 카운트는 동기 반납(OnDestroy 중복 차감 방지)
            Transform existing = weapon.transform.Find(TrailGoName);
            if (existing != null)
            {
                var prev = existing.GetComponent<SoldierSwingTrail>();
                if (prev != null) prev.ReleaseCount();
                Object.Destroy(existing.gameObject);
            }

            // 팁 산출 — renderer bounds 합산(WeaponEquipManager.ApplyBoundsGripAlignment 수식 선례)
            var rends = weapon.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;                   // 렌더러 없음 — 팁 미산출(부착 스킵, 장착 자체는 계속)
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.sqrMagnitude < 1e-10f) return;

            // 최장축 판정(선례 동일: x→y→z 순 비교)
            int axis = 0;
            float len = b.size.x;
            if (b.size.y > len) { axis = 1; len = b.size.y; }
            if (b.size.z > len) { axis = 2; len = b.size.z; }

            // 그립/팁 끝면 중심 — 최장축 양끝 면 중심(FaceCenter 선례)
            Vector3 eMin = FaceCenter(b, axis, b.min);
            Vector3 eMax = FaceCenter(b, axis, b.max);

            // 그립 앵커 — gripRatio 지정 우선, 없으면(기본 0.0) 손(부모 본/피벗)에 가까운 끝면이 그립(선례 휴리스틱)
            float gripT = (gripRatio > 0f && gripRatio < 1f)
                ? Mathf.Clamp01(gripRatio)
                : AutoGripRatio(eMin, eMax, weapon.transform.parent, weapon.transform);
            Vector3 grip = Vector3.Lerp(eMin, eMax, gripT);

            // 팁 월드 좌표 = 그립 반대편 끝 — bounds 중심 + (그립→팁 최장축 단위벡터 × 최장축 절반)
            // (WeaponEquipManager 팁 수식 선례: axisDir=(b.center-grip).normalized, tip=b.center+axisDir*(len*0.5))
            Vector3 axisDir = (b.center - grip).sqrMagnitude > 1e-8f ? (b.center - grip).normalized : weapon.transform.up;
            Vector3 tipWorld = b.center + axisDir * (len * 0.5f);

            // 무기 자식 GO 생성 + 팁 앵커 = InverseTransformPoint(WeaponSwingTrail.Attach 방식 그대로 — scale/rotation 보정 포함)
            var trailGo = new GameObject(TrailGoName);
            trailGo.transform.SetParent(weapon.transform, false);
            trailGo.transform.localPosition = weapon.transform.InverseTransformPoint(tipWorld);
            trailGo.transform.localRotation = Quaternion.identity;

            var trail = trailGo.AddComponent<TrailRenderer>();
            ConfigureTrail(trail);
            trail.emitting = false;                          // 발화는 HumanoidClipDriver(콤보 진입)가 담당

            var host = trailGo.AddComponent<SoldierSwingTrail>();
            host._trail = trail;
            host._counted = true;
            _activeCount++;

            Debug.Log($"[Soldier] 스윙 트레일 부착: weapon={weapon.name}, 팁={tipWorld:F2}, gripT={gripT:F2}, active={_activeCount}/{MaxActive}");
        }

        // ================================================================
        // 인스턴스 API
        // ================================================================

        /// <summary>발화 토글 — HumanoidClipDriver(mode==DriveMode.Soldier) 전용. 미부착이면 조용히 무시, 동일값 무시(스팸 방지 선례).</summary>
        public void SetEmittingLocal(bool on)
        {
            if (_emitting == on) return;   // 동일 값 무시 — WeaponSwingTrail.SetEmitting 선례(프레임 호출 스팸 방지)
            _emitting = on;
            if (_trail != null)
                _trail.emitting = on;
        }

        private void OnDestroy()
        {
            ReleaseCount();
            // TrailRenderer는 무기 파괴와 함께 소멸(부모 자식 — 수명 무관 — 별도 파괴 불필요)
        }

        /// <summary>[ST5] 활성 카운트 반납 — 멱등(재부착 동기 반납/OnDestroy 어느 쪽에서 호출해도 중복 차감 없음).</summary>
        private void ReleaseCount()
        {
            if (_counted)
            {
                _counted = false;
                _activeCount--;
            }
        }

        // ================================================================
        // 내부: 설정 적용 / 지오메트리 헬퍼
        // ================================================================

        /// <summary>트레일 설정 — WeaponSwingTrail.ApplyWeaponConfig BOTW 규격 동일(색 계약만 청백으로 상이).</summary>
        private static void ConfigureTrail(TrailRenderer trail)
        {
            trail.time = TrailTime;
            trail.minVertexDistance = MinVertexDistance;
            trail.widthMultiplier = TipWidth;
            // width 3키 곡선 — 뿌리 굵게(1.0) → 60% 지점 0.65로 완만히 감쇠 → 끝에서 소실(0). (45차 P4 규격 동일)
            AnimationCurve width = new AnimationCurve();
            width.AddKey(0f, 1f);
            width.AddKey(0.6f, 0.65f);
            width.AddKey(1f, 0f);
            trail.widthCurve = width;
            // [ST1 색 계약] 청백 시작(패망 청 계열; 병사 파랑 국가색 무손상) → 알파 0 소멸
            trail.startColor = StartColorPaleBlue;
            trail.endColor = new Color(StartColorPaleBlue.r, StartColorPaleBlue.g, StartColorPaleBlue.b, 0f);
            trail.numCapVertices = 4;      // 팁 마감 둥글림 — 끊긴 궤적 방지
            trail.alignment = LineAlignment.View;   // BOTW식 카메라 정면 빌보드 궤적
            trail.generateLightingData = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            // [ST2] 머티리얼 중복 금지 — WeaponSwingTrail 공개 팩토리 위임 재사용
            // (URP Particles/Unlit additive 세팅 + Sprites/Default 폴백까지 사양 동일 보장)
            trail.material = WeaponSwingTrail.CreateTrailMaterialShared(1.0f);
        }

        /// <summary>그립 끝면 자동 판정 — WeaponEquipManager 휴리스틱 선례(손 원점에 가까운 끝면이 그립). 0=최소축 끝, 1=최대축 끝.</summary>
        private static float AutoGripRatio(Vector3 eMin, Vector3 eMax, Transform hand, Transform pivot)
        {
            Vector3 anchor = hand != null ? hand.position : pivot.position;
            return (eMin - anchor).sqrMagnitude <= (eMax - anchor).sqrMagnitude ? 0f : 1f;
        }

        /// <summary>최장축 끝면 중심(WeaponEquipManager.FaceCenter 선례 동일 — 지정축 끝값, 나머지 축은 bounds 중심).</summary>
        private static Vector3 FaceCenter(Bounds b, int axis, Vector3 end)
        {
            Vector3 c = b.center;
            if (axis == 0) c.x = end.x;
            else if (axis == 1) c.y = end.y;
            else c.z = end.z;
            return c;
        }
    }
}
