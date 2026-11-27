using UnityEngine;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [146차 P2] 실내 미니맵 RT 캡처 배커 (P2 A안 — 룸 상공 직교 카메라 1회 캡처).
    ///
    /// [역할] 실내씬 진입/동굴 층 전환/테스트씬 룸 빌드 완료 시점에 룸을 중앙 상공에서
    ///   직교 촬영해 512² 텍스처로 보관한다. MinimapUTK.Refresh()의 실내 분기가 이 텍스처를
    ///   배경으로 쓰고, MapToUv로 플레이어/유닛 위치를 절대 배치한다(월드 크롭 산식 우회 —
    ///   UpdateMapCrop 본체는 무수정 단일소스 계약 유지).
    ///
    /// [좌표계 계약] 캡처 카메라 rotation Euler(90,0,0) — 화면 위=+z(북), 오른쪽=+x(동).
    ///   ReadPixels 산출 Texture2D는 Unity 관례(v=0=하단)가 화면 하단(=−z)과 일치하므로
    ///   v=(z−min.z)/size.z 가 북방 상승 — MinimapUTK 표시 규약(UpdateMapCrop 크롭/
    ///   MinimapRouteOverlay py=half−dz·k의 +z=북=상)과 동일 방향.
    ///   MapToUv는 이 산식의 순수 함수 — EditMode에서 직접 수치 테스트 가능(IndoorMinimapTests).
    ///
    /// [수명/안전 계약]
    ///   - 임시 카메라/RT는 캡처 직후 즉시 파괴·반환(프로브 소유권 원칙 — RenderAuditProbe 선례).
    ///   - 교체 성공 시 이전 LatestTexture는 Object.Destroy — 누수 방지.
    ///   - 전체 try-catch — 실패 시 로그 1행+무음(크래시 금지 계약), 실패 캡처는 커밋하지 않아
    ///     기존 상태 보존(최초 실패면 HasCapture=false 유지).
    ///   - Random API 0건 — 캡처 프레이밍은 결정론(같은 룸 → 같은 시야).
    ///   - 천장: 프로젝트 탑다운 천장 금지 계약(CaveInteriorBuilder 천장 폐기 선례)상 실내 룸에는
    ///     상공을 막는 온천장이 없다 — 상공 촬영이 바닥 평면도를 담보된다.
    /// </summary>
    public static class IndoorMinimapBaker
    {
        private const string LogTag = "[146차 P2]";
        private const int CaptureSize = 512;          // RT 한 변(px) — 정사각(지름 프레임 1:1 수용)
        private const float FramePadding = 1.1f;      // 방 전체 여유 프레이밍 계수(벽 두께 잘림 방지)
        private const float CaptureHeight = 15f;      // AABB 최상단 위 촬영 높이(m)
        private const float AmbientBoostFactor = 6f;  // 캡처 중 앰비언트 ×6 상향
        private const float FlatAmbientFloor = 0.5f;  // ×6가 이보다 어두우면 Flat 1.0 폴백(암흑 룸 판독 보장)

        private static Texture2D _latest;
        private static Bounds _latestBounds;

        /// <summary>최신 실내 평면도 텍스처(readable RGBA32) — 캡처 전 null.</summary>
        public static Texture2D LatestTexture => _latest;

        /// <summary>캡처 당시 룸 전체 AABB(렌더러+콜라이더) — MapToUv의 좌표 기준 진실원.</summary>
        public static Bounds LatestBounds => _latestBounds;

        /// <summary>유효 캡처 존재 여부 — 실패/미캡처 시 false(MinimapUTK 실내 분기 게이트가 읽음).</summary>
        public static bool HasCapture => _latest != null;

        /// <summary>
        /// 실내 룸 상공 직교 캡처 — 훅 3곳(IndoorSceneTransition.BuildAndSettleIndoor /
        /// CaveLevelDirector.BuildFloor / CaveInteriorTestSetup)에서 호출.
        /// 실패해도 게임플레이에 영향 없음(무음 — 크래시 금지 계약).
        /// </summary>
        public static void CaptureFor(GameObject room)
        {
            if (room == null)
            {
                Debug.Log($"{LogTag} 실내 미니맵 캡처 스킵 — 룸 NULL(빌더 실패/폴백 경로)");
                return;
            }
            try
            {
                // 1) 룸 전체 AABB — 활성 렌더러+콜라이더 전수(실제 보이는 껍데기 기준).
                Bounds bounds = ComputeRoomBounds(room);
                if (bounds.size.x <= 0.001f || bounds.size.z <= 0.001f)
                {
                    Debug.Log($"{LogTag} 실내 미니맵 캡처 스킵 — AABB 크기 0({room.name})");
                    return;
                }

                // 2) 임시 직교 카메라 — 태그 없음(MainCamera 태그 계약 오염 금지), 중앙 상공 수직 하강.
                var probeGO = new GameObject("IndoorMinimapProbe") { hideFlags = HideFlags.HideAndDontSave };
                var cam = probeGO.AddComponent<Camera>();
                cam.orthographic = true;
                cam.aspect = 1f;   // 정사각 RT — 종횡비 고정(왜곡 방지. targetTexture 지정 시 RT 비율 우선)
                cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * FramePadding;
                cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + CaptureHeight, bounds.center.z);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 화면 위=+z(북), 오른쪽=+x(동)
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = CaptureHeight + bounds.size.y + 50f;   // 바닥까지 도달 보장
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f); // 배경 진회색 0.05 — 방 밖 여백 판독 톤
                cam.allowMSAA = false;

                // 3) RT 캡처 — 512² / 24bit depth / ARGB32(RenderAuditProbe.GetTemporary 선례).
                var rt = RenderTexture.GetTemporary(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;

                // 앰비언트 임시 상향 — 저장→캡처→정확 복원(finally 보장). 실내 ambientMode=Flat 강제 계약 유지.
                var prevAmbientMode = RenderSettings.ambientMode;
                var prevAmbientLight = RenderSettings.ambientLight;
                var prevFog = RenderSettings.fog;
                Texture2D captured = null;
                try
                {
                    Color boosted = prevAmbientLight * AmbientBoostFactor;
                    if (boosted.maxColorComponent < FlatAmbientFloor)
                        boosted = new Color(1f, 1f, 1f, 1f);   // Flat 1.0 폴백 — 동굴 등 암흑 룸 평면도 판독 보장
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = boosted;
                    RenderSettings.fog = false;                // 평면도 회색 덮임 방지(포그는 실내 톤 강제원이 있음 — 정확 복원)

                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    captured = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false)
                    {
                        name = "IndoorMinimapCapture",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    captured.ReadPixels(new Rect(0f, 0f, CaptureSize, CaptureSize), 0, 0);
                    captured.Apply(false, false);              // readable 유지(계약) — 파생 샘플링 여지
                }
                finally
                {
                    RenderSettings.fog = prevFog;
                    RenderSettings.ambientLight = prevAmbientLight;   // 정확 복원(캡처 성패와 무관)
                    RenderSettings.ambientMode = prevAmbientMode;
                    RenderTexture.active = prevActive;
                    cam.targetTexture = prevTarget;
                    RenderTexture.ReleaseTemporary(rt);        // RT 즉시 반환
                    Object.DestroyImmediate(probeGO);          // 임시 카메라 즉시 파괴(프로브 소유권 원칙)
                }

                if (captured == null) return;

                // 4) 커밋 — 성공 시에만 교체(실패 시 기존 캡처 유지 → HasCapture 상태 보존).
                if (_latest != null) Object.Destroy(_latest);  // 이전 캡처 누수 방지
                _latest = captured;
                _latestBounds = bounds;
                Debug.Log($"{LogTag} 실내 미니맵 캡처 완료 — {room.name} AABB {bounds.size.x:F1}×{bounds.size.z:F1}m (512² RT)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} 실내 미니맵 캡처 실패(무음 유지 — 기존 캡처 보존): {e.Message}");
            }
        }

        /// <summary>룸 전체 AABB — 활성 렌더러+콜라이더 전수 Encapsulate(둘 다 무존재 시 size 0 → 스킵 판정).</summary>
        private static Bounds ComputeRoomBounds(GameObject room)
        {
            Bounds bounds = default;
            bool has = false;
            foreach (var r in room.GetComponentsInChildren<Renderer>())
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            foreach (var c in room.GetComponentsInChildren<Collider>())
            {
                if (!has) { bounds = c.bounds; has = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return bounds;
        }

        /// <summary>
        /// 월드 좌표 → 캡처 평면도 UV 순수 함수 — u=(x−min.x)/size.x, v=(z−min.z)/size.z, Clamp01.
        /// v는 북방(+z) 상승 — 캡처 카메라 Euler(90,0,0) 좌표계 계약(클래스 헤더 참조).
        /// 엔진 상태 비의존 — EditMode에서 직접 수치 어서션 가능.
        /// </summary>
        public static Vector2 MapToUv(Vector3 worldPos, Bounds b)
        {
            float sizeX = Mathf.Max(b.size.x, 0.0001f);
            float sizeZ = Mathf.Max(b.size.z, 0.0001f);
            float u = (worldPos.x - b.min.x) / sizeX;
            float v = (worldPos.z - b.min.z) / sizeZ;
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }
    }
}
