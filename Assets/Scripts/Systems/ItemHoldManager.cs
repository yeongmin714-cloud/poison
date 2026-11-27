using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [68차 P3] 핫바 숫자키로 비무기 아이템을 플레이어 오른손에 "들기" — 정적 관리자.
    /// 모델은 RuntimeModelLoader.TryGetModel(GLB 프리팹) 경유. WeaponEquipManager와 상호 배타:
    ///   - Hold 시작 시 무기 장착 중이면 WeaponEquipManager.Unequip 먼저
    ///   - WeaponEquipManager.Equip 시작 시 홀드 중이면 ItemHoldManager.UnholdIfHolding 먼저
    /// Unhold 경로는 Equip을 호출하지 않는다 — 상호 정리 무한루프 차단 계약(EditMode 테스트 검증).
    /// </summary>
    public static class ItemHoldManager
    {
        /// <summary>현재 홀드 중인 모델키. null = 홀드 없음.</summary>
        public static string HeldModelKey { get; private set; }

        /// <summary>아이템 홀드 중 여부 (모델 인스턴스 생존 포함 판정).</summary>
        public static bool IsHolding => _held != null;

        /// <summary>손에 들고 있는 GLB 인스턴스 (null = 없음).</summary>
        private static GameObject _held;

        // ── [165b차] per-key 홀드 구성 ──
        // 핫바 기본 규약(0.22m 캡 + (0,0.02,0) 오프셋)의 오버라이드 — 장축/거치대 부착 GLB 전용.
        // 기존 로직 개조 없음: Hold() 내 분기만 추가 (WeaponEquipManager _gripTable 선례 미러링).
        public struct HoldConfig
        {
            public float MaxDim;            // bounds 최대변 목표. 0 = 기본 0.22 규약
            public string[] HideNodeNames;  // Instantiate 후 SetActive(false)할 자식 노드명
            public Vector3 LocalEuler;      // 손 본 기준 초기 회전 (Play 실측 트림 대상)
            public bool GripAlign;          // true = 최장축 그립 끝(bounds min측)을 손 원점에 정렬
        }

        static readonly Dictionary<string, HoldConfig> _holdConfigs = new()
        {
            // [165b차] propped 사선(26.6°) 베이크 보정: Z=27도로 평탄화 후 손 본 위임 — 각도는 Play 실측 트림.
            { "fishing_rod", new HoldConfig { MaxDim = 1.0f, HideNodeNames = new[]{ "stick" }, LocalEuler = new Vector3(0f, 0f, 27f), GripAlign = true } },
            // [210차] 모루 아이콘 밑동 제외 — 사용자 지시: 아이콘 렌더에서 stump 노드 은닉(GLB 원형 유지, 월드 무영향).
            //   anvil-on-stump → stump(mesh0) + anvil-iron(mesh1) 중 stump만 은닉. HoldConfig 나머지 기본값(MaxDim=0
            //   → 0.22 기본 규약, LocalEuler=zero, GripAlign=false)이라 Hold() 동작은 미등록과 동일 무효이며,
            //   모루는 시설 배치물(FacilityPropBuilder/InteriorFurnishingPass 배선)로 손 홀드 경로가 없어 월드 무영향.
            { "anvil-1286cb", new HoldConfig { HideNodeNames = new[]{ "stump" } } },
        };

        /// <summary>[165b차] per-key 홀드 구성 조회 — 미등록 키는 null(기본 0.22 규약 유지).</summary>
        public static HoldConfig? GetHoldConfig(string modelKey)
        {
            return modelKey != null && _holdConfigs.TryGetValue(modelKey, out var cfg) ? cfg : (HoldConfig?)null;
        }

        /// <summary>[165b차] per-key 은닉 노드명 조회 — GblItemIconRenderer용. 미등록 키는 null.</summary>
        public static string[] GetHideNodeNames(string modelKey)
        {
            return modelKey != null && _holdConfigs.TryGetValue(modelKey, out var cfg) ? cfg.HideNodeNames : null;
        }

        /// <summary>[165b차] 은닉 노드 탐색 — 직계 Find 우선, 미발견 시 이름 재귀 검색
        /// (GLB 중첩 구조 실측: propped-fishing-rod → rod → stick — 루트 직계 Find("stick")은 실패).</summary>
        public static Transform FindHideNode(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null) return t;
            foreach (Transform child in root)
            {
                var found = FindHideNode(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 아이템 홀드 — modelKey GLB를 플레이어 RightHand에 부착.
        /// bounds 최대변 0.22m 스케일 보정 + 손 오프셋 (0, 0.02, 0).
        /// 모델 로드 실패 시 경고 로그 + no-op(예외 금지 — 핫바 입력 흐름 보호).
        /// </summary>
        public static void Hold(string modelKey, Transform player)
        {
            if (string.IsNullOrEmpty(modelKey))
            {
                Debug.LogWarning("[ItemHoldManager] modelKey 비어 있음 — 홀드 스킵");
                return;
            }
            if (player == null)
            {
                // InventoryWindowUTK 1179행 선례 — FindWithTag 폴백
                var found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
            }
            if (player == null)
            {
                Debug.LogWarning("[ItemHoldManager] player 미발견 — 홀드 스킵");
                return;
            }

            // [68차] 무기 장착 중이면 먼저 해제 — 손에 무기+아이템 동시 부착 방지.
            //   Unequip은 Equip을 호출하지 않아 무한루프 없음.
            if (WeaponEquipManager.IsEquipped) WeaponEquipManager.Unequip();

            UnholdInternal();   // 기존 홀드 정리 (교체)

            if (!RuntimeModelLoader.TryGetModel(modelKey, out var model))
            {
                Debug.LogWarning($"[ItemHoldManager] 모델 없음: {modelKey} — 홀드 no-op (예외 아님)");
                return;
            }

            var held = Object.Instantiate(model);
            held.name = modelKey;

            // [165b차] per-key 은닉 노드 — 거치대(stick) 등 부착 불요 부분 SetActive(false).
            //   파괴 금지 — EditMode 안전 + GblItemIconRenderer 아이콘 경로 재사용.
            var cfg = GetHoldConfig(modelKey);
            foreach (var n in cfg?.HideNodeNames ?? System.Array.Empty<string>())
                FindHideNode(held.transform, n)?.gameObject.SetActive(false);

            var animator = player.GetComponentInChildren<Animator>();
            var handBone = WeaponEquipManager.GetHandBone(animator, bowLeft: false);
            if (handBone == null)
            {
                Debug.LogWarning("[ItemHoldManager] RightHand 본 미발견 — 홀드 스킵");
                Object.Destroy(held);
                return;
            }
            held.transform.SetParent(handBone, false);

            // [68차] 낙하/물리 간섭 차단 — Rigidbody 제거 + Collider 비활성
            // (WeaponEquipManager 219-228행 GLB 낙함 known-trap 선례 동일)
            var rbs = held.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                rbs[i].isKinematic = true;
                rbs[i].useGravity = false;
                Object.Destroy(rbs[i]);
            }
            var cols = held.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            // [68차] bounds 최대변 0.22m 스케일 보정 — 손에 들기 적정 크기(무기 스케일과 무관 규약).
            //   렌더러 shadow는 기본 유지(별도 조작 없음 — 그림자 보임).
            var rends = held.GetComponentsInChildren<Renderer>(true);
            float maxDim = 0f;
            if (rends.Length > 0)
            {
                var bounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maxDim > 0.001f)
                {
                    // [165b차] per-key MaxDim 오버라이드 — 낚싯대(2.227m)를 0.22m 캡으로 축소하면 장난감 막대가 됨.
                    float cap = cfg?.MaxDim > 0f ? cfg.Value.MaxDim : 0.22f;
                    float scale = Mathf.Min(1f, cap / maxDim);
                    held.transform.localScale = held.transform.localScale * scale;
                }
            }

            // [165b차] per-key 회전 — propped 사선(26.6°) 베이크 보정: Z=27°로 평탄화 후 손 본 위임.
            if (cfg != null && cfg.Value.LocalEuler != Vector3.zero)
                held.transform.localRotation = Quaternion.Euler(cfg.Value.LocalEuler);

            // [165b차] bounds 그립 정렬 — WeaponEquipManager "GLB bounds 자동 그립 정렬" 선례 미러링:
            //   렌더러 bounds 합산 → 최장축 = 그립축, 그립 끝(최소측 면 중심)이 손 원점에 착지하도록
            //   pivot-to-grip 오프셋을 localPosition에서 차감(손 본 로컬 공간 환산). 기본 (0,0.02,0) 위에 적용.
            var basePos = new Vector3(0f, 0.02f, 0f);   // 손 아래 살짝 띄움
            if (cfg != null && cfg.Value.GripAlign && rends.Length > 0)
            {
                var gb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) gb.Encapsulate(rends[i].bounds);
                int axis = 0;
                float len = gb.size.x;
                if (gb.size.y > len) { axis = 1; len = gb.size.y; }
                if (gb.size.z > len) { axis = 2; len = gb.size.z; }
                if (len > 0.001f)
                {
                    Vector3 gripEnd = gb.center;
                    if (axis == 0) gripEnd.x = gb.min.x;
                    else if (axis == 1) gripEnd.y = gb.min.y;
                    else gripEnd.z = gb.min.z;
                    Vector3 pivot = held.transform.position;
                    Vector3 offset = handBone.InverseTransformPoint(gripEnd) - handBone.InverseTransformPoint(pivot);
                    held.transform.localPosition = basePos - offset;
                    Debug.Log($"[ItemHoldManager] 그립 정렬: {modelKey} axis={(axis == 0 ? "X" : axis == 1 ? "Y" : "Z")} len={len:F3} gripEnd={gripEnd:F3} offset={offset:F3} → localPos={held.transform.localPosition:F3}");
                }
                else
                {
                    held.transform.localPosition = basePos;
                }
            }
            else
            {
                held.transform.localPosition = basePos;
            }

            _held = held;
            HeldModelKey = modelKey;
            Debug.Log($"[ItemHoldManager] ✅ 아이템 홀드: {modelKey} → RightHand({handBone.name}) maxDim={maxDim:F3}");
        }

        /// <summary>홀드 해제 (사용자 토글 경유). 홀드 없음 시 조용히 통과.</summary>
        public static void Unhold()
        {
            if (_held == null && HeldModelKey == null) return;
            UnholdInternal();
            Debug.Log("[ItemHoldManager] 🔓 홀드 해제");
        }

        /// <summary>[68차] WeaponEquipManager.Equip 시작 정리용 — 홀드 중일 때만 해제.
        /// Equip을 호출하지 않으므로 Hold↔Equip 상호 정리 무한루프 없음.</summary>
        public static void UnholdIfHolding()
        {
            if (_held == null && HeldModelKey == null) return;
            UnholdInternal();
            Debug.Log("[ItemHoldManager] 무기 장착으로 홀드 정리");
        }

        /// <summary>내부 해제 경로 — Hold 교체 경로(log 없음)와 토글 경로 공유.
        /// [EditMode 안전] 파괴는 실행 모드 전용 Destroy / 편집 모드 DestroyImmediate
        /// (SelectionRingController 파괴 게이트 선례).</summary>
        private static void UnholdInternal()
        {
            if (_held != null)
            {
                if (Application.isPlaying) Object.Destroy(_held);
                else Object.DestroyImmediate(_held);
            }
            _held = null;
            HeldModelKey = null;
        }
    }
}
