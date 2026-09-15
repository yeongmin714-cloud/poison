using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 성(실내)씬에서 플레이어 소유 영지의 병사를 '공격/수비/해제'로 배치하는 간단 IMGUI 패널.
    /// TerritoryDeploymentSystem을 호출하는 진입점이며, 별도 클래스로 분리되어 있다.
    /// - _uiScale = sqrt((W/1920)*(H/1080)) + UIFont.Load() 기존 UI 패턴 사용.
    /// - GUI.skin 직접 수정 금지 — 로컬 GUIStyle에만 폰트를 지정한다.
    /// 사용법: 실내씬의 임의 GameObject에 TerritoryDeploymentUI를 부착하면 패널이 표시된다.
    /// </summary>
    public class TerritoryDeploymentUI : MonoBehaviour
    {
        private bool _showPanel = true;

        // 공격 대상 선택 상태
        private bool _pickingAttack = false;
        private TerritoryId _pickSourceId;

        private float _uiScale = 1f;
        private Font _cachedFont;

        private void OnGUI()
        {
            if (!_showPanel) return;

            // 해상도 비례 스케일 (기존 UI 산식 재사용)
            _uiScale = Mathf.Max(0.35f, Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f)));
            if (_cachedFont == null) _cachedFont = UIFont.Load();

            // 로컬 스타일 (GUI.skin 수정 금지 → 인스턴스 스타일에만 폰트 지정)
            var labelStyle = new GUIStyle(GUI.skin.label) { font = _cachedFont, fontSize = Mathf.RoundToInt(13 * _uiScale) };
            var boxStyle = new GUIStyle(GUI.skin.box) { font = _cachedFont, fontSize = Mathf.RoundToInt(13 * _uiScale) };
            var btnStyle = new GUIStyle(GUI.skin.button) { font = _cachedFont, fontSize = Mathf.RoundToInt(13 * _uiScale) };

            GUILayout.BeginArea(new Rect(12f * _uiScale, 12f * _uiScale, 360f * _uiScale, 520f * _uiScale), boxStyle);
            GUILayout.Label("⚔️ 영지 병사 배치", labelStyle);

            if (GuardManager.Instance == null || TerritoryDatabase.Instance == null)
            {
                GUILayout.Label("(GuardManager / TerritoryDatabase 없음)", labelStyle);
                GUILayout.EndArea();
                return;
            }

            // 플레이어 소유 영지 목록
            var playerTerritories = GetPlayerTerritories();
            if (playerTerritories.Count == 0)
            {
                GUILayout.Label("점령한 영지가 없습니다.", labelStyle);
            }
            else
            {
                GUILayout.Label($"점령 영지 {playerTerritories.Count}곳", labelStyle);

                foreach (var def in playerTerritories)
                {
                    TerritoryState st = TerritoryDatabase.Instance.GetState(def.id);
                    GUILayout.Space(4f * _uiScale);
                    GUILayout.BeginHorizontal();
                    int liveCount = GuardManager.Instance.GetGuardsInTerritory(def.id).Count;
                    int count = liveCount > 0 ? liveCount : def.guardCount;
                    string roleText = st != null ? st.garrisonRole.ToString() : "None";
                    GUILayout.Label($"◆ {def.territoryName} [병사 {count} · {roleText}]", labelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("🛡️ 수비", btnStyle))
                        TerritoryDeploymentSystem.DeployDefense(def.id);
                    if (GUILayout.Button("⏹️ 해제", btnStyle))
                        TerritoryDeploymentSystem.Undeploy(def.id);
                    if (GUILayout.Button("⚔️ 공격", btnStyle))
                    {
                        _pickingAttack = true;
                        _pickSourceId = def.id;
                    }
                    GUILayout.EndHorizontal();

                    // 공격 대상 선택 (선택 시에만 표시)
                    if (_pickingAttack && _pickSourceId.Equals(def.id))
                    {
                        foreach (var targetDef in GetAttackTargets(def.id))
                        {
                            if (GUILayout.Button($"→ {targetDef.territoryName} ({targetDef.id})", btnStyle))
                            {
                                TerritoryDeploymentSystem.DeployAttack(def.id, targetDef.id);
                                _pickingAttack = false;
                            }
                        }
                        if (GUILayout.Button("대상 선택 취소", btnStyle))
                            _pickingAttack = false;
                    }
                }
            }

            GUILayout.Space(8f * _uiScale);
            if (GUILayout.Button("패널 닫기", btnStyle))
                _showPanel = false;
            GUILayout.EndArea();
        }

        /// <summary>플레이어가 소유한 영지 정의 목록</summary>
        private List<TerritoryDefinition> GetPlayerTerritories()
        {
            var db = TerritoryDatabase.Instance;
            var result = new List<TerritoryDefinition>();
            foreach (var def in db.GetAllDefinitions())
            {
                var st = db.GetState(def.id);
                if (st != null && st.ownership == TerritoryOwnership.PlayerOwned)
                    result.Add(def);
            }
            return result;
        }

        /// <summary>공격 대상 후보 (자기 자신 제외 — 아직 점령 안 된/타 소유 영지 포함)</summary>
        private List<TerritoryDefinition> GetAttackTargets(TerritoryId sourceId)
        {
            var db = TerritoryDatabase.Instance;
            var result = new List<TerritoryDefinition>();
            foreach (var def in db.GetAllDefinitions())
            {
                if (def.id.Equals(sourceId)) continue;
                result.Add(def);
            }
            // 최대 8개만 표시해 패널 오버플로 방지
            return result.Count > 8 ? result.GetRange(0, 8) : result;
        }
    }
}