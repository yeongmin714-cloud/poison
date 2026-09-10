using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 농경 매니저 싱글턴 — 농장 경작지(FarmPlot)의 생성/조립만 전담.
    /// 성장/파종/수확 로직은 각 FarmPlot이 자체 Update에서 처리한다(매니저는 관여하지 않음).
    ///
    /// 사용법:
    ///   if (FarmingManager.Instance == null)
    ///       new GameObject("FarmingManager").AddComponent<FarmingManager>();
    ///   FarmingManager.Instance.SpawnPlots(NationType.East, 1, center, rows:2, cols:2,
    ///       spacing:2.5f, HerbPickup.HerbType.Red, 2);
    /// </summary>
    public class FarmingManager : MonoBehaviour
    {
        public static FarmingManager Instance { get; private set; }

        private readonly List<FarmPlot> _plots = new List<FarmPlot>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);   // 중복 인스턴스 제거
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 주어진 center 주변에 rows×cols 격자(격자 중심이 center와 정렬)로 FarmPlot을 생성하고
        /// Configure(소속 영지 + 작물 + 성장 일수)까지 호출한다.
        /// 각 플롯 이름은 "FarmPlot_r_c". y는 center.y를 따르되, FarmPlot.Awake에서
        /// SurfaceY(x,z)+0.1 계약으로 자체 보정한다.
        /// </summary>
        public List<FarmPlot> SpawnPlots(NationType nation, int index, Vector3 center,
            int rows, int cols, float spacing, HerbPickup.HerbType crop, int growDays)
        {
            var spawned = new List<FarmPlot>();

            if (rows < 1) rows = 1;
            if (cols < 1) cols = 1;
            if (spacing <= 0f) spacing = 2.5f;

            float startOffsetX = -(cols - 1) * spacing * 0.5f;   // 격자 중심 정렬
            float startOffsetZ = -(rows - 1) * spacing * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = center.x + startOffsetX + c * spacing;
                    float z = center.z + startOffsetZ + r * spacing;

                    var go = new GameObject($"FarmPlot_{r}_{c}");
                    go.transform.position = new Vector3(x, center.y, z);

                    var plot = go.AddComponent<FarmPlot>();
                    plot.Configure(nation, index, crop, growDays);

                    _plots.Add(plot);
                    spawned.Add(plot);
                }
            }

            Debug.Log($"[FarmingManager] ✅ 경작지 {spawned.Count}칸 생성 " +
                      $"(영지 {nation}_{index:D2}, 격자 {rows}x{cols}, 간격 {spacing:F1}m, 작물 {crop}, 성장 {growDays}일)");

            return spawned;
        }

        /// <summary>현재 관리 중인 모든 FarmPlot 목록(복사본).</summary>
        public List<FarmPlot> GetAllPlots() => new List<FarmPlot>(_plots);

        /// <summary>관리 중인 경작지 수.</summary>
        public int Count => _plots.Count;

        /// <summary>모든 경작지를 파괴하고 목록을 비운다.</summary>
        public void ClearAll()
        {
            foreach (var plot in _plots)
            {
                if (plot != null)
                    Destroy(plot.gameObject);
            }
            _plots.Clear();
            Debug.Log("[FarmingManager] 🧹 모든 경작지 제거 완료");
        }
    }
}