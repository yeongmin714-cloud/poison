using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_10_TerritoryCombat 전용 플레이어 애니메이션 부트 (Phase 1).
    ///
    /// [배경] 코드 생성 플레이어는 PlayerPlaceholder.TryLoadGLBModel → ModelAnimatorAssigner.ForceBiped
    /// 경로로 GLB 모델(PlayerModel)에 레거시 애니 스택(Procedural/Neural/Hybrid/ProceduralBoneMap)이
    /// 자동부착된다. 메인씬은 이 경로를 쓰지 않는다(PlayerMovement 228행 주석:
    /// "Neural/Hybrid 보류 — Player_AC(HumanoidClipDriver) 단일 경로, 유산 자동부착 제거").
    /// 휴리스틱 본 조작이 모델 골반을 지형 아래로 밀어 침하시키고 Player_AC 재생도 막는 것이 근본원인.
    ///
    /// [동작] PlayerModel 발견(최대 5초 대기) → 레거시 컴포넌트 전부 Destroy → 루트 빈 Animator 제거 →
    /// PlayerModel에 Player_AC 부착 → Rigidbody 무해화 재확인 → 루트에 HumanoidClipDriver 부착 →
    /// 2초 후 침하 감시 1회. 전 구간 try-catch 격리 — 부트가 실패해도 Play는 계속된다.
    /// </summary>
    public class TestPlayerAnimatorBoot : MonoBehaviour
    {
        private const string ModelName = "PlayerModel";
        private const string PlayerAcPath = "Animation/Controllers/Player_AC";
        private const float FindTimeout = 5f;

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private IEnumerator Boot()
        {
            // ── 1) PlayerModel 대기 (최대 5초) ──────────────────────────────
            Transform model = null;
            float deadline = Time.realtimeSinceStartup + FindTimeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    model = FindModel(player.transform);
                    if (model != null) break;
                }
                yield return null;
            }

            if (model == null)
            {
                Debug.LogWarning("[TestPlayerAnimatorBoot] ⚠️ PlayerModel 5초간 미발견(GLB 미로드 추정) — 부트 생략, Play는 계속");
                yield break;
            }

            // ── 2) PlayerPlaceholder.Start + ForceBiped 완료 보장 1프레임 ─────
            yield return null;

            var playerRoot = GameObject.FindWithTag("Player");
            if (playerRoot == null || model == null)
            {
                Debug.LogWarning("[TestPlayerAnimatorBoot] ⚠️ 부트 직전 플레이어/모델 소실 — 생략, Play는 계속");
                yield break;
            }

            // ── 3)~7) 정리 + 부착 (NRE 격리) ────────────────────────────────
            var removed = new List<string>();
            bool bootOk = true;
            try
            {
                StripLegacyAnimation(playerRoot.transform, removed);   // 3) 레거시 Procedural/Neural 제거
                ConfigureRootAnimator(playerRoot.transform, removed);  // 4) 루트 빈 Animator 제거
                ConfigureModelAnimator(model);                         // 5) PlayerModel에 Player_AC 부착
                NeutralizeModelRigidbody(model);                       // 6) Rigidbody 무해화 재확인
                EnsureClipDriver(playerRoot.transform);                // 7) HumanoidClipDriver 부착
            }
            catch (System.Exception ex)
            {
                bootOk = false;
                Debug.LogError($"[TestPlayerAnimatorBoot] ❌ 부트 중 예외 — 무시하고 Play 유지: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            // ── 9) 최종 로그 ───────────────────────────────────────────────
            string removedTxt = removed.Count > 0 ? string.Join(", ", removed) : "제거 대상 없음";
            if (bootOk)
                Debug.Log($"[TestPlayerAnimatorBoot] ✅ Player_AC 부착+레거시 제거 완료 (제거 {removed.Count}개: {removedTxt})");
            else
                Debug.LogWarning($"[TestPlayerAnimatorBoot] ⚠️ 부트 부분 실패 — 제거/부착 불완전 가능 (제거 {removed.Count}개: {removedTxt}), Play는 계속");

            // ── 8) 침하 감시 — 부트 완료 2초 후 1회 ─────────────────────────
            StartCoroutine(SinkWatch(playerRoot.transform, model));
        }

        /// <summary>플레이어 루트에서 이름이 "PlayerModel"인 자식을 찾는다(직계 우선, 없으면 전체 자식).</summary>
        private static Transform FindModel(Transform root)
        {
            Transform direct = root.Find(ModelName);
            if (direct != null) return direct;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ModelName) return t;
            }
            return null;
        }

        /// <summary>
        /// 3) 레거시 애니 컴포넌트 전부 제거(루트 포함 전체 자식 대상, 각 타입별 루프).
        /// 런타임이므로 Destroy 사용(프레임 말 일괄 파괴 — RequireComponent 의존 무관 제거 가능).
        /// ProceduralBoneMap은 여러 컴포넌트의 RequireComponent 의존 대상이므로 마지막에 제거.
        /// </summary>
        private static void StripLegacyAnimation(Transform root, List<string> removed)
        {
            DestroyAll<ProjectName.Systems.Animation.ModelAnimatorAssigner>(root, removed);
            DestroyAll<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>(root, removed);
            DestroyAll<ProjectName.Systems.Animation.Neural.NeuralAnimationController>(root, removed);
            DestroyAll<ProjectName.Systems.Animation.Neural.HybridAnimationController>(root, removed);
            DestroyAll<ProjectName.Systems.QuadrupedProceduralAnimation>(root, removed);
            DestroyAll<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>(root, removed); // 마지막
        }

        private static void DestroyAll<T>(Transform root, List<string> removed) where T : Component
        {
            var comps = root.GetComponentsInChildren<T>(true);
            foreach (var c in comps)
            {
                if (c == null) continue;
                removed.Add($"{typeof(T).Name}@{c.gameObject.name}");
                Destroy(c);
            }
        }

        /// <summary>
        /// 4) 플레이어 루트의 빈 Animator 제거 — PlayerCombat.cs 101행 선례:
        /// RequireComponent(Animator)가 루트에 만든 컨트롤러 없는 Animator가 Player_AC 재생을 깬다.
        /// 컨트롤러가 있는 유효 Animator는 보호(유지).
        /// </summary>
        private static void ConfigureRootAnimator(Transform root, List<string> removed)
        {
            var anims = root.GetComponents<Animator>(); // 루트 자신만
            foreach (var a in anims)
            {
                if (a == null) continue;
                if (a.runtimeAnimatorController == null)
                {
                    removed.Add($"Animator(루트 빈 컨트롤러)@{root.name}");
                    Destroy(a);
                }
            }
        }

        /// <summary>
        /// 5) PlayerModel에 Player_AC 부착(InventoryWindow 1004~1012행 선례와 동일 로드 경로).
        /// applyRootMotion=false, cullingMode=AlwaysAnimate. 컨트롤러 로드 실패 시 LogError 1회 후 계속.
        /// </summary>
        private static void ConfigureModelAnimator(Transform model)
        {
            Animator anim = model.GetComponent<Animator>();
            if (anim == null)
                anim = model.gameObject.AddComponent<Animator>();

            var ctrl = Resources.Load<RuntimeAnimatorController>(PlayerAcPath);
            if (ctrl == null)
            {
                Debug.LogError($"[TestPlayerAnimatorBoot] ❌ Player_AC 로드 실패: Resources/{PlayerAcPath} — 애니 미재생(Play는 계속)");
                return;
            }

            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal; // ForceBiped가 세팅한 Fixed+animatePhysics(Procedural 전용) 되돌리기
        }

        /// <summary>6) PlayerModel Rigidbody 무해화 재확인 — 이전 수정분(isKinematic=true 등) 유지 보장.</summary>
        private static void NeutralizeModelRigidbody(Transform model)
        {
            Rigidbody rb = model.GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        /// <summary>
        /// 7) 플레이어 루트에 HumanoidClipDriver 부착(기존 없을 때만). Start에서
        /// GetComponentInChildren<Animator> + GetComponentInParent(CC/PlayerMovement/PlayerCombat)로
        /// 탐색하므로 루트 부착이 정상 동작한다. mode는 기본값(Player) 사용.
        /// </summary>
        private static void EnsureClipDriver(Transform root)
        {
            if (root.GetComponent<HumanoidClipDriver>() != null) return;
            root.gameObject.AddComponent<HumanoidClipDriver>();
        }

        /// <summary>
        /// 8) 침하 감시 — 부트 완료 2초 후 1회. 모델 전체 Renderer bounds.center.y가 루트 y보다
        /// 0.5m 이상 낮으면(침하 서명: 루트 캡슐은 착지, 모델만 아래로 밀림) 경고 로그.
        /// 수치는 정상/이상 무관 항상 기록(증거 수집용).
        /// </summary>
        private IEnumerator SinkWatch(Transform root, Transform model)
        {
            yield return new WaitForSeconds(2f);

            if (root == null || model == null)
                yield break;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.Log("[TestPlayerAnimatorBoot] [침하감시] 렌더러 없음 — 스킵");
                yield break;
            }

            try
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
                }

                float dev = bounds.center.y - root.position.y;
                Debug.Log($"[TestPlayerAnimatorBoot] [침하감시] 모델 bounds.center.y={bounds.center.y:F3} / 루트 y={root.position.y:F3} / 편차={dev:F3}m");

                if (dev < -0.5f)
                    Debug.LogWarning("[TestPlayerAnimatorBoot] ⚠️ 모델 침하 감지 — 모델 중심이 루트보다 0.5m 이상 낮음 (증거 수집용)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestPlayerAnimatorBoot] 침하 감시 예외(무시): {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
