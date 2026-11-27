using System;
using System.Reflection;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [143차 P7] 동굴 다층 레벨 디렉터 — 1~5층 수직 진행 총괄.
    /// 탑다운 수직 적층 금지 계약 → 층 전환은 "현재 룸 오브젝트 파괴 → 새 룸 리빌드 → 스폰 텔레포트" 방식.
    /// 씬에는 항상 CaveRoom 1개만 존재하며, 층은 오브젝트 적층이 아니라 리빌드로 표현된다.
    /// UI 어셈블리 경계(IndoorSceneTransition/UTKToastService/CaveEntrance)는 ArenaBettingSystem 선례의
    /// FindCrossAssemblyType 리플렉션 경유 — Systems asmdef에 UI 레퍼런스가 없어 직접 참조 불가(실측).
    /// </summary>
    public static class CaveLevelDirector
    {
        public const int MinFloor = 1;
        public const int MaxFloor = 5;

        public static int CurrentFloor { get; private set; } = 1;
        private static string _territoryId = "default";
        private static int _tier = 1;

        /// <summary>
        /// [146차 P1] 티어 컨텍스트 동기화 — 부트(Test_21 CaveInteriorTestSetup)가 2인자
        /// BuildCaveInterior(territoryId, tier)를 직접 호출하면 Director의 _territoryId="default"/_tier=1
        /// 디폴트가 그대로 남아 첫 GoDeeper가 tier 1(스케일 1.0)로 리빌드하는 결함 수리.
        /// [146차 P0 실측] 2층 CaveFloor 21.6×7.0m(원본) vs 1층 ×1.25 — 부트 경유 누락 수리.
        /// 멱등 — 동일 인자 반복 호출에 상태 불변(빌더 Core가 매 빌드마다 재동기화).
        /// </summary>
        internal static void SyncContext(string territoryId, int tier, int floor)
        {
            _territoryId = string.IsNullOrEmpty(territoryId) ? "default" : territoryId;
            _tier = tier;
            CurrentFloor = Mathf.Clamp(floor, MinFloor, MaxFloor);
        }

        /// <summary>층 빌드 — floor는 1 기반(1=입구층). 빌더 계약상 floorIndex는 0 기반이므로 −1 변환.</summary>
        public static GameObject BuildFloor(string territoryId, int tier, int floor)
        {
            _territoryId = string.IsNullOrEmpty(territoryId) ? "default" : territoryId;
            _tier = tier;
            CurrentFloor = Mathf.Clamp(floor, MinFloor, MaxFloor);
            var room = CaveInteriorBuilder.BuildCaveInterior(_territoryId, _tier, CurrentFloor - 1);
            // [146차 P2] 실내 미니맵 캡처 훅 — 층 리빌드 완료(반환 직전). Systems→UI 직접 참조 금지 계약
            //   (Systems asmdef에 UI 레퍼런스 부재 — 클래스 헤더 실측) → ReturnToSurface/ShowFloorToast와
            //   동일한 FindCrossAssemblyType 리플렉션 경유. 실패 시 로그 1행+무음(크래시 금지).
            CaptureMinimapForRoom(room);
            return room;
        }

        /// <summary>하강 — 현재 룸 파괴 → 다음 층 리빌드 → 스폰 텔레포트 → 토스트. MaxFloor면 무시+로그.</summary>
        public static GameObject GoDeeper(GameObject currentRoom)
        {
            if (CurrentFloor >= MaxFloor)
            {
                Debug.Log($"[CaveLevel] 이미 최심부({MaxFloor}층) — 하강 무시");
                return currentRoom;
            }
            if (currentRoom != null) UnityEngine.Object.Destroy(currentRoom);
            CurrentFloor++;
            var room = BuildFloor(_territoryId, _tier, CurrentFloor);
            TeleportPlayerToFloorSpawn();
            ShowFloorToast(CurrentFloor);
            return room;
        }

        /// <summary>상승 — 1층이면 지상 귀환 경로, 아니면 위 층 리빌드+텔레포트+토스트.</summary>
        public static GameObject GoUp(GameObject currentRoom)
        {
            if (CurrentFloor <= MinFloor)
            {
                ReturnToSurface();
                return currentRoom;
            }
            if (currentRoom != null) UnityEngine.Object.Destroy(currentRoom);
            CurrentFloor--;
            var room = BuildFloor(_territoryId, _tier, CurrentFloor);
            TeleportPlayerToFloorSpawn();
            ShowFloorToast(CurrentFloor);
            return room;
        }

        /// <summary>[143차 P7] 지상 귀환 — UI 어셈블리 경계면 FindCrossAssemblyType 경유.
        /// 실측 진입점: ProjectName.UI.IndoorSceneTransition.ExitBuilding() public static 무인자(L647).
        /// 미발견 시 CaveEntrance.SavedPlayerPosition 복원 폴백(리플렉션 — 로그 명시).</summary>
        private static void ReturnToSurface()
        {
            Type t = FindCrossAssemblyType("ProjectName.UI.IndoorSceneTransition");
            var exit = t?.GetMethod("ExitBuilding", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (exit != null)
            {
                exit.Invoke(null, null);
                return;
            }
            Debug.LogWarning("[CaveLevel] IndoorSceneTransition.ExitBuilding 미발견 — CaveEntrance.SavedPlayerPosition 복원 폴백");
            RestoreSavedPositionFallback();
        }

        /// <summary>폴백 — CaveEntrance.SavedPlayerPosition/HasSavedPosition 리플렉션 복원(실패 시 로그뿐, 크래시 금지).</summary>
        private static void RestoreSavedPositionFallback()
        {
            try
            {
                Type t = FindCrossAssemblyType("ProjectName.UI.CaveEntrance");
                var hasPos = t?.GetProperty("HasSavedPosition", BindingFlags.Public | BindingFlags.Static);
                var savedPos = t?.GetProperty("SavedPlayerPosition", BindingFlags.Public | BindingFlags.Static);
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null || hasPos == null || savedPos == null) return;
                if (!(bool)hasPos.GetValue(null)) return;
                player.transform.position = (Vector3)savedPos.GetValue(null);
                Debug.Log("[CaveLevel] 지상 복귀 — CaveEntrance.SavedPlayerPosition 복원 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CaveLevel] 지상 복귀 폴백 실패(무시): {e.Message}");
            }
        }

        /// <summary>[156차 P2-4] private→public 승격 — CaveFallGuard 낙하 복귀 재사용(GoDeeper/GoUp과 동일 경로 단일화). 본체 로직 0변경.</summary>
        public static void TeleportPlayerToFloorSpawn()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[CaveLevel] Player 태그 오브젝트 미발견 — 텔레포트 스킵");
                return;
            }
            // [146차 P1] _tier 동행 — 1인자 오버로드는 tier=1 고정 위임이라 tierScale 스케일 스폰 누락.
            player.transform.position = CaveInteriorBuilder.GetFloorSpawnPosition(CurrentFloor, _tier);
        }

        /// <summary>층 진입 토스트 — ProjectName.UI.Toolkit.UTKToastService.Show 리플렉션.
        /// 실측 시그니처: Show(string) 및 Show(string, float) 오버로드(UTKControls.cs L207, 호출부 실측).
        /// 실패 시 Debug.Log("[CaveLevel] 지하 N층 진입") 폴백 — 크래시 금지.</summary>
        private static void ShowFloorToast(int floor)
        {
            string text = floor <= MinFloor ? $"지하 {floor}층 — 입구" : $"지하 {floor}층 진입";
            try
            {
                Type t = FindCrossAssemblyType("ProjectName.UI.Toolkit.UTKToastService");
                var m = t?.GetMethod("Show", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(float) }, null);
                if (m != null)
                {
                    m.Invoke(null, new object[] { text, 2.5f });
                    return;
                }
                m = t?.GetMethod("Show", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string) }, null);
                if (m != null)
                {
                    m.Invoke(null, new object[] { text });
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CaveLevel] 토스트 리플렉션 실패 — 로그 폴백: {e.Message}");
            }
            Debug.Log($"[CaveLevel] {text}");
        }

        /// <summary>ArenaBettingSystem 선례 복제 — 어셈블리 탐색형 타입 조회(이름 불변).</summary>
        private static Type FindCrossAssemblyType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>[146차 P2] 실내 미니맵 캡처 위임 — ProjectName.UI.Toolkit.IndoorMinimapBaker.CaptureFor(GameObject)
        ///   리플렉션 호출(ArenaBettingSystem/UTKToastService 선례 동일 어셈블리 경계면). 대상 시그니처 실측:
        ///   public static void CaptureFor(GameObject room). 실패 무음 — 미니맵 부재가 게임플레이를 막지 않는다.</summary>
        private static void CaptureMinimapForRoom(GameObject room)
        {
            try
            {
                Type t = FindCrossAssemblyType("ProjectName.UI.Toolkit.IndoorMinimapBaker");
                var m = t?.GetMethod("CaptureFor", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null);
                m?.Invoke(null, new object[] { room });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CaveLevel] 실내 미니맵 캡처 위임 실패(무시): {e.Message}");
            }
        }
    }
}
