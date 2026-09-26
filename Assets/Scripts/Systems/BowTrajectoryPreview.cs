using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-09-25 젤다식 탑다운 조준 궤적선] 활 드로 중 지면에 뿌려지는 궤적 예측선.
    /// 클래식 탑다운 젤다의 활 조준처럼, 드로 파워에 따른 실제 발사 물리(ArrowProjectile과 동일:
    /// 속도 = ArrowSpeed×(0.7+0.5×power), 중력 = Physics.gravity×0.22)로 화살 궤적을 계산해
    /// 소프트 글로우 점들(PooledBILLBOARD)로 지면 위에 그린다. 풀 파워일수록 먼/높이 도달.
    ///
    /// 핵심: **기존 시스템을 전혀 건드리지 않는다** — BowAimState(드로/파워)만 폴링하고,
    /// 플레이어는 Tag "Player"로 재조회한다. 활 드로 중에만 점들을 활성화, 릴리즈/드로 종료 시 숨김.
    /// 뷰는 게임 고정 탑다운(단일 카메라) — 각 점은 매 프레임 카메라를 향해 빌보드(항상 정면).
    ///
    /// 구조: static 파사드 + 숨은 호스트(SlashFxHost 선례, 영속 싱글턴 — 처음 사용 시 생성 후 유지).
    /// </summary>
    public static class BowTrajectoryPreview
    {
        /// <summary>호스트 지연 생성 — PlayerCombat 활 드로 시작 시 1회 호출. 이후 영속.</summary>
        public static void Ensure()
        {
            if (_host != null) return;
            var go = new GameObject("BowTrajectoryPreview") { hideFlags = HideFlags.HideAndDontSave };
            _host = go.AddComponent<BowTrajectoryHost>();
        }

        private static BowTrajectoryHost _host;
    }

    /// <summary>궤적선 호스트 — LateUpdate에서 BowAimState 폴링 + 궤적 계산 + 풀링된 점들 업데이트.</summary>
    public sealed class BowTrajectoryHost : MonoBehaviour
    {
        /// <summary>점 풀 크기 = 궤적 표본 수(발사~낙하).</summary>
        private static readonly int PoolSize = 28;
        /// <summary>ArrowManager 기본 발사 속도(m/s)와 동일 — 발사 물리 일치.</summary>
        private static readonly float ArrowSpeed = 70f;
        /// <summary>ArrowProjectile.GravityScale과 동일 — 축소 중력(약 0.22g).</summary>
        private static readonly float GravityScale = 0.22f;
        /// <summary>궤적 표본 시간 간격(s) — 작을수록 선이 촘촘(0.37s 비행 ≈ 12~20점).</summary>
        private static readonly float SampleDt = 0.03f;
        /// <summary>낙하 판정 기준 높이(발사 원점 아래로 이만큼 내려가면 착지).</summary>
        private static readonly float LandDrop = 0.15f;

        private GameObject[] _dots;
        private Material _dotMat;
        private GameObject _landMarker;      // [B 고품질] 착지점 엔드포인트 마커(파워↑ 멀어짐)
        private Material _landMat;
        private float _landPulse = 0f;

        private void Awake()
        {
            _dotMat = BuildDotMaterial();
            _dots = new GameObject[PoolSize];
            for (int i = 0; i < PoolSize; i++)
                _dots[i] = BuildDot();
            _landMat = BuildLandMaterial();
            _landMarker = BuildLandMarker();
        }

        private void LateUpdate()
        {
            if (this == null || !gameObject || !gameObject.activeInHierarchy) return;

            if (!BowAimState.Drawing || BowAimState.ReleasePending)
            {
                HideAll();
                return;
            }

            var playerGo = GameObject.FindGameObjectWithTag("Player");
            Transform player = playerGo != null ? playerGo.transform : null;
            if (player == null) { HideAll(); return; }

            Vector3 fwd = new Vector3(player.forward.x, 0f, player.forward.z);
            if (fwd.sqrMagnitude < 1e-6f) fwd = player.forward;
            fwd.Normalize();

            Vector3 origin = player.position + Vector3.up * 1.4f + fwd * 0.6f;   // [QA#4] 실제 머즐(PlayerCombat +fwd0.6+up1.4) 정렬
            float speed = ArrowSpeed * (0.7f + 0.5f * BowAimState.Power);
            Vector3 vel = fwd * speed;
            Vector3 g = Physics.gravity * GravityScale;
            float ground = origin.y;   // 평탄 지면 가정(지형 높이 API 미의존 — 궤적은 발사 높이로 낙하 판정)

            Vector3 p = origin;
            Vector3 v = vel;
            int n = 0;
            Vector3 landPoint = p;   // [B] 착지점 트래킹 — 점 루프가 끝나는 지면 지점
            for (float t = 0f; t < 2.0f; t += SampleDt)   // [튜닝] 최대 2s — 보통 0.37s 비행에서 낙하로 종료
            {
                if (n >= PoolSize) break;
                if (p.y < ground - LandDrop) break;   // 지면 도달(충돌점) — 이후 소멸
                Transform dt_ = _dots[n].transform;
                dt_.position = p;
                _dots[n].SetActive(true);
                n++;
                v += g * SampleDt;
                p += v * SampleDt;
                landPoint = p;
            }
            // 남은 점 숨김
            for (int i = n; i < PoolSize; i++)
                if (_dots[i].activeSelf) _dots[i].SetActive(false);

            // [B] 착지점 마커 — 궤적 끝 지면에 골드 발광 마커(파워 풀수록 멀리). 펄스 스케일.
            if (_landMarker != null && n > 0)
            {
                _landPulse += Time.deltaTime * 5f;
                float pulse = 0.82f + 0.18f * Mathf.Sin(_landPulse);
                _landMarker.transform.position = new Vector3(landPoint.x, 0.06f, landPoint.z);
                _landMarker.transform.localScale = Vector3.one * (0.9f * pulse);
                _landMarker.SetActive(true);
            }

            // 빌보드 — 각 점을 탑다운 카메라로 향하게(항상 정면, 고품질 베이크 글로우가 또렷).
            // [QA#1] Quad 전면 법선은 -Z — LookRotation의 +Z를 카메라 반대(점→카메라 아님)로 + _Cull 0.
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camPos = cam.transform.position;
                Vector3 camUp = cam.transform.up;   // [QA#3] 근수직 탑다운 LookRotation up 퇴화 방지
                for (int i = 0; i < n; i++)
                {
                    GameObject d = _dots[i];
                    Vector3 to = d.transform.position - camPos;   // +Z가 카메라 반대 → -Z(전면)가 카메라
                    if (to.sqrMagnitude < 1e-6f) continue;
                    d.transform.rotation = Quaternion.LookRotation(to.normalized, camUp);
                }
            }
        }

        private void HideAll()
        {
            if (_dots != null)
                for (int i = 0; i < PoolSize; i++)
                    if (_dots[i].activeSelf) _dots[i].SetActive(false);
            if (_landMarker != null && _landMarker.activeSelf) _landMarker.SetActive(false);
        }

        private GameObject BuildLandMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BowTrajectoryLand";
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 탑다운 카메라 기준 평평(위쪽 바라봄)
            go.transform.localScale = Vector3.one * 0.9f;
            go.SetActive(false);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = _landMat;
                mr.receiveShadows = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>[B] 착지점 마커 재질 — golg 발광(StarFlare/shad ow_glow), 애더티브. Robusto 탑다운 가시성.</summary>
        private static Material BuildLandMaterial()
        {
            var tex = Resources.Load<Texture2D>("UI/StarFlare");
            if (tex == null) tex = Resources.Load<Texture2D>("UI/shadow_glow");
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);              // Additive
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (tex != null) mat.mainTexture = tex;
            mat.color = new Color(1f, 0.9f, 0.35f, 0.9f);   // 골드 — 착지점 명확 표식
            return mat;
        }

        private GameObject BuildDot()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BowTrajectoryDot";
            // [QA#2] 호스트 자식 + HideAndDontSave — 씬 전환(Single) 시 루트 파괴와 함께 공존,
            //         호스트가 살아남아도 걸린 참조가 되지 않는다(크래시 방지).
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.5f;
            go.SetActive(false);   // 기본 숨김 — 드로 중만 활성
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = _dotMat;
                mr.receiveShadows = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>점 재질 — URP Particles/Unlit + shadow_glow 베이크 텍스처 + 애더티브(흰 발광).</summary>
        private static Material BuildDotMaterial()
        {
            var tex = Resources.Load<Texture2D>("UI/shadow_glow");
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);              // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);                  // Additive
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            // [QA#1] 백페이스 컬링 무효 — 방향 벨트기 위해 양면 렌더(정방향 빌보드와 함께 안전망).
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (tex != null) mat.mainTexture = tex;
            mat.color = new Color(0.95f, 0.97f, 1f, 0.85f);   // 옅은 흰·청 발광
            return mat;
        }
    }
}