using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
	/// <summary>
	/// P25-02: 용병 매니저 싱글톤.
	/// 고용/해고/목록 관리를 담당합니다.
	/// </summary>
	public class MercenaryManager : MonoBehaviour
	{
		public static MercenaryManager Instance { get; private set; }

		// 고용된 용병 목록 (인원 상한 없음 — 고용 가능 수는 카탈로그의 서로 다른 용병 수로 제한됨)
		private List<MercenaryInstance> _hiredMercenaries = new List<MercenaryInstance>();

		// 기본 용병 데이터베이스
		private Dictionary<string, MercenaryData> _mercenaryDatabase = new Dictionary<string, MercenaryData>();

		// 호감도 데이터: 용병 ID → 호감도 (0~100)  (단일 진실 공급원)
		private Dictionary<string, float> _affinity = new Dictionary<string, float>();

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);

			EnsureDatabase();
		}

		/// <summary>[O7] DB 초기화 멱등 훅 — Awake 미실행 환경(에디터 테스트 등) 대응. HireMercenary 등 진입점에서 호출.</summary>
		public void EnsureDatabase()
		{
			if (_databaseInitialized) return;
			InitializeDatabase();
			_databaseInitialized = true;
		}

		private bool _databaseInitialized;

		private void InitializeDatabase()
		{
			// ===== ★ 일반 용병 =====
			RegisterMercenary(new MercenaryData(
				"merc_soldier_01", "카림", MercenaryGrade.Normal,
				80f, 12f, 8f, 4f, 100,
				"기본 전투 기술", "가난한 농민 출신으로 전장에서 살아남기 위해 검을 잡았다.",
				"Soldier"
			));
			RegisterMercenary(new MercenaryData(
				"merc_soldier_02", "엘라", MercenaryGrade.Normal,
				75f, 14f, 6f, 5f, 100,
				"민첩한 움직임", "도시 빈민가에서 자란 그녀는 작고 빠른 몸놀림이 특기다.",
				"Soldier"
			));

			// ===== ★★ 고급 용병 =====
			RegisterMercenary(new MercenaryData(
				"merc_high_01", "그리프", MercenaryGrade.High,
				150f, 22f, 15f, 5f, 400,
				"강타 — 20% 확률로 2배 데미지", "전직 황제국 기병대 출신. 사령관과의 불화로 용병이 되었다.",
				"Soldier"
			));
			RegisterMercenary(new MercenaryData(
				"merc_high_02", "세라프", MercenaryGrade.High,
				130f, 20f, 18f, 4.5f, 400,
				"철벽 방어 — 방어력 30% 증가", "바위산 부족의 전사. 방패 하나면 수십 명을 막아낸다.",
				"Soldier"
			));

			// ===== ★★★ 정예 용병 =====
			RegisterMercenary(new MercenaryData(
				"merc_elite_01", "발터", MercenaryGrade.Elite,
				250f, 35f, 25f, 6f, 800,
				"전장의 지휘관 — 주변 아군 공격력 10% 증가", "국경 분쟁에서 100전 100승을 기록한 전설적인 용병대장.",
				"Soldier"
			));
			RegisterMercenary(new MercenaryData(
				"merc_elite_02", "흑표범", MercenaryGrade.Elite,
				220f, 40f, 20f, 7f, 800,
				"암습 — 전투 시작 시 3배 데미지 일격", "그의 정체를 아는 자는 아무도 없다. 그림자처럼 적을 베어넘긴다.",
				"Soldier"
			));

			// ===== ★★★★ 전설 용병 =====
			RegisterMercenary(new MercenaryData(
				"merc_legend_01", "아라곤 왕자", MercenaryGrade.Legendary,
				500f, 60f, 40f, 8f, 2000,
				"왕자의 검 — 모든 능력치 50% 증가 (전투 중)", "멸망한 왕국의 마지막 왕자. 복수를 위해 용병이 되었다.",
				"Soldier"
			));
			RegisterMercenary(new MercenaryData(
				"merc_legend_02", "얼음 마녀", MercenaryGrade.Legendary,
				400f, 70f, 30f, 6f, 2000,
				"얼음 폭풍 — 적군 전체 3초 행동 불가", "북부 설원에서 온 마녀. 그녀의 마법 앞에 적은 얼음덩어리가 된다.",
				"Soldier"
			));

			// ===== ★★★ 바드 (특수) =====
			RegisterMercenary(new MercenaryData(
				"merc_bard_01", "루트 연주자 리리엔", MercenaryGrade.Elite,
				100f, 0f, 10f, 5f, 800,
				"전투의 노래 — 반경 15m 아군 공+15% 방+10% 이속+10%", "황실 음악원 출신의 천재 바드. 그녀의 음악은 병사들을 광전사로 만든다.",
				"Bard"
			));
		}

		private void RegisterMercenary(MercenaryData data)
		{
			if (!_mercenaryDatabase.ContainsKey(data.id))
			{
				_mercenaryDatabase[data.id] = data;
			}
		}

		/// <summary>모든 용병 데이터 반환</summary>
		public MercenaryData[] GetAllMercenaryData()
		{
			var list = new List<MercenaryData>(_mercenaryDatabase.Values);
			return list.ToArray();
		}

		/// <summary>ID로 용병 데이터 조회 (없으면 default 반환, id == null 로 미발견 확인)</summary>
		public MercenaryData GetMercenaryData(string id)
		{
			if (_mercenaryDatabase.TryGetValue(id, out var data))
				return data;
			return default;
		}

		/// <summary>ID로 용병 데이터 조회 (안전 패턴)</summary>
		public bool TryGetMercenaryData(string id, out MercenaryData data)
		{
			return _mercenaryDatabase.TryGetValue(id, out data);
		}

		/// <summary>특정 등급의 용병만 반환</summary>
		public MercenaryData[] GetMercenariesByGrade(MercenaryGrade grade)
		{
			var list = new List<MercenaryData>();
			foreach (var kvp in _mercenaryDatabase)
			{
				if (kvp.Value.grade == grade)
					list.Add(kvp.Value);
			}
			return list.ToArray();
		}

		/// <summary>용병 고용</summary>
		public bool HireMercenary(string mercenaryId)
		{
			EnsureDatabase(); // [O7] Awake 미실행 환경 대응

			// [O3 C-O3-03] 컨텐츠 게이트 — "tavern_mercenary" (Lv5, 초반 보호). 가장 얕은 진입점에서 차단.
			// [O7] 싱글턴 미연결 환경(에디터 테스트) 대응 — FindAnyObjectByType 폴백.
			var stats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
			int level = stats?.Level ?? 1;
			if (!ContentGate.IsUnlocked("tavern_mercenary", level))
			{
				string msg = ContentGate.GetLockedMessage("tavern_mercenary");
				// [158차 P2] 고용 차단 피드백 — 콘솔 전용 → 대화 패널 표출(상호작용불가=피드백 필수 계약).
				PlayerNotify.Notify(msg);
				Debug.LogWarning("[MercenaryManager] " + msg);
				return false;
			}

			if (!_mercenaryDatabase.ContainsKey(mercenaryId))
			{
				Debug.LogWarning($"[MercenaryManager] 알 수 없는 용병 ID: {mercenaryId}");
				return false;
			}

			// 이미 고용된 용병 확인 (동일 ID 중복 고용 금지 — 고용 상한은 카탈로그 고유 ID 수가 결정)
			foreach (var m in _hiredMercenaries)
			{
				if (m.data.id == mercenaryId)
				{
					Debug.LogWarning($"[MercenaryManager] 이미 고용된 용병: {mercenaryId}");
					return false;
				}
			}

			var mercData = _mercenaryDatabase[mercenaryId];

			// 입단비 + 첫 주 계약비 결제 (등록 전). 실패 시 고용 목록 불변.
			if (!SoldierWageSystem.TryChargeMercenaryHire(mercData, out _, out _))
			{
				Debug.LogWarning($"[MercenaryManager] 골드 부족 — 고용 거부: {mercenaryId}");
				return false;
			}

			var instance = new MercenaryInstance
			{
				data = mercData,
				currentHP = mercData.maxHP,
				isAlive = true,
				affinity = 50f, // 기본 호감도 50
				potionSlots = new PlayerInventory.ItemData[3]
			};

			_hiredMercenaries.Add(instance);

			// 호감도 초기화 (단일 진실 공급원)
			if (!_affinity.ContainsKey(mercenaryId))
				_affinity[mercenaryId] = 50f;

			Debug.Log($"[MercenaryManager] ✅ 용병 고용: {instance.data.mercenaryName} ({instance.data.GradeStars})");

			// [P3] 바드 고용 시 기본 류트 자동 장착 — B키 연주 즉시 가능(악기 미보유로 막히는 것 방지)
			if (instance.data.jobType == "Bard")
			{
				var equipSys = GuardEquipmentSystem.Instance ?? FindAnyObjectByType<GuardEquipmentSystem>();
				if (equipSys != null)
				{
					var lute = InstrumentData.ToItemData(InstrumentData.GetProfile("instrument_bard_lute").Value);
					equipSys.EquipMercenary(mercenaryId, GuardEquipmentSystem.EquipSlot.Instrument, lute);
					Debug.Log("[MercenaryManager] 🎵 바드 기본 류트 자동 장착 완료");
				}
			}
			return true;
		}

		/// <summary>용병 해고</summary>
		public bool FireMercenary(string mercenaryId)
		{
			for (int i = 0; i < _hiredMercenaries.Count; i++)
			{
				if (_hiredMercenaries[i].data.id == mercenaryId)
				{
					Debug.Log($"[MercenaryManager] 🔴 용병 해고: {_hiredMercenaries[i].data.mercenaryName}");
					_hiredMercenaries.RemoveAt(i);
					// 계약 스케줄 정리 (스테일 청구일 방지). 골드 변동 없음.
					SoldierWageSystem.ClearContractSchedule(mercenaryId);
					// 호감도 데이터 정리 (고아 데이터 방지)
					if (_affinity.ContainsKey(mercenaryId))
						_affinity.Remove(mercenaryId);
					return true;
				}
			}
			Debug.LogWarning($"[MercenaryManager] 해고할 용병 없음: {mercenaryId}");
			return false;
		}

		/// <summary>고용된 용병 목록 반환</summary>
		public MercenaryInstance[] GetHiredMercenaries()
		{
			return _hiredMercenaries.ToArray();
		}

		/// <summary>특정 ID의 용병 인스턴스 반환 (없으면 default)</summary>
		public MercenaryInstance GetHiredMercenary(string mercenaryId)
		{
			foreach (var m in _hiredMercenaries)
			{
				if (m.data.id == mercenaryId)
					return m;
			}
			return default;
		}

		/// <summary>특정 ID의 고용된 용병 인스턴스 조회 (안전 패턴)</summary>
		public bool TryGetHiredMercenary(string mercenaryId, out MercenaryInstance instance)
		{
			for (int i = 0; i < _hiredMercenaries.Count; i++)
			{
				if (_hiredMercenaries[i].data.id == mercenaryId)
				{
					instance = _hiredMercenaries[i];
					return true;
				}
			}
			instance = default;
			return false;
		}

		/// <summary>고용된 용병 수</summary>
		public int HiredCount => _hiredMercenaries.Count;

		/// <summary>
		/// 고용 가능한 서로 다른 용병 수 (카탈로그 고유 ID 수). 인원 상한이 아니라 실제 고용 가능 대상 수다.
		/// 같은 ID는 한 번만 고용 가능하므로 HiredCount의 최댓값이 이 값이다.
		/// </summary>
		public int AvailableMercenaryCount
		{
			get
			{
				EnsureDatabase();
				return _mercenaryDatabase.Count;
			}
		}

		// ===== 호감도 시스템 =====

		/// <summary>호감도 증가 (전투 참여, 선물 등)</summary>
		public void AddAffinity(string mercenaryId, float amount)
		{
			if (!_affinity.ContainsKey(mercenaryId))
				_affinity[mercenaryId] = 50f;

			_affinity[mercenaryId] = Mathf.Clamp(_affinity[mercenaryId] + amount, 0f, 100f);

			// 인스턴스에도 반영 (고용 중인 경우에만)
			for (int i = 0; i < _hiredMercenaries.Count; i++)
			{
				if (_hiredMercenaries[i].data.id == mercenaryId)
				{
					var instance = _hiredMercenaries[i];
					instance.affinity = _affinity[mercenaryId];
					_hiredMercenaries[i] = instance;
					break;
				}
			}
		}

		/// <summary>호감도 조회 (단일 진실 공급원: _affinity dict)</summary>
		public float GetAffinity(string mercenaryId)
		{
			if (_affinity.TryGetValue(mercenaryId, out float val))
				return val;
			return 50f;
		}

		/// <summary>호감도 기반 능력치 보너스 계산 (0~20%)</summary>
		public float GetAffinityBonus(string mercenaryId)
		{
			float aff = GetAffinity(mercenaryId);
			return aff / 100f * 0.2f; // 100% 호감도 = 20% 능력치 보너스
		}

		// ===== 물약 시스템 =====

		/// <summary>용병 물약 슬롯에 아이템 장착</summary>
		public bool SetPotionInSlot(string mercenaryId, int slotIndex, PlayerInventory.ItemData item)
		{
			if (slotIndex < 0 || slotIndex >= 3)
				return false;

			for (int i = 0; i < _hiredMercenaries.Count; i++)
			{
				if (_hiredMercenaries[i].data.id == mercenaryId)
				{
					if (item == null || item.category == PlayerInventory.ItemCategory.Potion || item.category == PlayerInventory.ItemCategory.Herb)
					{
						var instance = _hiredMercenaries[i];
						instance.potionSlots[slotIndex] = item;
						_hiredMercenaries[i] = instance;
						return true;
					}
					return false;
				}
			}
			return false;
		}

		/// <summary>용병 자동 회복 처리 (HP 30% 이하)</summary>
		public void ProcessAutoHeal(string mercenaryId)
		{
			int idx = _hiredMercenaries.FindIndex(m => m.data.id == mercenaryId);
			if (idx < 0) return;

			var merc = _hiredMercenaries[idx];
			if (!merc.isAlive) return;

			float hpRatio = merc.currentHP / merc.data.maxHP;
			if (hpRatio > 0.3f) return; // 30% 초과면 사용 안 함

			// 물약 슬롯에서 회복 아이템 찾기
			for (int i = 0; i < 3; i++)
			{
				var item = merc.potionSlots[i];
				if (item != null && item.category == PlayerInventory.ItemCategory.Potion)
				{
					// 회복 효과 적용
					// TODO: ItemData에 healAmount 필드 추가 후 실제 값 사용
					float healAmount = 30f;
					merc.currentHP = Mathf.Min(merc.data.maxHP, merc.currentHP + healAmount);
					merc.potionSlots[i] = null;
					_hiredMercenaries[idx] = merc; // ★ struct 재할당 (필수!)
					Debug.Log($"[MercenaryManager] 💊 {merc.data.mercenaryName} 자동 회복! +{healAmount} HP");
					return;
				}
			}
		}

		// ===== 저장/복원 (SaveData v4 mercenaries 블록) =====

		/// <summary>
		/// 고용 로스터와 호감도 테이블을 DTO로 내보낸다. Unity 참조(potionSlots 아이템)는 저장하지 않는다.
		/// 호감도는 고용 중이 아닌 용병 것도 포함한다(_affinity 단일 테이블).
		/// </summary>
		public MercenarySaveData ExportSaveData()
		{
			var data = new MercenarySaveData();
			foreach (var m in _hiredMercenaries)
			{
				data.hired.Add(new MercenaryInstanceSaveData
				{
					mercenaryId = m.data.id,
					currentHP = m.currentHP,
					isAlive = m.isAlive,
					affinity = m.affinity
				});
			}

			var keys = new List<string>(_affinity.Keys);
			keys.Sort(string.CompareOrdinal);
			foreach (string key in keys)
			{
				data.affinities.Add(new AffinitySaveEntry { mercenaryId = key, value = _affinity[key] });
			}
			return data;
		}

		/// <summary>
		/// 고용 로스터 복원. 골드를 청구하지 않는다(TryChargeMercenaryHire 호출 없음).
		/// 용병 정의는 id로 DB에서 재조회하며, 알 수 없는 id 또는 중복 id는 건너뛴다.
		/// 물약 슬롯은 빈 상태로 복원된다. 호감도는 저장된 값을 우선하고, 없으면 고용 중 인스턴스 값/기본 50.
		/// 계약 청구일 복원은 SoldierWageSystem.ImportSaveData가 담당한다(이 메서드 이후 호출).
		/// </summary>
		public void ImportSaveData(MercenarySaveData data)
		{
			EnsureDatabase();
			_hiredMercenaries.Clear();
			_affinity.Clear();

			if (data == null) return;

			if (data.affinities != null)
			{
				foreach (var a in data.affinities)
				{
					if (a == null || string.IsNullOrEmpty(a.mercenaryId)) continue;
					_affinity[a.mercenaryId] = Mathf.Clamp(a.value, 0f, 100f);
				}
			}

			if (data.hired != null)
			{
				foreach (var h in data.hired)
				{
					if (h == null || string.IsNullOrEmpty(h.mercenaryId)) continue;
					if (!_mercenaryDatabase.TryGetValue(h.mercenaryId, out var mercData))
					{
						Debug.LogWarning($"[MercenaryManager] 세이브의 알 수 없는 용병 ID 건너뜀: {h.mercenaryId}");
						continue;
					}
					bool duplicate = false;
					foreach (var m in _hiredMercenaries)
					{
						if (m.data.id == h.mercenaryId) { duplicate = true; break; }
					}
					if (duplicate) continue;

					float aff = _affinity.TryGetValue(h.mercenaryId, out float stored) ? stored : Mathf.Clamp(h.affinity, 0f, 100f);
					_affinity[h.mercenaryId] = aff;

					_hiredMercenaries.Add(new MercenaryInstance
					{
						data = mercData,
						currentHP = Mathf.Clamp(h.currentHP, 0f, mercData.maxHP),
						isAlive = h.isAlive,
						affinity = aff,
						potionSlots = new PlayerInventory.ItemData[3]
					});
				}
			}
		}
	}

	/// <summary>
	/// 고용된 용병의 인스턴스 데이터 (런타임 상태 포함).
	/// NOTE: struct로 구현되어 있어 수정 후 반드시 List에 재할당 필요.
	/// </summary>
	[System.Serializable]
	public struct MercenaryInstance
	{
		/// <summary>용병 기본 데이터</summary>
		public MercenaryData data;

		/// <summary>현재 체력</summary>
		public float currentHP;

		/// <summary>생존 여부</summary>
		public bool isAlive;

		/// <summary>호감도 (0~100) — 단일 진실 공급원은 MercenaryManager._affinity dict</summary>
		public float affinity;

		/// <summary>물약 전용 슬롯 (3칸)</summary>
		public PlayerInventory.ItemData[] potionSlots;

		/// <summary>능력치 보너스 비율 (호감도 기반)</summary>
		public float AffinityBonus => affinity / 100f * 0.2f;
	}
}