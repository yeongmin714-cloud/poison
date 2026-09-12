using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 3단계 이펙트: 에셋 스토어 무료 프리팹 기반 스윙/피격 FX 정적 러너.
    ///   - 스윙: Free Slash VFX "Slash VFX.prefab" (Assets/Resources/FX/Slash/Slash VFX)
    ///   - 피격: Matthew Guz "Basic Hit / Basic Hit 2" (Assets/Resources/FX/Impact/BasicHit, BasicHit2)
    ///
    /// 프리팹은 에디터 인스톨러(Tools/VFX/Install Slash+Impact to Resources, -executeMethod:
    /// ProjectName.EditorTools.VFXResourceInstaller.InstallAll)가 Resources 하위로 복사해두므로
    /// Resources.Load로 지연 로드 + static 캐시한다. 로드 실패 시 경고는 static bool로 1회만.
    ///
    /// HitVFX.cs의 정적 러너 패턴과 일치: static 클래스는 코루틴을 돌릴 수 없으므로
    /// 내부 숨은 호스트 MonoBehaviour(SlashFxHost, HideAndDontSave)를 지연 생성해 사용.
    ///
    /// 파괴 정책: duration+startLifetime.max 기반 동적 파괴도 가능하지만 3초 고정으로 충분
    /// (두 프리팹 모두 3초 내 완전 소진 — 주석 명시).
    ///
    /// [2026-09-12 P2 피격 FX 재색상] 외부 팩 프리팹의 보라·자주 기본색은 사용자 취향("피격시 보라색
    /// 점들이 떠다니는 건 별로")과 불일치 → 스폰 직후 TintParticles로 붉은/흰 계열 재색상
    /// (십자가 0.85,0.15,0.15 / 스윙 0.85,0.95,1.0 / 임팩트 0.8,0.2,0.2 — 하단 공용 헬퍼 참조).
    /// </summary>
    public static class SlashVFXRunner
    {
        /// <summary>스팸 방지: 마지막 스폰 후 이 시간 이내 재호출은 무시 (이중 발화 흡수).</summary>
        private const float MIN_SPAWN_INTERVAL = 0.08f;

        /// <summary>인스턴스 파괴 예약 시간 (고정).</summary>
        private const float DESTROY_AFTER = 3f;

        /// <summary>
        /// [2026-09-12 스트로크 미러 플립] 사용자 실측(테스트 영상 4): 빌보드 정면 뷰에서도 아크 진행이
        /// 좌우 반대로(우→좌) 읽힘 → X 미러로 스트로크 방향 플립(쿼드 로컬 X 반전 = 아크 진행 좌우 반전).
        /// 빌보드 Instantiate 직후, 롤(Rotate) 적용 전에 localScale로 적용. 정방향 스트로크로 판명되면
        /// 1f로 되돌릴 것(튜닝 상수). 크로스/임팩트에는 미적용 — 스윙(Slash VFX) 전용.
        /// </summary>
        private const float StrokeMirrorX = -1f;

        private const string SlashResourcePath = "FX/Slash/Slash VFX";
        private const string CrossSlashResourcePath = "FX/Slash/Multiple Slashes";
        private const string BasicHitResourcePath = "FX/Impact/BasicHit";
        private const string ConstructHitResourcePath = "FX/Impact/BasicHit2";

        // ── static 캐시/상태 ─────────────────────────────────────────
        private static GameObject _slashPrefab;
        private static GameObject _crossSlashPrefab;
        private static GameObject _basicHitPrefab;
        private static GameObject _constructHitPrefab;

        private static bool _slashLoadFailed;
        private static bool _crossSlashLoadFailed;
        private static bool _basicHitLoadFailed;
        private static bool _constructHitLoadFailed;
        private static bool _shaderErrorWarned;

        // 스팸 방지 타이머는 스윙/임팩트 분리: 같은 프레임에 스윙+임팩트가 연속 와도
        // 서로를 묵살하지 않게 하고, 동일 종류의 이중 발화(PlayerCombat+AttackSystem
        // 좌클릭 이중 처리 등)만 0.08s 쿨다운으로 흡수한다.
        private static float _lastSwingSpawnTime = -999f;
        private static float _lastImpactSpawnTime = -999f;
        private static float _lastCrossSpawnTime = -999f;   // 크로스 전용 쿨다운 — 스윙/임팩트와 독립
        private static SlashFxHost _host;

        // ================================================================
        // 공개 진입점
        // ================================================================

        /// <summary>
        /// 공격 스윙 FX. position: 플레이어 전방 스윙 지점, direction: 수평 스윙 방향.
        /// 로드 실패 시 1회 경고 후 조용히 반환 (전투 흐름 절대 방해 없음).
        /// </summary>
        public static void PlaySlash(Vector3 position, Vector3 direction) => PlaySlash(position, direction, 0f);

        /// <summary>
        /// 공격 스윙 FX (궤적 기울임 포함). arcRollDegrees: 로컬 Z축 회전각(도) —
        /// 0이면 수평 스윙. 2타 수직 궤적(-90)/3타 사선(-45) 등 궤적 평면 기울이기 전용 —
        /// [2026-09-12] 쿼드 자체는 카메라 수평 빌보드로 세워지므로 롤은 화면축 기준 기울임이다.
        /// [2026-09-12 스트로크 미러 플립] 빌보드 직후 X 스케일을 <see cref="StrokeMirrorX"/>로 반전해
        /// 아크 진행 방향을 좌우 플립한다(사용자 실측: 우→좌 역방향 읽힘). 크로스/임팩트에는 미적용.
        /// </summary>
        public static void PlaySlash(Vector3 position, Vector3 direction, float arcRollDegrees)
        {
            // 스팸 방지 (동일 스윙 이중 발화 흡수 — 임팩트와는 독립 쿨다운)
            if (Time.time - _lastSwingSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastSwingSpawnTime = Time.time;

            GameObject prefab = LoadSlashPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // [2026-09-12 카메라 수평 빌보드] 기존 Quaternion.LookRotation(dir)은 쿼드 +Z를 스윙 dir(원거리)로
            // 향하게 했다 — 3인칭 후방 카메라가 쿼드 뒷면(-Z)을 비스듬히 보게 되고, 슬래시 팩의 아크(+Z 정면
            // 제작이 일반적)는 뒤에서 보면 스트로크 진행이 좌우 미러링되어 "뒤에서 앞으로 휘두르는" 역스윙처럼
            // 읽혔다(사용자 2회차 리포트). → 쿼드를 카메라 시선 축(수평)에 정렬해 제작사 의도의 정면 뷰를 보장.
            // 궤적 개성(수직/사선)은 아래 로컬 Z 롤(화면축 기준 기울임)로만 표현하고, dir은 폴백/로그용으로 유지.
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            // [2026-09-12 스트로크 미러 플립] X 스케일 반전(튜닝 상수 StrokeMirrorX) — 아크 진행 방향 좌우 플립.
            // 빌보드 Instantiate 직후, 롤(Rotate) 적용 전에 실행해 로컬 X 미러가 롤 회전축과 간섭 없이 먹게 한다.
            instance.transform.localScale = new Vector3(StrokeMirrorX, 1f, 1f);
            // 로컬 Z축 롤 = 빌보드 이후 화면축 기준 궤적 기울이기 (수직/사선 궤적 표현은 여기서만 담당)
            if (Mathf.Abs(arcRollDegrees) > 0.01f)
                instance.transform.Rotate(0f, 0f, arcRollDegrees, Space.Self);
            // [2026-09-12 P2 피격 FX 재색상] 스윙 궤적 일관색 — 밝은 흰/청백 틴트(팩 기본 보라 톤 제거).
            // 파티클 재생 전에 적용해 첫 방출 파티클부터 새 색이 나온다.
            TintParticles(instance, new Color(0.85f, 0.95f, 1.0f), 0.1f);
            Debug.Log($"[SlashVFX] ✅ 스윙 FX 스폰 (pos={position}, faceDir={faceDir:F2}, roll={arcRollDegrees:F0}°, 발화시각={Time.time:F2}s)");   // 1회성 검증 아님 — 좌클릭마다 1줄, 발화 증거
            instance.name = "SlashVFX_Swing";

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Slash");
            ScheduleDestroy(instance);
        }

        /// <summary>
        /// 타 완료 시점 십자가 VFX — Free Slash VFX 팩의 "Multiple Slashes"(다중 슬래시 십자) 스폰.
        /// 스윙이 끝나는 지점(스테이지 경계 통과)에 1회 발화한다(HumanoidClipDriver 콤보 감시에서 호출).
        /// PlaySlash와 동일한 쿨다운/캐시/파괴 패턴. 프리팹 미설치 시 static 1회 경고 후 조용히 반환.
        /// [2026-09-12] 쿼드 오리엔테이션을 카메라 수평 빌보드로 변경(기존 LookRotation(dir)은 후방 카메라에서
        /// 뒷면 미러링 = 역스윙 체감 유발). direction은 빌보드 폴백/로그용으로 유지.
        /// </summary>
        public static void PlayCross(Vector3 position, Vector3 direction)
        {
            // 스팸 방지 (동일 크로스 이중 발화 흡수 — 스윙/임팩트와는 독립 쿨다운)
            if (Time.time - _lastCrossSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastCrossSpawnTime = Time.time;

            GameObject prefab = LoadCrossSlashPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // [2026-09-12 카메라 수평 빌보드] 히트 지점에서 쿼드 +Z를 카메라 시선 축(수평)에 정렬 —
            // 십자가가 후방 카메라에서도 항상 정면에서 읽힌다(뒷면 미러링 = 역스윙 체감 차단). 폴백 동일.
            Vector3 faceDir = CameraHorizontalFaceDir(position, dir);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(faceDir));
            instance.name = "SlashVFX_Cross";
            // [2026-09-12 P2 피격 FX 재색상] 보라색 팩 파티클("Multiple Slashes" 십자가) → 흰/붉은 타격
            // 마크 톤(0.85,0.15,0.15)으로 재색상 — 사용자 취향 반영(피격 보라 부유 입자 제거). 재생 전 적용.
            TintParticles(instance, new Color(0.85f, 0.15f, 0.15f), 0.15f);
            Debug.Log($"[SlashVFX] ✅ 크로스 FX 스폰 (Multiple Slashes, pos={position}, faceDir={faceDir:F2}, 발화시각={Time.time:F2}s)");

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "CrossSlash");
            ScheduleDestroy(instance);
        }

        /// <summary>
        /// 피격 임팩트 FX. Organic → BasicHit, Construct → BasicHit2, 그 외 → BasicHit.
        /// </summary>
        public static void PlayImpact(Vector3 position, CombatHitType type)
        {
            // 스팸 방지 (동일 임팩트 이중 발화 흡수 — 스윙과는 독립 쿨다운)
            if (Time.time - _lastImpactSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastImpactSpawnTime = Time.time;

            string resourcePath = type == CombatHitType.Construct ? ConstructHitResourcePath : BasicHitResourcePath;
            GameObject prefab = LoadImpactPrefab(resourcePath, type);
            if (prefab == null) return;

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = $"ImpactVFX_{type}";
            // [2026-09-12 P2 피격 FX 재색상] 보라색 팩 파티클("BasicHit"/"BasicHit2" 임팩트) → 붉은 계열
            // (0.8,0.2,0.2)로 재색상 — 사용자 취향 반영(피격 보라 부유 입자 제거). 재생 전 적용.
            TintParticles(instance, new Color(0.8f, 0.2f, 0.2f), 0.2f);

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Impact");
            ScheduleDestroy(instance);
        }

        // ================================================================
        // 내부: 카메라 수평 빌보드 faceDir 산식 (슬래시/크로스 공용)
        // ================================================================

        /// <summary>
        /// [2026-09-12] 슬래시/크로스 쿼드 공용 faceDir — 카메라 수평 빌보드.
        /// +Z 정면 관례 — 카메라가 아크의 앞면에서 보도록(미러링 방지): 쿼드 +Z를
        /// 스폰 지점→카메라 축의 수평 성분에 정렬해 3인칭 후방 카메라에서도
        /// 제작사 의도의 정면 뷰가 나오게 한다(뒷면 미러링 = 역스윙 체감 원인 제거).
        /// 폴백: Camera.main 부재 또는 수평 성분 퇴화 시 -dir(수평화), 그것도 소실 시 Vector3.forward.
        /// </summary>
        private static Vector3 CameraHorizontalFaceDir(Vector3 position, Vector3 dir)
        {
            Vector3 faceDir;
            var cam = Camera.main;
            if (cam != null)
                faceDir = cam.transform.position - position;   // 스폰 지점→카메라 축 — 쿼드 +Z가 카메라를 향함(카메라가 +Z 정면에서 아크를 봄)
            else
                faceDir = -dir;                                 // 폴백: 카메라 부재 — 스윙 방향 역방향(캐릭터 쪽 정면 뷰)
            faceDir.y = 0f;                                     // 수평 빌보드 — 카메라 피치가 궤적 기울임(roll)에 섞이지 않게 제거
            if (faceDir.sqrMagnitude < 0.0001f)
            {
                faceDir = -dir;                                 // 퇴화(카메라가 스폰 지점 수직 상하) → dir 역방향 폴백
                faceDir.y = 0f;
            }
            if (faceDir.sqrMagnitude < 0.0001f) faceDir = Vector3.forward;   // 최종 방어 — dir 수직 등 수평 성분 완전 소실
            return faceDir.normalized;
        }

        // ================================================================
        // 내부: 지연 로드 + 캐시 (실패 경고 1회)
        // ================================================================

        private static GameObject LoadSlashPrefab()
        {
            if (_slashPrefab != null) return _slashPrefab;
            if (_slashLoadFailed) return null;

            _slashPrefab = Resources.Load<GameObject>(SlashResourcePath);
            if (_slashPrefab == null)
            {
                _slashLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Slash/Slash VFX — 에디터 메뉴 Tools/VFX/Install Slash+Impact to Resources 실행 필요");
            }
            return _slashPrefab;
        }

        private static GameObject LoadCrossSlashPrefab()
        {
            if (_crossSlashPrefab != null) return _crossSlashPrefab;
            if (_crossSlashLoadFailed) return null;

            _crossSlashPrefab = Resources.Load<GameObject>(CrossSlashResourcePath);
            if (_crossSlashPrefab == null)
            {
                _crossSlashLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Slash/Multiple Slashes — 에디터 메뉴 Tools/VFX/Install Slash+Impact to Resources 실행 필요");
            }
            return _crossSlashPrefab;
        }

        private static GameObject LoadImpactPrefab(string resourcePath, CombatHitType type)
        {
            if (resourcePath == ConstructHitResourcePath)
            {
                if (_constructHitPrefab != null) return _constructHitPrefab;
                if (_constructHitLoadFailed) return null;
                _constructHitPrefab = Resources.Load<GameObject>(resourcePath);
                if (_constructHitPrefab == null)
                {
                    _constructHitLoadFailed = true;
                    Debug.LogWarning($"[SlashVFX] 로드 실패(1회만 경고): Resources/{resourcePath} (Construct 임팩트) — 인스톨러 실행 필요");
                }
                return _constructHitPrefab;
            }

            // Organic 및 분류 불가(None 등)는 모두 BasicHit 사용
            if (_basicHitPrefab != null) return _basicHitPrefab;
            if (_basicHitLoadFailed) return null;

            _basicHitPrefab = Resources.Load<GameObject>(resourcePath);
            if (_basicHitPrefab == null)
            {
                _basicHitLoadFailed = true;
                Debug.LogWarning("[SlashVFX] 로드 실패(1회만 경고): Resources/FX/Impact/BasicHit — 인스톨러 실행 필요");
            }
            return _basicHitPrefab;
        }

        // ================================================================
        // 내부: 파티클 재생 / 재색상 / 셰이더 감지 / 파괴 예약
        // ================================================================

        /// <summary>
        /// [2026-09-12 P2 피격 FX 재색상] 공용 헬퍼 — 외부 VFX 팩 프리팹("Multiple Slashes" 십자가,
        /// "BasicHit"/"BasicHit2" 임팩트, "Slash VFX" 스윙)의 보라·자주 기본색은 사용자 취향
        /// ("피격시 보라색 점들이 떠다니는 건 별로")과 불일치하므로, Instantiate 직후 root 및
        /// 자식 전체 ParticleSystem을 순회해 main.startColor를 baseColor±variance 랜덤 틴트
        /// (MinMaxGradient flat — 시스템별 1회 추첨)로 교체한다. 파티클 텍스처는 곱셈(multiply)
        /// 조명이라 흰 텍스처에서 틴트가 그대로 드러난다. 알파는 원본 startColor가 flat 단색인
        /// 경우에만 보존해 팩 프리팹의 페이드 알파 훼손을 막는다(그 외 모드는 baseColor 알파).
        /// </summary>
        internal static void TintParticles(GameObject root, Color baseColor, float variance)
        {
            if (root == null) return;

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;

                // main 모듈 1회 캐시 — ps.main 반복 접근 비용 회피(Unity 권장 패턴)
                var main = ps.main;

                // 원본 알파 보존 — flat 단색 모드에서만 신뢰 가능(그라디언트 모드는 알파 커브가 있어 폐기)
                float alpha = baseColor.a;
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                    alpha = main.startColor.color.a;

                // baseColor 채널별 ±variance 랜덤 틴트 → flat MinMaxGradient로 교체
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(
                    Mathf.Clamp01(baseColor.r + Random.Range(-variance, variance)),
                    Mathf.Clamp01(baseColor.g + Random.Range(-variance, variance)),
                    Mathf.Clamp01(baseColor.b + Random.Range(-variance, variance)),
                    Mathf.Clamp01(alpha)));
            }
        }

        /// <summary>일부 프리팹은 자체 재생 안 함 → 모든 ParticleSystem을 명시 재생.</summary>
        private static void PlayAllParticleSystems(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps != null)
                    ps.Play(true);
            }
        }

        /// <summary>첫 인스턴스에서 Renderer 스캔 — InternalErrorShader(마젠타) 발견 시 1회 경고.</summary>
        private static void DetectShaderErrorOnce(GameObject instance, string label)
        {
            if (_shaderErrorWarned) return;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null) continue;

                foreach (Material mat in materials)
                {
                    if (mat == null || mat.shader == null) continue;

                    if (mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        _shaderErrorWarned = true;
                        Debug.LogWarning($"[SlashVFX] ⚠️ InternalErrorShader 감지(마젠타, 1회만 경고): {label} — {mat.name}");
                        return;
                    }
                }
            }
        }

        /// <summary>숨은 호스트로 코루틴을 돌려 3초 후 파괴 예약 (파티클이 끝나도 GO 잔존 없음).</summary>
        private static void ScheduleDestroy(GameObject instance)
        {
            EnsureHost();
            if (_host != null)
                _host.StartCoroutine(DestroyAfterDelay(instance, DESTROY_AFTER));
            else
                Object.Destroy(instance, DESTROY_AFTER); // 폴백: 호스트 생성 실패 시 예약 파괴
        }

        private static System.Collections.IEnumerator DestroyAfterDelay(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance != null)
                Object.Destroy(instance);
        }

        /// <summary>숨은 호스트 지연 생성 (HitVFX.HitFlashRunner 패턴: 비활성 생성 → 활성화로 Awake 타이밍 이슈 회피).</summary>
        private static void EnsureHost()
        {
            if (_host != null) return;

            var hostGo = new GameObject("SlashFxHost")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hostGo.SetActive(false);
            _host = hostGo.AddComponent<SlashFxHost>();
            hostGo.SetActive(true);
        }
    }

    /// <summary>
    /// SlashVFXRunner의 내부 숨은 호스트 — static 클래스가 코루틴을 실행하기 위한 컨테이너.
    /// HideAndDontSave로 씬 언로드에도 유지, 3초 파괴 예약 코루틴만 담당.
    /// </summary>
    internal class SlashFxHost : MonoBehaviour
    {
    }
}
