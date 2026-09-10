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
    /// </summary>
    public static class SlashVFXRunner
    {
        /// <summary>스팸 방지: 마지막 스폰 후 이 시간 이내 재호출은 무시 (이중 발화 흡수).</summary>
        private const float MIN_SPAWN_INTERVAL = 0.08f;

        /// <summary>인스턴스 파괴 예약 시간 (고정).</summary>
        private const float DESTROY_AFTER = 3f;

        private const string SlashResourcePath = "FX/Slash/Slash VFX";
        private const string BasicHitResourcePath = "FX/Impact/BasicHit";
        private const string ConstructHitResourcePath = "FX/Impact/BasicHit2";

        // ── static 캐시/상태 ─────────────────────────────────────────
        private static GameObject _slashPrefab;
        private static GameObject _basicHitPrefab;
        private static GameObject _constructHitPrefab;

        private static bool _slashLoadFailed;
        private static bool _basicHitLoadFailed;
        private static bool _constructHitLoadFailed;
        private static bool _shaderErrorWarned;

        // 스팸 방지 타이머는 스윙/임팩트 분리: 같은 프레임에 스윙+임팩트가 연속 와도
        // 서로를 묵살하지 않게 하고, 동일 종류의 이중 발화(PlayerCombat+AttackSystem
        // 좌클릭 이중 처리 등)만 0.08s 쿨다운으로 흡수한다.
        private static float _lastSwingSpawnTime = -999f;
        private static float _lastImpactSpawnTime = -999f;
        private static SlashFxHost _host;

        // ================================================================
        // 공개 진입점
        // ================================================================

        /// <summary>
        /// 공격 스윙 FX. position: 플레이어 전방 스윙 지점, direction: 수평 스윙 방향.
        /// 로드 실패 시 1회 경고 후 조용히 반환 (전투 흐름 절대 방해 없음).
        /// </summary>
        public static void PlaySlash(Vector3 position, Vector3 direction)
        {
            // 스팸 방지 (동일 스윙 이중 발화 흡수 — 임팩트와는 독립 쿨다운)
            if (Time.time - _lastSwingSpawnTime < MIN_SPAWN_INTERVAL) return;
            _lastSwingSpawnTime = Time.time;

            GameObject prefab = LoadSlashPrefab();
            if (prefab == null) return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.LookRotation(dir));
            Debug.Log($"[SlashVFX] ✅ 스윙 FX 스폰 (pos={position}, 발화시각={Time.time:F2}s)");   // 1회성 검증 아님 — 좌클릭마다 1줄, 발화 증거
            instance.name = "SlashVFX_Swing";

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Slash");
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

            PlayAllParticleSystems(instance);
            DetectShaderErrorOnce(instance, "Impact");
            ScheduleDestroy(instance);
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
        // 내부: 파티클 재생 / 셰이더 감지 / 파괴 예약
        // ================================================================

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
