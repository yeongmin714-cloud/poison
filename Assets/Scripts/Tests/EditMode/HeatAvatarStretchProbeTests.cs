using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-P4b1] heat 아바타 스트레치 EditMode 실측 프로브 — 진단 전용(숫자 계약 아님).
    /// 가설: heat 프리팹의 스킨 바인드 스팬(≈1.16m)보다 아바타 T-포즈 골격 월드 스팬이
    ///       ≈1.35배(≈1.566m) 크다 — 아바타 평가 시점에 골격이 늘어나 메시가 스트레치된다.
    /// 절차: bind(SMR sharedMesh.bounds.size.y 최대) vs Instantiate 직후/Update(0) 3회 단계별 월드 스팬
    ///       실측 → ratio 산출 → 마커 증거 로그 1줄. plausibility Assert만(0.1~10.0m / 0.2~5.0),
    ///       측정 불가 시 Assert.Inconclusive + 구체 사유(가짜 숫자 금지).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음 — 배치 금지).
    /// </summary>
    public class HeatAvatarStretchProbeTests
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

        /// <summary>
        /// SMR 집합의 월드 스팬(모든 SMR world bounds 합산 box의 y 크기) —
        /// 합산 min/max(aggregate) 방식. 렌더러가 없으면 0f.
        /// </summary>
        private static float MeasureWorldSpan(SkinnedMeshRenderer[] smrs)
        {
            if (smrs == null || smrs.Length == 0) return 0f;
            bool any = false;
            float minY = 0f, maxY = 0f;
            for (int i = 0; i < smrs.Length; i++)
            {
                var smr = smrs[i];
                if (smr == null) continue;
                Bounds b = smr.bounds; // SMR world bounds는 스켈레톤(수정) 현재 상태 기준
                if (!any) { minY = b.min.y; maxY = b.max.y; any = true; }
                else { minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y); }
            }
            return any ? Mathf.Max(0f, maxY - minY) : 0f;
        }

        /// <summary>
        /// [무번호 28-P4b1v2] SMR 집합을 BakeMesh(현재 포즈)로 구워 정점 world y min/max 측정 —
        /// ArmorVisual PlayerBodyMeasure와 동일 방식(BakeMesh→verts→transform.localToWorldMatrix 변환).
        /// 프로브는 시간계열 5회 측정뿐이라 정점을 stride 없이 전부 샘플(런타임 예산 불필요).
        /// 기여 SMR이 하나도 없으면 false(호출부에서 span=0으로 기록 — 가짜 숫자 금지).
        /// </summary>
        private static bool MeasureBakeMeshSpanY(SkinnedMeshRenderer[] smrs, out float minY, out float maxY)
        {
            minY = 0f;
            maxY = 0f;
            if (smrs == null || smrs.Length == 0) return false;
            bool any = false;
            for (int i = 0; i < smrs.Length; i++)
            {
                var smr = smrs[i];
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh tmp = new Mesh();
                try { smr.BakeMesh(tmp); }
                catch (System.Exception)
                {
                    Object.DestroyImmediate(tmp);
                    continue;
                }
                Vector3[] verts = tmp.vertices;
                Object.DestroyImmediate(tmp); // 즉시 해제 — EditMode(파일 TearDown과 동일 패턴)
                int n = verts.Length;
                if (n == 0) continue;
                Matrix4x4 l2w = smr.transform.localToWorldMatrix; // BakeMesh 결과는 SMR 로컬 — l2w 근사(패턴 동일)
                for (int v = 0; v < n; v++)
                {
                    Vector3 w = l2w.MultiplyPoint3x4(verts[v]);
                    if (!any) { minY = w.y; maxY = w.y; any = true; }
                    else { if (w.y < minY) minY = w.y; if (w.y > maxY) maxY = w.y; }
                }
            }
            return any;
        }

        [Test]
        public void HeatAvatar_TPoseStretch_Measurement()
        {
            // 1) 프리팹 로드
            var prefab = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
            Assert.That(prefab, Is.Not.Null,
                "[무번호 28-P4b1] heat 프리팹 로드 실패 — 경로 실측 요함");

            // 2) 씬 루트에 Instantiate — TearDown에서 DestroyImmediate
            _spawned = Object.Instantiate(prefab);
            Assert.That(_spawned, Is.Not.Null,
                "[무번호 28-P4b1] heat 프리팹 Instantiate 실패");

            var smrs = _spawned.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(smrs.Length, Is.GreaterThan(0),
                "[무번호 28-P4b1] heat 프리팹에 SkinnedMeshRenderer 없음 — 바인드 스팬 실측 불가");

            // 3) bindSpan = SMR sharedMesh.bounds.size.y 최댓값
            float bindSpan = 0f;
            string bindName = null;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                float y = smr.sharedMesh.bounds.size.y;
                Debug.Log($"[28-P4b1] SMR bind — {smr.name}: sharedMesh.bounds.size.y={y:F3}m");
                if (y > bindSpan) { bindSpan = y; bindName = smr.name; }
            }

            // 4) Instantiate 직후 월드 스팬(아바타 평가 전)
            float worldSpan = MeasureWorldSpan(smrs);
            Debug.Log($"[28-P4b1] Instantiate 직후 worldSpan={worldSpan:F3}m");

            // 5) Animator 존재 — 없으면 실측(아바타 평가) 불가 → Inconclusive
            var animator = _spawned.GetComponentInChildren<Animator>();
            if (animator == null)
                Assert.Inconclusive("[무번호 28-P4b1] Animator 없음 — 아바타 평가 불가, T-포즈 스트레치 실측 불가(가짜 숫자 금지)");
            Debug.Log($"[28-P4b1] Animator={animator.name} avatar={(animator.avatar != null ? animator.avatar.name : "null")}");
            if (animator.avatar != null)
                Debug.Log($"[28-P4b1] avatar.isHuman={animator.avatar.isHuman}");

            animator.enabled = true;
            for (int i = 0; i < 3; i++)
            {
                animator.Update(0f);
                float step = MeasureWorldSpan(smrs);
                Debug.Log($"[28-P4b1] animator.Update(0f) {i + 1}회 후 worldSpan={step:F3}m");
            }

            // 6) tposeSpan = 3회 Update 후 최종 월드 스팬
            float tposeSpan = MeasureWorldSpan(smrs);
            Debug.Log($"[28-P4b1] tposeSpan(3회 Update 후)={tposeSpan:F3}m");

            // 7) ratio = tposeSpan / bindSpan (bindSpan>0 가드)
            if (bindSpan <= 0f)
                Assert.Inconclusive("[무번호 28-P4b1] bindSpan=0 — SMR sharedMesh.bounds.size.y 최대가 0, ratio 산출 불가");
            if (tposeSpan <= 0f)
                Assert.Inconclusive("[무번호 28-P4b1] tposeSpan=0 — 아바타 평가 후 월드 스팬 실측 불가(가짜 숫자 금지)");
            float ratio = tposeSpan / bindSpan;

            // 8) 핵심 증거 로그 1줄(형식 그대로 — QA 파싱 대상)
            string human = (animator != null && animator.avatar != null)
                ? animator.avatar.isHuman.ToString()
                : "noavatar";
            Debug.Log($"[28-P4b1] heat 아바타 실측 — bind={bindSpan:F3}m tpose={tposeSpan:F3}m ratio={ratio:F3} avatarIsHuman={human}");

            // plausibility Assert만(진단 테스트 — 숫자 자체는 계약 아님)
            Assert.That(bindSpan, Is.InRange(0.1f, 10.0f),
                $"[무번호 28-P4b1] bindSpan={bindSpan:F3}m — plausible 범위 0.1~10.0m 이탈(가설: ≈1.16m), 원인 확인 요함");
            Assert.That(tposeSpan, Is.InRange(0.1f, 10.0f),
                $"[무번호 28-P4b1] tposeSpan={tposeSpan:F3}m — plausible 범위 0.1~10.0m 이탈(가설: ≈1.566m), 원인 확인 요함");
            Assert.That(ratio, Is.InRange(0.2f, 5.0f),
                $"[무번호 28-P4b1] ratio={ratio:F3} — plausible 범위 0.2~5.0 이탈(가설: ≈1.35), 원인 확인 요함");
        }

        /// <summary>
        /// [무번호 28-P4b1v2] 클립 재생 시간계열 프로브 — 진단 전용(숫자 계약 아님).
        /// v2 질문: ①클립 재생 중 스트레치가 EditMode에서 재현되는가 ②시간에 따라 변하는가(일시 vs 지속)
        ///          ③HumanBodyBones.Head 매핑이 실제 두상 본과 일치하는가(매핑 오류 의심).
        /// 절차: heat 프리팹 Instantiate + Player_AC 컨트롤러 할당 후 animator.Update(0.016f) 총 120스텝,
        ///       스텝 0/10/30/60/120마다 BakeMesh 기반 메시 스팬(월드 y min/max) + 본 y(Head/Spine/
        ///       LeftFoot/LeftHand 절대 월드 y)와 본 이름(매핑 탐지) 로그 → 최종 요약 로그 1줄.
        /// plausibility Assert만(스팬 0.1~10m, 본 y NaN 아님). 평가 불가 시 Inconclusive+구체 사유
        /// (가짜 숫자 금지). 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브는 배치 실행 금지).
        /// </summary>
        [Test]
        public void HeatAvatar_ClipPlay_TimeSeries_Stretch()
        {
            // 1) heat 프리팹 로드 + Instantiate(기존 1케이스 패턴 재사용 — TearDown 공유)
            var prefab = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
            Assert.That(prefab, Is.Not.Null,
                "[무번호 28-P4b1v2] heat 프리팹 로드 실패 — 경로 실측 요함");

            _spawned = Object.Instantiate(prefab);
            Assert.That(_spawned, Is.Not.Null,
                "[무번호 28-P4b1v2] heat 프리팹 Instantiate 실패");

            var smrs = _spawned.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(smrs.Length, Is.GreaterThan(0),
                "[무번호 28-P4b1v2] heat 프리팹에 SkinnedMeshRenderer 없음 — 메시 스팬 실측 불가");

            // 2) Player_AC 컨트롤러 로드 — 부재 시 Inconclusive+사유(가짜 숫자 금지)
            var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC");
            if (ctrl == null)
                Assert.Inconclusive(
                    "[무번호 28-P4b1v2] Resources/Animation/Controllers/Player_AC 로드 실패 — 클립 재생 실측 불가(가짜 숫자 금지)");

            var animator = _spawned.GetComponentInChildren<Animator>();
            if (animator == null)
                Assert.Inconclusive("[무번호 28-P4b1v2] Animator 없음 — 클립 재생 실측 불가(가짜 숫자 금지)");

            // 3) bindWorld — bind(SMR local sharedMesh.bounds.size.y 최대) × 해당 SMR 계층 스케일
            //    (v1 실측 대조용: bind 0.403m × 계층 스케일 = 월드 1.156m)
            float bindWorld = 0f;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                float worldY = smr.sharedMesh.bounds.size.y * Mathf.Abs(smr.transform.lossyScale.y);
                if (worldY > bindWorld) bindWorld = worldY;
            }

            // 4) 컨트롤러 할당 + 활성화(컨트롤러 원복 불요 — 인스턴스 폐기)
            animator.runtimeAnimatorController = ctrl;
            animator.enabled = true;
            if (animator.runtimeAnimatorController == null)
                Assert.Inconclusive("[무번호 28-P4b1v2] runtimeAnimatorController 할당 실패 — 클립 재생 실측 불가(가짜 숫자 금지)");

            // 5) 시간계열: 스텝 0(컨트롤러 할당 직후·재생 시작 전) 측정 후 총 120스텝 × Update(0.016f)
            //    — 스텝 10/30/60/120마다 측정·로그(누적 시뮬레이션 1.92s)
            float spanFirst = float.NaN;
            float spanLast = float.NaN;
            float maxSpan = float.MinValue;
            float minSpan = float.MaxValue;
            string headName = "unmapped";
            string spineName = "unmapped";
            string footName = "unmapped";
            string handName = "unmapped";
            bool anyNan = false;
            string nanDetail = "없음";

            for (int step = 0; step <= 120; step++)
            {
                if (step > 0) animator.Update(0.016f);
                bool isMeasureStep = (step == 0 || step == 10 || step == 30 || step == 60 || step == 120);
                if (!isMeasureStep) continue;

                // 메시 스팬(핵심) — BakeMesh→verts→l2w 변환(ArmorVisual PlayerBodyMeasure 패턴 동일)
                float meshMinY = 0f;
                float meshMaxY = 0f;
                bool hasMesh = MeasureBakeMeshSpanY(smrs, out meshMinY, out meshMaxY);
                float span = hasMesh ? Mathf.Max(0f, meshMaxY - meshMinY) : 0f;

                // 본 y(절대 월드) + 본 이름(GetBoneTransform — 매핑 오류 탐지 핵심)
                Transform headT = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform spineT = animator.GetBoneTransform(HumanBodyBones.Spine);
                Transform footT = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform handT = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                headName = headT != null ? headT.name : "unmapped";
                spineName = spineT != null ? spineT.name : "unmapped";
                footName = footT != null ? footT.name : "unmapped";
                handName = handT != null ? handT.name : "unmapped";
                float headY = headT != null ? headT.position.y : float.NaN;
                float spineY = spineT != null ? spineT.position.y : float.NaN;
                float footY = footT != null ? footT.position.y : float.NaN;
                float handY = handT != null ? handT.position.y : float.NaN;
                float rootY = _spawned.transform.position.y;

                // 핵심 증거 로그(파싱 계약 — 형식 그대로)
                Debug.Log($"[28-P4b1v2] t={step:D3} mesh=[{meshMinY:F2}~{meshMaxY:F2}] span={span:F3}m head={headName}@{headY:F2} spine={spineName}@{spineY:F2} foot={footName}@{footY:F2} hand={handName}@{handY:F2} root={rootY:F2}");

                // 시계열 통계 누적(요약+plausibility 재료)
                if (float.IsNaN(spanFirst)) spanFirst = span;
                spanLast = span;
                if (span > maxSpan) maxSpan = span;
                if (span < minSpan) minSpan = span;
                if (!anyNan && (float.IsNaN(headY) || float.IsNaN(spineY) || float.IsNaN(footY) || float.IsNaN(handY)))
                {
                    anyNan = true;
                    nanDetail = "t=" + step.ToString("D3") + " head=" + headName + " spine=" + spineName + " foot=" + footName + " hand=" + handName;
                }
            }

            // 6) 최종 요약 로그 1줄(형식 그대로 — QA 파싱 대상)
            Debug.Log($"[28-P4b1v2] 요약 — spanFirst={spanFirst:F3}m spanLast={spanLast:F3}m maxSpan={maxSpan:F3}m bindWorld={bindWorld:F3}m headMap={headName}");

            // 7) plausibility Assert만(진단 테스트 — 숫자 자체는 계약 아님)
            Assert.That(minSpan, Is.InRange(0.1f, 10.0f),
                $"[무번호 28-P4b1v2] 시간계열 스팬 최소={minSpan:F3}m — plausible 범위 0.1~10.0m 이탈(원인 확인 요함)");
            Assert.That(maxSpan, Is.InRange(0.1f, 10.0f),
                $"[무번호 28-P4b1v2] 시간계열 스팬 최대={maxSpan:F3}m — plausible 범위 0.1~10.0m 이탈(원인 확인 요함)");
            Assert.That(anyNan, Is.False,
                $"[무번호 28-P4b1v2] 본 y NaN 발생 — HumanBodyBones 매핑 오류 의심: {nanDetail}");
        }
    }
}
