using UnityEngine;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [91차 F3] 대상 앵커 화면 투영 공용 유틸 — 상호작용 패널/대화 패널/E프롬프트 3소비처 단일소스.
    ///
    /// 진단(마을씬 실측): Camera.main이 유저가 보는 카메라와 다른 카메라(멀티 카메라 씬)이거나 투영이
    /// 무효(z ≤ 0)면 패널 배치가 실패/무효 좌표 → 기본 흐름 위치(우상단)에 고정됐다.
    ///
    /// [92차 결함2] 계약 변경(PlacementTick 폴백 튐 수리): 유효 판정 = <b>z&gt;0(카메라 전방) 단일 기준</b> —
    /// 화면 좌표는 화면 밖이어도 항상 반환한다. 구 ±64px 화면 마진 검사 폐기: 근접/대화 카메라
    /// (FOV 30~45°, 2~3m)에서는 프러스텀 가장자리 앵커까지 "화면 밖"으로 무효화되어 개방 1초 후
    /// PlacementTick이 폴백(하단 중앙)으로 튀었다("부착 안 됨" 체감). 화면 안 배치는 호출부가
    /// 루트 좌표 클램프로 담당(InteractionPanelUTK/NPCDialoguePanelUTK — 패널 크기 고려).
    /// false = 전 카메라 z&lt;0(뒤)일 때만 — 호출부 폴백 배치/무배치 계약.
    ///
    /// 계약:
    ///  ① Camera.main 우선 — 대상 렌더러 bounds(부재 시 transform.position) 기준 앵커 투영.
    ///     유효 판정 = vp.z &gt; 0 [92차 결함2 — 화면 내 판정 폐기].
    ///  ② z≤0 → Camera.allCameras 순회(최초 유효 카메라 채택).
    ///  ③ 전부 무효 → false(호출부 폴백 배치/무배치 계약).
    ///  ④ 카메라 결정/무효 전이 시 1회성 진단 로그(대상 변경 재로그, 스로틀 0.5s).
    /// </summary>
    public static class TargetScreenProjector
    {
        /// <summary>[91차 F3] 진단 로그 스로틀(초) — 대상/카메라 변경 시 재로그.</summary>
        private const float LogThrottleSeconds = 0.5f;

        private static Component _lastLoggedTarget;
        private static string _lastLoggedCam;
        private static float _lastLogTime = -999f;
        private static bool _lastLoggedInvalid;

        /// <summary>
        /// [91차 F3 → 92차 결함2 계약 변경] 대상 앵커 투영 — Camera.main → allCameras 폴백 순회 후
        /// 최초 z&gt;0 카메라의 화면 px 반환. 앵커 = 렌더러 bounds 기준 [92차] 공용 산식(ComputeAnchor),
        /// bounds 부재 시 transform.position + upOffsetM.
        /// 유효 = z&gt;0만 — 화면 밖 좌표도 반환(호출부가 루트 클램프로 패널 전체를 화면 안에 배치).
        /// 실패(전 카메라 z≤0) 시 false — 호출부 폴백 배치(InteractionPanelUTK 하단 중앙) 또는 무배치 계약.
        /// </summary>
        public static bool TryProject(Component target, float upOffsetM, out Vector2 screenPx, out string camName)
        {
            screenPx = default;
            camName = null;
            if (target == null) return false;
            if (Screen.width <= 0 || Screen.height <= 0) return false;

            Vector3 anchor = ComputeAnchor(target, upOffsetM);

            // ① Camera.main 우선
            if (IsProjectionValid(Camera.main, anchor, out screenPx, out camName))
            {
                LogDecision(target, camName, screenPx);
                return true;
            }

            // ② z≤0 → Camera.allCameras 순회(최초 유효 카메라 채택 — 멀티 카메라 씬 수리)
            var all = Camera.allCameras;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsProjectionValid(all[i], anchor, out screenPx, out camName))
                {
                    LogDecision(target, camName, screenPx);
                    return true;
                }
            }

            // ③ 전부 무효(전 카메라 뒤) — 폴백 계약
            LogInvalid(target);
            return false;
        }

        /// <summary>[91차 F3] 대상 시각 bounds 유무 — 앵커 산식 분기용(F4).</summary>
        public static bool HasVisualBounds(Component target)
        {
            if (target == null) return false;
            GetTargetBounds(target, out bool has);
            return has;
        }

        /// <summary>
        /// 앵커 월드점 — [92차 결함2] bounds 있으면 (center.x, <c>center.y + extents.y×0.15</c>, center.z)
        /// (머리 살짝 위 — 근접/대화 카메라 프러스텀 안쪽. 구 max.y + upOffsetM 폐기 — 머리 위 앵커가
        /// 프러스텀 상단 밖으로 나가 PlacementTick 무효의 원인), bounds 부재 시 position + up×upOffsetM.
        /// upOffsetM은 bounds 부재 폴백 전용 오프셋으로 승계(호출처 시그니처 계약 유지).
        /// </summary>
        private static Vector3 ComputeAnchor(Component target, float upOffsetM)
        {
            Bounds b = GetTargetBounds(target, out bool has);
            if (!has)
                return target.transform.position + Vector3.up * upOffsetM;
            // [92차 결함2] 앵커 = bounds 중심 + 높이 15% — 프러스텀 안쪽 고정
            return new Vector3(b.center.x, b.center.y + b.extents.y * 0.15f, b.center.z);
        }

        /// <summary>대상 시각 bounds — 자식 렌더러 합산(EInteractionPromptUTK GetTargetBounds 동일 판정).</summary>
        private static Bounds GetTargetBounds(Component target, out bool has)
        {
            has = false;
            Bounds b = default;
            var rends = target.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                if (!has) { b = rends[i].bounds; has = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            return b;
        }

        /// <summary>
        /// [92차 결함2] 투영 유효 판정 — z&gt;0(카메라 앞)만 성패 기준. 화면 밖 좌표도 그대로 반환
        /// (호출부 클램프 배치 계약) — 구 ±64px 화면 마진 검사 폐기(PlacementTick 폴백 튐 원인).
        /// </summary>
        private static bool IsProjectionValid(Camera cam, Vector3 anchor, out Vector2 screenPx, out string camName)
        {
            screenPx = default;
            camName = null;
            if (cam == null) return false;
            Vector3 vp = cam.WorldToScreenPoint(anchor);
            if (float.IsNaN(vp.x) || float.IsNaN(vp.y) || float.IsInfinity(vp.x) || float.IsInfinity(vp.y))
                return false;
            if (vp.z <= 0f) return false;   // [91차 F3] z 가드 — 카메라 뒤 무효 / [92차 결함2] z>0 = 유일 성패 기준
            screenPx = new Vector2(vp.x, vp.y);   // [92차 결함2] 화면 밖 좌표도 반환 — 호출부 클램프 후 배치
            camName = cam.name;
            return true;
        }

        /// <summary>[91차 F3] 1회성 진단 로그 — 카메라 결정/무효 전이/대상 변경 시만(스로틀 0.5s, 다음 Play 판별용).</summary>
        private static void LogDecision(Component target, string camName, Vector2 screenPx)
        {
            bool changed = !ReferenceEquals(target, _lastLoggedTarget)
                           || !string.Equals(camName, _lastLoggedCam, System.StringComparison.Ordinal)
                           || _lastLoggedInvalid;   // 무효→유효 전이
            if (!changed) return;                                   // 변화 없는 프레임 — 무로그(스팸 방지)
            if (Time.unscaledTime - _lastLogTime < LogThrottleSeconds) return;   // 스로틀 0.5s
            _lastLoggedTarget = target;
            _lastLoggedCam = camName;
            _lastLoggedInvalid = false;
            _lastLogTime = Time.unscaledTime;
            Debug.Log($"[TargetProject] 대상 투영 — target={target.name} cam={camName} screenPx=({screenPx.x:F0},{screenPx.y:F0})");
        }

        /// <summary>[91차 F3] 투영 무효 전이 로그(1회성 — 스로틀 0.5s).</summary>
        private static void LogInvalid(Component target)
        {
            if (_lastLoggedInvalid) return;
            if (Time.unscaledTime - _lastLogTime < LogThrottleSeconds) return;
            _lastLoggedInvalid = true;
            _lastLoggedCam = null;
            _lastLogTime = Time.unscaledTime;
            Debug.Log($"[TargetProject] 투영 무효 — target={target.name} (전 카메라 뒤(z≤0) — 폴백 배치/무배치)");
        }
    }
}
