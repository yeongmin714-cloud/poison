using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P8] 리그 정합 확정 프로브 — 진단 전용(숫자 계약 아님).
    /// P7 Play 실측(headScale=(1.557,1.557,1.557) 균일, footY=1.01/headY=1.81, 봉투[0.65~2.53]=1.88m,
    /// renderSpan=2.43m)의 두 가설 동시 확정:
    ///   H1 이중 스케일 — 스킨 렌더 = SMR.L2W × Σw(bone.L2W × bindpose): SMR과 본이 각각 ×S이면 스킨만 ×S².
    ///                    (S=1 EditMode 프로브에서 skin≡bones였던 이유 설명 — 1²=1)
    ///   H2 본 매핑 오배선 — 아바타 Head 본이 실제 두상 본이 아님(흉부/목 레벨 의심 — S=1 프로브 62%).
    /// Case A: 아바타 매핑 전수(HumanBodyBones 전체 GetBoneTransform) world y + lossyScale + 본 이름,
    ///         + 원본 계층 최상단 변형 6개(전체 경로) — 실제 두상 본 식별.
    /// Case B: 루트 스케일 사다리(1.0 / 1.552) — 각 단계 ①SMR aggregate bounds 스팬(봉투)
    ///         ②BakeMesh 기본(로컬→전체 L2W) 스팬 ③BakeMesh(useScale=true→스케일 제거 L2W) 스팬
    ///         ④아바타 Head y ⑤최상단 원본 본 y ⑥SMR lossyScale — 스킨이 ×S²로 나면 H1 확정.
    /// plausibility Assert만(진단 테스트 — 숫자 자체는 계약 아님). 실행은 QA 서브에이전트 증거.
    /// </summary>
    public class ArmorRigLayoutProbeP8Tests
    {
        private GameObject _spawned;

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null)
            {
                Object.DestroyImmediate(_spawned);
                _spawned = null;
            }
        }

        private SkinnedMeshRenderer[] SpawnHeat(out Animator animator)
        {
            var prefab = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
            Assert.That(prefab, Is.Not.Null, "[무번호 29-P8] heat 프리팹 로드 실패");
            _spawned = Object.Instantiate(prefab);
            Assert.That(_spawned, Is.Not.Null, "[무번호 29-P8] heat 프리팹 Instantiate 실패");
            var smrs = _spawned.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(smrs.Length, Is.GreaterThan(0), "[무번호 29-P8] SMR 없음");
            animator = _spawned.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "[무번호 29-P8] Animator 없음 — 아바타 평가 불가");
            animator.enabled = true;
            for (int i = 0; i < 3; i++) animator.Update(0.016f);
            return smrs;
        }

        /// <summary>Case A — [무번호 29-P8] 아바타 매핑 전수 + 원본 계층 최상단 본 식별.</summary>
        [Test]
        public void RigLayout_AvatarMapping_FullSweep()
        {
            SpawnHeat(out var animator);

            // ① 아바타 매핑 전수 — 핵심 상하축 본 우선 상세, 나머지 요약
            var sb = new System.Text.StringBuilder();
            var core = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightFoot, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
            foreach (var b in core)
            {
                var t = animator.GetBoneTransform(b);
                if (t == null) { sb.Append(b).Append("=null; "); continue; }
                sb.Append(b).AppendFormat(": {0}@y{1:F2}(s{2:F2}) ", t.name, t.position.y, t.lossyScale.y);
            }
            Debug.Log($"[29-P8] 핵심 본 — {sb}");

            int mapped = 0; sb.Clear();
            foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (b == HumanBodyBones.LastBone) break;
                var t = animator.GetBoneTransform(b);
                if (t == null) continue;
                mapped++;
                if (sb.Length < 900) sb.AppendFormat("{0}={1}@{2:F2}; ", b, t.name, t.position.y);
            }
            Debug.Log($"[29-P8] 매핑 전수 — mapped={mapped}개 상세: {sb}");

            // ② 원본 계층 최상단 변형 6개(전체 경로) — 실제 두상 본 식별용
            var all = _spawned.GetComponentsInChildren<Transform>(true);
            var tops = new System.Collections.Generic.List<(string path, float y)>();
            foreach (var t in all)
            {
                if (t == _spawned.transform) continue;
                tops.Add((t.name, t.position.y));
            }
            tops.Sort((a, b) => b.y.CompareTo(a.y));
            var tb = new System.Text.StringBuilder();
            for (int i = 0; i < tops.Count && i < 6; i++) tb.AppendFormat("{0}@{1:F2}; ", tops[i].path, tops[i].y);
            Debug.Log($"[29-P8] 계층 최상단 6 — {tb}");

            // ③ SMR transform 상태(이중 스케일 판별 기초 데이터)
            var smrs = _spawned.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                Debug.Log($"[29-P8] SMR — {smr.name}: localPos={smr.transform.localPosition.ToString("F2")}"
                          + $" localScale={smr.transform.localScale.ToString("F3")} lossy={smr.transform.lossyScale.ToString("F3")}"
                          + $" worldPos={smr.transform.position.ToString("F2")} bindSpan={smr.sharedMesh.bounds.size.y:F3}m");
            }

            Assert.That(mapped, Is.GreaterThan(10), "[무번호 29-P8] 아바타 매핑 본 10개 미만 — 매핑 자체 파손");
        }

        /// <summary>Case B — [무번호 29-P8] 스케일 사다리(1.0/1.552) — 스킨 ×S² vs 본 ×S 판별.</summary>
        [Test]
        public void RigLayout_ScaleLadder_SkinVsBones()
        {
            float[] scales = { 1.0f, 1.552f };
            foreach (float s in scales)
            {
                if (_spawned != null) { Object.DestroyImmediate(_spawned); _spawned = null; }
                SpawnHeat(out var animator);
                _spawned.transform.localScale = Vector3.one * s;
                for (int i = 0; i < 3; i++) animator.Update(0.016f);

                var smrs = _spawned.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                // ① 봉투 — SMR aggregate world bounds
                bool anyE = false; float eMin = 0f, eMax = 0f;
                foreach (var smr in smrs)
                {
                    if (smr == null) continue;
                    Bounds b = smr.bounds;
                    if (!anyE) { eMin = b.min.y; eMax = b.max.y; anyE = true; }
                    else { eMin = Mathf.Min(eMin, b.min.y); eMax = Mathf.Max(eMax, b.max.y); }
                }
                float envSpan = anyE ? eMax - eMin : -1f;

                // ② 베이크 기본(로컬 verts → 전체 L2W) — Play 2.43m 관측과 동일 관례
                float naive = BakeSpan(smrs, false);
                // ③ 베이크 useScale=true → 스케일 제거 L2W 매핑 — P5 scaleBake 관례
                float scaled = BakeSpan(smrs, true);

                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                float headY = head != null ? head.position.y : -1f;
                float smrLossy = smrs[0] != null ? smrs[0].transform.lossyScale.y : -1f;

                Debug.Log($"[29-P8] 사다리 S={s:F3} — envSpan={envSpan:F3}m naiveBake={naive:F3}m scaledBake={scaled:F3}m"
                          + $" headY={headY:F3} smrLossy={smrLossy:F3}");

                Assert.That(naive, Is.InRange(0.05f, 20f), $"[무번호 29-P8] S={s} naive 스팬 비정상");
                Assert.That(envSpan, Is.InRange(0.05f, 20f), $"[무번호 29-P8] S={s} 봉투 스팬 비정상");
            }
        }

        /// <summary>SMR 집합 베이크 y스팬 — useScale=false: 로컬 verts→전체 L2W / true: useScale 베이크→스케일 제거 L2W.</summary>
        private static float BakeSpan(SkinnedMeshRenderer[] smrs, bool useScale)
        {
            bool any = false; float mn = 0f, mx = 0f;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh tmp = new Mesh();
                try { smr.BakeMesh(tmp, useScale); }
                catch (System.Exception) { Object.DestroyImmediate(tmp); continue; }
                Vector3[] verts = tmp.vertices;
                Object.DestroyImmediate(tmp);
                Matrix4x4 l2w = smr.transform.localToWorldMatrix;
                if (useScale)
                {
                    Vector3 lossy = smr.transform.lossyScale;
                    l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation,
                        new Vector3(Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                                    Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                                    Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z));
                }
                foreach (var v in verts)
                {
                    float y = l2w.MultiplyPoint3x4(v).y;
                    if (!any) { mn = y; mx = y; any = true; }
                    else { if (y < mn) mn = y; if (y > mx) mx = y; }
                }
            }
            return any ? mx - mn : -1f;
        }
    }
}
