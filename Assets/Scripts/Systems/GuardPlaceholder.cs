using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 병사 모델.
    /// C9-08: 병사 상호작용 → 정보 HUD 표시 + 메뉴 (말걸기/음식주기/약주기)
    /// C9-11: 음식/약주기 — 인벤토리 선택 → 아이템 지급 → 호감도/중독도 변화
    /// Phase 34: NPCAwarenessSystem 연동 + 시야각 120° + 암살
    /// P13 (2026-09-20): 상호작용 UI OnGUI 철거 → UTK SoldierInteractUTK 창으로 이관.
    ///               F키 → SoldierInteractBridge.RaiseInteract → SoldierInteractUTK (정보는 창 내 '병사 정보보기').
    ///               OnTalk/OnRecruit/아이템 지급 로직은 public 래퍼(BeginTalk/BeginRecruit/GiveFood/GiveDrug)로
    ///               재사용 — 데이터/규칙 불변. E키 토글 제거(실내 진입 전용).
    /// </summary>
    public class GuardPlaceholder : MonoBehaviour, IDamageable, IWorldSpaceHUD
    {
        [Header("설정")]
        [SerializeField] private string guardName = "경비병";
        [SerializeField] private int level = 1;
        [SerializeField] private string nation = "동";
        [SerializeField] private string jobTitle = "병사";

        // ===== [2026-09-17] 랜덤 스탯 배분 (레벨 고정 총합, 4-way 랜덤 분배) =====
        private int _statAttack;
        private int _statDefense;
        private int _statVitality;   // 체력 보너스 (기본 MaxHP에 가산)
        private int _statAgility;    // 민첩
        private bool _statsRolled;

        [Header("상호작용")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _maxHP = 25f;   // [TEST27-68차] 10→25 — 슬라임 공격 5 기준 2타 사망 → 5타로 완화(병사 전투 체감)
        private float _currentHP;
        private bool _isDead = false;

        // [P30-C] 아군 임시 버프 상태 — 아군병사 물약 지급 시 PotionBuffData 기반으로 적립(가산 중첩),
        //         UpdateAllyBuff(Time.deltaTime)가 잔여 시간을 경과 감소시키고 0 도달 시 소멸. 적병사는 항상 0.
        private float _allyAttackBuff;
        private float _allyDefenseBuff;
        private float _allyAgilityBuff;
        private float _allyBuffRemaining;   // 초 단위 잔여. 0이면 버프 없음.

        [Header("호감도/중독")]
        [SerializeField] private float _loyalty = 50f;
        [SerializeField] private float _addiction = 0f;

        [Header("포섭 (C9-15)")]
        [SerializeField] private bool _isRecruited = false;

        [Header("역할 (C9-16)")]
        [SerializeField] private GuardRole _role = GuardRole.Soldier; // 플레이어에게 포섭되었는가

        // ===== P30-A: 문지기 (영지 출입 조건 판정은 Phase5가 수행 — 여기선 필드만) =====
        [Header("문지기 (P30 — Phase5 판정용)")]
        [SerializeField] private bool _isGatekeeper = false;

        // ===== Phase 34: NPCAwarenessSystem =====
        [Header("Phase 34 — 경계 AI")]
        [SerializeField] private float _sightRange = 12f;
        [SerializeField][Range(1f, 180f)] private float _fieldOfView = 120f; // 시야각 120°
        private NPCAwarenessSystem _awareness;

        // ===== 사망 이벤트 (GuardResurrectionSystem 연동) =====
        public static event System.Action<GuardPlaceholder> OnAnyGuardDied;

        private enum SelectionMode { None, SelectingFood, SelectingDrug }
        private SelectionMode _selectionMode = SelectionMode.None;

        private bool _playerNearby = false;
        private string _statusMessage = "";
        // Rig animation
        private RigAnimationController _rigAnim;

        // 캐시된 플레이어 참조 (매 프레임 Find 방지)
        private GameObject _playerCache;

        private void Awake()
        {
            _rigAnim = GetComponent<RigAnimationController>();
            if (_rigAnim == null)
            {
                Animator anim = GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    _rigAnim = gameObject.AddComponent<RigAnimationController>();
            }

            // C9-20: Rigidbody 캐싱 (있으면 MovePosition으로 이동 우회, 없으면 transform 직접 이동)
            _rb = GetComponent<Rigidbody>();

            // Phase 34: NPCAwarenessSystem 캐싱 (없으면 자동 추가)
            _awareness = GetComponent<NPCAwarenessSystem>();
            if (_awareness == null)
            {
                _awareness = gameObject.AddComponent<NPCAwarenessSystem>();
            }

            // P30-A: 인스펙터 문지기 플래그 → 런타임 프로퍼티 동기화 (출입 판정은 Phase5가 수행)
            IsGatekeeper = _isGatekeeper;
        }

        public void SetGuardInfo(string name, int lvl, NationType nationType)
        {
            guardName = name;
            level = lvl;
            nation = NationTypeToKorean(nationType);
        }

        private static string NationTypeToKorean(NationType type)
        {
            switch (type)
            {
                case NationType.East: return "동";
                case NationType.West: return "서";
                case NationType.South: return "남";
                case NationType.North: return "북";
                case NationType.Empire: return "황제국";
                default: return "무소속";
            }
        }

        private void Start()
        {
            _currentHP = _maxHP;

            // 플레이어 캐싱
            _playerCache = GameObject.FindGameObjectWithTag("Player");

            // 기본 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // C32-04~06: 병사 장비 자동 생성 및 장착
            GuardEquipmentSpawner.SpawnEquipment(gameObject, level);

            // [2026-09-17] 랜덤 스탯 배분 — 먼저 롤된 장비와 무관하게, 레벨 기반 총합을 4개 능력치로 분배.
            RollStats();
        }

        private void Update()
        {
            // [TEST28-69차] 사망 시 업데이트 정지 — 쓰러짐 연출 중 시체가 명령/이동하지 않도록
            if (_isDead) return;

            // 캐시된 참조 갱신 (null이거나 비활성화된 경우 재탐색)
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            var player = _playerCache;

            // C9-20/C9-21: 명령 실행 루프 — 플레이어 부재와 무관하게 RTS/전투 명령(이동·공격)을 수행한다
            ExecuteMovement();

            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            _playerNearby = dist <= _interactRange;

            // [P13] E키 토글 제거 — 상호작용 창은 F키 → SoldierInteractUTK 전용.
            // 멀어지면 아이템 선택 모드만 해제(창 자체는 UTK 쪽 200ms 폴링 — 4.5m 초과 자동 닫힘).
            if (_selectionMode != SelectionMode.None && dist > _interactRange * 1.5f)
            {
                _selectionMode = SelectionMode.None;
            }

            // C9-12: 중독도 처리 (생존 중일 때만)
            if (!_isDead && _addiction > 0)
            {
                GuardAddictionSystem.ProcessDecay(this, Time.deltaTime);
                GuardAddictionSystem.ProcessPoisonDamage(this, Time.deltaTime);
                GuardAddictionSystem.CheckOverdose(this);
            }

            // 전투 타이머 갱신
            UpdateCombatTimer(Time.deltaTime);

            // Phase 34: NPCAwarenessSystem 연동 — 시야각 120° 체크
            UpdateAwareness(player, dist);

            // Phase 34: 은신 상태 NPC 뒤에서 좌클릭 → 암살
            TryAssassinateGuard(player, dist);

            // C9-21: 동행 병사 전투 AI (매 Update 말미 — playerTransform은 캐시된 _playerCache.transform)
            GuardCombatAI.UpdateGuardBehavior(this, player.transform);

            // [P30-C] 아군 임시 버프 경과 감소 — Update 말미 호출(잔여 0 도달 시 버프 소멸)
            UpdateAllyBuff(Time.deltaTime);
        }

        /// <summary>
        /// [P13] 현재 선택 모드(음식/약)에 맞는 인벤토리 아이템 목록 반환 — SoldierInteractUTK가 호출.
        /// 음식 = Food / 약 = Potion+Drug (기존 IMGUI 팝업과 동일 규칙).
        /// </summary>
        public List<KeyValuePair<PlayerInventory.ItemData, int>> GetInventoryItemsByMode()
        {
            var result = new List<KeyValuePair<PlayerInventory.ItemData, int>>();
            if (PlayerInventory.Instance == null) return result;

            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                bool matches = _selectionMode == SelectionMode.SelectingFood
                    ? slot.item.category == PlayerInventory.ItemCategory.Food
                    : slot.item.category == PlayerInventory.ItemCategory.Potion
                      || slot.item.category == PlayerInventory.ItemCategory.Drug;
                if (matches) result.Add(new KeyValuePair<PlayerInventory.ItemData, int>(slot.item, slot.count));
            }
            return result;
        }

        // ===== C9-11: 아이템 지급 처리 =====
        private void GiveItemToGuard(PlayerInventory.ItemData item)
        {
            if (PlayerInventory.Instance == null || !PlayerInventory.Instance.HasItem(item.id))
            {
                _statusMessage = "아이템이 부족합니다.";
                return;
            }

            PlayerInventory.Instance.RemoveItem(item.id);

            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Food:
                    float heal = 5f + item.displayName.Length * 0.5f;
                    _currentHP = Mathf.Min(_maxHP, _currentHP + heal);
                    GuardLoyaltySystem.GiveGift(this, 30);
                    _statusMessage = $"{guardName}: \\\"음식 고맙다!\\\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Potion:
                    _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    _statusMessage = $"{guardName}: \\\"약을 주다니 고맙군!\\\" ❤️ 호감도 UP";
                    break;

                case PlayerInventory.ItemCategory.Drug:
                    GuardLoyaltySystem.GiveDrug(this, 2);
                    _statusMessage = $"{guardName}: \\\"어.. 뭔가 이상한 기분이...\\\" 💊 중독+10";
                    break;
            }
        }

        // ===== [P13] UTK 상호작용 창용 public 래퍼 — 기존 private 로직 재사용(데이터/규칙 불변) =====
        /// <summary>병사 상태 메시지 (SoldierInteractUTK 표시용 — 말걸기/지급/포섭 응답 등).</summary>
        public string StatusMessage => _statusMessage;

        /// <summary>🗣️ 말걸기 — 기존 OnTalk 경로.</summary>
        public void BeginTalk() { OnTalk(); }

        /// <summary>🤝 포섭 — 기존 OnRecruit 경로 (GuardRecruitSystem 규칙 불변).</summary>
        public void BeginRecruit() { OnRecruit(); }

        /// <summary>🥩 음식 선택 모드 진입 — 기존 _selectionMode 세팅만 수행 (UTK 창이 호출).</summary>
        public void BeginFoodSelection() { _selectionMode = SelectionMode.SelectingFood; }

        /// <summary>💊 약 선택 모드 진입 — 기존 _selectionMode 세팅만 수행 (UTK 창이 호출).</summary>
        public void BeginDrugSelection() { _selectionMode = SelectionMode.SelectingDrug; }

        /// <summary>아이템 선택 모드 해제 (UTK 창 취소/닫기 시 호출).</summary>
        public void CancelItemSelection() { _selectionMode = SelectionMode.None; }

        /// <summary>🥩 음식 지급 — 기존 GiveItemToGuard 경로 (호감도/회복 규칙 불변).</summary>
        public void GiveFood(PlayerInventory.ItemData item) { GiveItemToGuard(item); }

        /// <summary>💊 약 지급 — 기존 GiveItemToGuard 경로 (중독/중독도 규칙 불변).</summary>
        public void GiveDrug(PlayerInventory.ItemData item) { GiveItemToGuard(item); }

        // ===== [P30-C] 아군병사 전용 — 물약/음식 버프 실행 + 장비 직접 등록 (적병사 GiveDrug 중독 경로 불변) =====

        /// <summary>아군 현재 공격 임시 버프 (PotionBuffData 기반 — 물약 지급으로 가산 적립, UpdateAllyBuff로 경과 소멸).</summary>
        public float AllyAttackBuff => _allyAttackBuff;

        /// <summary>아군 현재 방어 임시 버프.</summary>
        public float AllyDefenseBuff => _allyDefenseBuff;

        /// <summary>아군 현재 민첩 임시 버프.</summary>
        public float AllyAgilityBuff => _allyAgilityBuff;

        /// <summary>아군 임시 버프 잔여 시간(초). 0이면 버프 없음.</summary>
        public float AllyBuffRemaining => _allyBuffRemaining;

        /// <summary>아군 임시 버프 경과 감소 — Update 말미에서 호출. 잔여 0 도달 시 버프 수치도 함께 소멸.</summary>
        public void UpdateAllyBuff(float deltaTime)
        {
            if (_allyBuffRemaining <= 0f) return;
            _allyBuffRemaining = Mathf.Max(0f, _allyBuffRemaining - deltaTime);
            if (_allyBuffRemaining <= 0f)
            {
                _allyAttackBuff = 0f;
                _allyDefenseBuff = 0f;
                _allyAgilityBuff = 0f;
                _statusMessage = $"{guardName}: 버프 효과가 사라졌다.";
            }
        }

        /// <summary>
        /// [P30-C] 🥩 음식 지급(아군 전용 래퍼) — 기존 GiveItemToGuard(Food=체력회복+호감도) 경로 그대로 유지.
        /// 음식 버프 = 체력 회복 자체(현행 유지, 과도한 신규 시스템 금지). 리턴 타입만 bool 통일(성공 시 true).
        /// 적병사면 false 반환 — 적병사 음식은 기존 GiveFood 경로를 그대로 사용(호출부 분기).
        /// </summary>
        public bool GiveAllyFood(PlayerInventory.ItemData item)
        {
            if (!IsAlly || item == null) return false;
            var inv = PlayerInventory.Instance;
            if (inv == null || inv.GetItemCount(item.id) <= 0) return false;
            int before = inv.GetItemCount(item.id);
            GiveFood(item);   // 기존 경로(회복+호감도) — 데이터/규칙 불변
            return inv.GetItemCount(item.id) < before;
        }

        /// <summary>
        /// [P30-C] 💊 약 지급(아군 전용 래퍼) — PotionBuffData 버프를 아군에게 실행(가산 중첩, 잔여 시간 가산).
        /// 아군은 중독 경로(GiveDrug → Drug 케이스)를 타지 않는다 — 적병사 전용 규칙 불변.
        /// 즉효 물약(buffSeconds=0)은 healFlat/healPercent 즉시 회복. 미등록 id는 기존 소량 회복 폴백.
        /// 아이템 제거는 본 메서드 내부에서 수행(GiveItemToGuard와 동일 소유 규칙).
        /// </summary>
        public void ApplyAllyPotion(PlayerInventory.ItemData item)
        {
            if (!IsAlly) { _statusMessage = "아군병사에게만 물약 버프를 줄 수 있습니다."; return; }
            if (item == null) return;
            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.HasItem(item.id)) { _statusMessage = "아이템이 부족합니다."; return; }
            inv.RemoveItem(item.id);

            if (item.category == PlayerInventory.ItemCategory.Drug)
            {
                // 아군에게 마약 — 중독 금지(적병사 전용 규칙). 소량 회복만.
                _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                GuardLoyaltySystem.GiveGift(this, 30);
                _statusMessage = $"{guardName}: \"이런 건 좀... 그래도 고맙군.\"";
                return;
            }

            if (PotionBuffData.GetBuff(item.id, out PotionBuffEffect eff))
            {
                if (eff.buffSeconds > 0f)
                {
                    // 버프 가산 중첩(잔여 시간 가산) — UpdateAllyBuff가 경과 소멸 담당
                    _allyAttackBuff += eff.attackBuff;
                    _allyDefenseBuff += eff.defenseBuff;
                    _allyAgilityBuff += eff.agilityBuff;
                    _allyBuffRemaining += eff.buffSeconds;
                    if (eff.healFlat > 0f || eff.healPercent > 0f)
                        _currentHP = Mathf.Min(_maxHP, _currentHP + eff.healFlat + _maxHP * eff.healPercent);
                    _statusMessage = $"{guardName}: \"몸에 힘이 넘치는군!\" ⚡ 버프 ON ({eff.buffSeconds:0}초)";
                }
                else
                {
                    // 즉효 물약 — 즉시 회복만 (potion_hp_small/big)
                    _currentHP = Mathf.Min(_maxHP, _currentHP + eff.healFlat + _maxHP * eff.healPercent);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    _statusMessage = $"{guardName}: \"약을 주다니 고맙군!\" ❤️ 호감도 UP";
                }
                return;
            }

            // 미등록 물약 — 기존 GiveItemToGuard.Potion 케이스와 동일 소량 회복 폴백
            _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
            GuardLoyaltySystem.GiveGift(this, 50);
            _statusMessage = $"{guardName}: \"약을 주다니 고맙군!\" ❤️ 호감도 UP";
        }

        /// <summary>
        /// [P30-C] 🛠️ 장비 직접 등록(아군 전용) — 플레이어 인벤토리 장비를 병사 6슬롯(무기/투구/갑옷/신발/장갑/방패)에 장착.
        /// GuardEquipmentSystem.EquipGuard는 EquipSlot{Weapon,Armor,Accessory,Instrument}만 지원하여
        /// 6슬롯(WeaponItem 등 자동 프로퍼티 — GuardInfoUTK.GearDefs/GetAttack/GetDefense가 읽는 원천)과 불일치하므로
        /// 규격 폴백: public 슬롯 setter로 직접 셋팅 + 플레이어 인벤토리에서 제거 + 교체된 이전 장비는 인벤토리 반환.
        /// 성공 시 true.
        /// </summary>
        public bool EquipAllyItem(PlayerInventory.ItemData item)
        {
            if (!IsAlly) { _statusMessage = "적병사에게는 장비를 직접 등록할 수 없습니다."; return false; }
            if (item == null) return false;

            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.HasItem(item.id)) { _statusMessage = "아이템이 부족합니다."; return false; }

            PlayerInventory.ItemData prev;
            string slotLabel;
            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Weapon:
                    prev = WeaponItem; WeaponItem = item; slotLabel = "무기";
                    break;

                case PlayerInventory.ItemCategory.Armor:
                    prev = EquipAllyArmorBySubType(item, out slotLabel);
                    break;

                default:
                    _statusMessage = $"{item.displayName}: 병사가 착용할 수 있는 장비가 아닙니다.";
                    return false;
            }

            inv.RemoveItem(item.id);
            if (prev != null) inv.AddItem(prev);   // 교체된 이전 장비 반환(GuardEquipmentSystem.ReturnToInventory 동일 규칙)
            _statusMessage = $"{guardName}: {item.displayName} 장착 완료!";
            UpdateVisual();   // 장비 외형 갱신 훅(현행 no-op — 호출 규약 유지)
            // [P30 후속②] 씬(월드 rig) 시각 연동 — 즉시 재부착 트리거 + 확인 로그(멱등)
            Debug.Log($"[GuardEquip] {guardName} {slotLabel} 장착 → {item.displayName} (시각 갱신 요청)");
            GuardVisualAttachSystem.RequestRefreshFor(this);
            return true;
        }

        /// <summary>Armor 카테고리 6슬롯 서브 분류(id 접두사/표시명 키워드 → 투구/신발/장갑/방패, 기본 갑옷). 교체된 이전 장비 반환.</summary>
        private PlayerInventory.ItemData EquipAllyArmorBySubType(PlayerInventory.ItemData item, out string slotLabel)
        {
            string id = item.id ?? string.Empty;
            string name = item.displayName ?? string.Empty;
            if (id.StartsWith("helmet_") || name.Contains("투구")) { var old = HelmetItem; HelmetItem = item; slotLabel = "투구"; return old; }
            if (id.StartsWith("boots_") || name.Contains("신발") || name.Contains("부츠")) { var old = BootsItem; BootsItem = item; slotLabel = "신발"; return old; }
            if (id.StartsWith("gloves_") || name.Contains("장갑")) { var old = GlovesItem; GlovesItem = item; slotLabel = "장갑"; return old; }
            if (id.StartsWith("shield_") || name.Contains("방패")) { var old = ShieldItem; ShieldItem = item; slotLabel = "방패"; return old; }
            var prev = ArmorItem; ArmorItem = item; slotLabel = "갑옷"; return prev;
        }

        private void OnTalk()
        {
            // ===== [P30-B] 대화 분기 — 적병사(!IsAlly): 중독 임계 + 호감 4단계. 아군은 아래 기존 대사 유지(Phase 3). =====
            if (!IsAlly)
            {
                // 중독 임계(60) 이상 — 중독 대화 1종 (강제 포섭 임계 RECRUIT_FORCE_THRESHOLD와 동일 경계값)
                if (Addiction >= GuardAddictionSystem.RECRUIT_FORCE_THRESHOLD)
                {
                    _statusMessage = $"{guardName}: '낯... 머리가 아득하다... 네가 뭐래도 따르겠다...'";
                    return;
                }

                // 호감도 4단계 (GuardLoyaltySystem.GetAffinityGrade — 호감>=60 / 보통 30~60 / 경계 0~30 / 위험<0)
                switch (GuardLoyaltySystem.GetAffinityGrade(_loyalty))
                {
                    case AffinityGrade.Friendly:
                        _statusMessage = $"{guardName}: 호의적인 미소로 '어서 오게, 잘 지내고 있소.'";
                        return;
                    case AffinityGrade.Normal:
                        _statusMessage = $"{guardName}: '무슨 일이냐? 말해 보게.'";
                        return;
                    case AffinityGrade.Cautious:
                        _statusMessage = $"{guardName}: 경계하며 '...멀리서 말하게.'";
                        return;
                    case AffinityGrade.Danger:
                        _statusMessage = $"{guardName}: 노려보며 '가까이 오지 마라!'";
                        return;
                }
            }

            _statusMessage = guardName + ": \\\"무슨 일이냐?\\\"";
        }

        // ===== P30-B: 뇌물 (적병사 전용 — 아군 금지) =====
        /// <summary>
        /// 💰 뇌물주기 — 레벨 스케일 단가(GuardLoyaltySystem.GetBribeCost = 30 + level×25)로 시도.
        /// 아군(포섭 완료)이면 false(뇌물 금지). 성공 시 TryBribe 내부에서 골드 차감 + 호감도 상승(금액 비례).
        /// </summary>
        public bool AttemptBribe()
        {
            if (IsAlly) return false;   // 아군 뇌물 금지 — TryBribe의 IsRecruited 게이트와 이중 방어
            int cost = GuardLoyaltySystem.GetBribeCost(Level);
            if (!GuardLoyaltySystem.TryBribe(this, cost)) return false;   // 골드 부족/시스템 부재
            _statusMessage = $"{guardName}: '똑똑하군...' 💰 뇌물 {cost}골드 → 호감도 {Loyalty:F0}";
            return true;
        }

        // ===== C9-15: 포섭 =====
        private void OnRecruit()
        {
            if (_isRecruited)
            {
                _statusMessage = $"{guardName}: \\\"이미 영지에 소속되어 있네.\"";
                return;
            }

            var result = GuardRecruitSystem.AttemptRecruit(this);
            if (result.success)
            {
                _isRecruited = true;

                // P30-A: 중독 임계 포섭(method="addiction") → 호감도 MAX — 중독으로 절대복종
                if (result.method == "addiction")
                    Loyalty = GuardLoyaltySystem.MAX_LOYALTY;

                _statusMessage = result.message;
            }
            else
            {
                _statusMessage = result.message;
            }
        }

        // ===== IDamageable =====
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            if (_isDead) return;
            _currentHP -= amount;

            // Phase 3: 통합 피격 VFX (Organic, 흰색 데미지 숫자)
            CombatFXGate.PlayHitFX(gameObject, hitDirection, CombatHitType.Organic, false, amount, Color.white);
            Debug.Log($"[GuardPlaceholder] HitFX played (dmg={amount}, hp={_currentHP})");

            if (_currentHP <= 0) Die();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            TakeDamage(damageInfo.amount, damageInfo.knockback.normalized, "melee");
        }

        /// <summary>[Phase G] AI 영주가 파견한 공격부대 표식 — 전투에서 쓰러지면 전멸 대신
        /// 플레이어 소속(노획)으로 전환되어 승자 군단에 편입된다.</summary>
        public bool IsWartimeCapturable;

        private void Die()
        {
            if (_isDead) return;

            // [Phase G] 파견 공격부대 노획 — 전투에서 패배해도 전멸하지 않고 아군이 된다.
            // (AI 자동 전쟁의 가시/수치 파견부대가 전투에서 쓰러지는 순간, 사망 처리 대신 포섭)
            if (IsWartimeCapturable && !_isRecruited)
            {
                SetRecruited(true);
                SetHP(Mathf.Max(1f, _maxHP * 0.1f));
                Debug.Log($"[Phase G] ⚔️ AI 파견 공격부대 병사 '{guardName}' 노획 — 플레이어 군단으로 편입!");
                CombatLog.AddEntry($"{guardName} 노획(포섭)", LogType.Kill);
                return; // 사망 처리 중단 (부활 시스템 불필요 — 아군으로 재기)
            }
            // ⏱️ 전투 로그: 병사 처치 기록
            CombatLog.AddEntry($"{guardName} 처치!", LogType.Kill);

            // 경험치: 병사 레벨 기반 (level × 5 × 난수 0.8~1.2)
            int exp = Mathf.Max(1, Mathf.RoundToInt(level * 5f * Random.Range(0.8f, 1.2f)));
            PlayerStats.Instance?.AddEXP(exp);
            CombatLog.AddEntry($"병사 처치 경험치 +{exp}", LogType.Kill);
            Debug.Log($"[GuardPlaceholder] 병사 처치 경험치 +{exp} (Lv.{level} × 5 × 난수 0.8~1.2)");
            _isDead = true;

            // [TEST27-68차] 사망 연출 — 쓰러짐(전도) + 충돌 비활성 (부활/정리 시스템이 후속 처리)
            try
            {
                float yaw = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(-90f, yaw, 0f);
                foreach (var col in GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
            catch { }

            // Phase 3: 사망 카메라 연출 (킬 이펙트)
            CombatCameraEffects.PlayKill();

            // 사망 애니메이션 (Idle 즉시 적용)
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // 사망 이벤트 발생 (GuardResurrectionSystem 등에서 구독)
            OnAnyGuardDied?.Invoke(this);

            // GuardManager에서 제거
            if (GuardManager.Instance != null)
            {
                GuardManager.Instance.OnGuardDiedInGame(this);
            }

            // [TEST28-69차] 전리품/비활성화를 1.2s 지연 — 쓰러짐 연출 먼저, 그 후 전리품 생성(사용자 요구 순서).
            //   연출 동안 시체가 명령/이동하지 않도록 명령 해제 + Update _isDead 게이트.
            ClearCommand();
            StartCoroutine(DieLootAndDeactivate(1.2f));
        }

        /// <summary>[TEST28-69차] 쓰러짐 연출(1.2s) 후 전리품 생성 → 비활성화(GuardResurrectionSystem 호환 유지).</summary>
        private System.Collections.IEnumerator DieLootAndDeactivate(float delay)
        {
            yield return new WaitForSeconds(delay);
            // ===== 전리품/드랍 처리 (try-catch 격리 — 드랍 실패가 사망 로직을 중단하지 않도록) =====
            try
            {
                LootBasket basket = LootBasket.Create(transform.position);
                // DropTableManager.Instance null 가드 (NRE 방지 — null이면 아래 폴백 골드 블록 실행)
                DropTable dropTable = DropTableManager.Instance != null ? DropTableManager.Instance.GetSoldierTable() : null;
                if (dropTable != null)
                    dropTable.ApplyToBasket(basket, MonsterLevelManager.Instance?.GetDropRateBonus(level) ?? 0f);
                else
                {
                    PlayerInventory.ItemData goldItem = new PlayerInventory.ItemData
                    {
                        id = "gold", displayName = "금", description = "통화",
                        category = PlayerInventory.ItemCategory.Material, maxStack = 99
                    };
                    basket.AddItem(goldItem, level * 10);
                    if (Random.value < 0.5f) basket.AddItem(PlayerInventory.RabbitFur, 1);
                }

                // ===== 장착 장비 전리품 드랍 (무기/방패/투구/갑옷) =====
                // 장비는 항상 100% 드랍하며, '실제 장착된'(null이 아닌) 아이템만 떨어뜨린다.
                // 빈 슬롯은 위의 SoldierDropTable/폴백 드랍에 그대로 맡긴다.
                DropEquippedItems(basket);

                // ===== 사망 드랍 최소 보장 =====
                // 장비 + 드롭 테이블(및 폴백) 모두로 바구니가 비었으면 최소 1개(금)는 반드시 떨어뜨린다.
                if (basket.IsEmpty)
                {
                    basket.AddItem(PlayerInventory.Gold, 1);
                    Debug.Log($"[GuardPlaceholder] {guardName} 죽: 드랍 최소 보장 — 금 1개 전리품 드랍");
                }
            }
            catch (System.Exception ex)
            {
                // 드랍 실패가 사망 처리(비활성화/부활 시스템)를 중단하지 않도록 격리
                Debug.LogWarning($"[GuardPlaceholder] 드랍 처리 실패(사망 자체는 계속): {ex.Message}");
            }

            // 비활성화 (Destroy 대신 — GuardResurrectionSystem에서 부활 가능)
            gameObject.SetActive(false);
        }
        /// <summary>
        /// 장착된 장비 슬롯(무기/방패/투구/갑옷)을 전리품 바구니에 드랍합니다.
        /// null이 아닌(장착된) 아이템만 1개씩 드랍하며, 각 드랍 시 한국어 로그를 남깁니다.
        /// </summary>
        private void DropEquippedItems(LootBasket basket)
        {
            if (basket == null) return;
            DropEquippedSlot(basket, WeaponItem);
            DropEquippedSlot(basket, ShieldItem);
            DropEquippedSlot(basket, HelmetItem);
            DropEquippedSlot(basket, ArmorItem);
            DropEquippedSlot(basket, BootsItem);
            DropEquippedSlot(basket, GlovesItem);
        }

        /// <summary>단일 장비 슬롯 드랍 처리 (null이 아니면 100% 드랍)</summary>
        private void DropEquippedSlot(LootBasket basket, PlayerInventory.ItemData item)
        {
            if (item == null) return; // 빈 슬롯: 기존 SoldierDropTable 폴백 드랍에 맡긴다
            basket.AddItem(item, 1);
            Debug.Log($"[GuardPlaceholder] {guardName} 죽: {item.displayName} 전리품 드랍");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRange);

            // Phase 34: 시야 원뿔 (Gizmos)
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Vector3 forward = transform.forward;
            float halfFOV = _fieldOfView * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0, -halfFOV, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, halfFOV, 0) * forward;
            Gizmos.DrawLine(transform.position, transform.position + leftDir * _sightRange);
            Gizmos.DrawLine(transform.position, transform.position + rightDir * _sightRange);
        }

        // ===== Phase 34: NPCAwareness 연동 =====
        /// <summary>
        /// NPC 시야각 120° 기반 플레이어 감지 및 NPCAwarenessSystem 상태 업데이트.
        /// </summary>
        private void UpdateAwareness(GameObject player, float distance)
        {
            if (_isDead || _awareness == null) return;
            if (player == null) return;

            // 사망 시 강제 평화 상태
            if (!gameObject.activeInHierarchy)
            {
                _awareness.ForcePeace();
                return;
            }

            // 플레이어가 시야 범위 내에 있는가
            if (distance > _sightRange)
                return; // 너무 멀면 체크 불필요

            // 시야 방향 계산
            Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            // 시야각 120° (절반 60°)
            bool inSightCone = angle < (_fieldOfView * 0.5f);

            if (inSightCone)
            {
                // Raycast로 시야 차단 확인
                if (!Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer, out RaycastHit hit, distance))
                {
                    // 플레이어 발견 → Detected
                    if (_awareness.CurrentAwarenessState != NPCAwarenessSystem.AwarenessState.Detected)
                    {
                        _awareness.SetDetected(player);
                        SetInCombat(true);
                    }
                }
                else
                {
                    // 차단된 오브젝트가 플레이어 본인인지 확인
                    if (hit.collider.gameObject == player)
                    {
                        if (_awareness.CurrentAwarenessState != NPCAwarenessSystem.AwarenessState.Detected)
                        {
                            _awareness.SetDetected(player);
                            SetInCombat(true);
                        }
                    }
                    else
                    {
                        // 장애물 뒤 — Suspicious
                        _awareness.SetSuspicious(player.transform.position);
                    }
                }
            }
            else
            {
                // 시야 밖 — 은신 상태 체크할 필요 없음 (NPCAwarenessSystem 자체 처리)
            }
        }

        /// <summary>
        /// 은신 상태 + NPC 뒤에서 좌클릭 시 StealthAssassination.TryAssassinate 호출.
        /// </summary>
        private void TryAssassinateGuard(GameObject player, float distance)
        {
            if (_isDead) return;
            if (player == null) return;

            // 암살 시스템 확인
            if (StealthAssassination.Instance == null) return;
            if (StealthAssassination.IsPerformingAssassination) return;

            // 은신 상태 확인
            if (StealthSystem.Instance == null || !StealthSystem.Instance.IsStealthed) return;

            // 거리 체크 (암살 가능 거리)
            if (distance > 2.5f) return;

            // 좌클릭 감지
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // NPC 뒤에서만 암살 가능
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, dirToPlayer);
                // dot < -0.3 = 뒤쪽 (≈ 120° 범위)
                if (dot < -0.3f)
                {
                    // 암살 시도
                    bool success = StealthAssassination.Instance.TryAssassinate(gameObject);
                    if (success)
                    {
                        // 구독자에게 암살 알림
                        Debug.Log($"[GuardPlaceholder] {guardName} 암살당함!");
                    }
                }
            }
        }

        // ===== 퍼블릭 API =====
        public string GuardName => guardName;
        public int Level => level;
        public string Nation => nation;
        public string JobTitle { get => jobTitle; set => jobTitle = value; }
        public float HP => _currentHP;
        public float Loyalty { get => _loyalty; set => _loyalty = Mathf.Clamp(value, -100, 100); }

        // ===== [2026-09-17] 랜덤 스탯 (관/방/체력/민첩) =====

        private void EnsureStatsRolled()
        {
            if (!_statsRolled) RollStats();
        }

        /// <summary>
        /// 레벨 기반 총합 스탯 예산을 4개 능력치(공격/방어/체력/민첩)에 랜덤 배분.
        /// 총합(total = 6 + level*1.5)은 항상 일정 — 병사의 "스탯 강함"은 레벨로 고정되고
        /// 분포만 병사마다 무작위. 3개의 랜덤 절단점을 정렬해 4개 구간으로 나눈다.
        /// </summary>
        public void RollStats()
        {
            int total = Mathf.RoundToInt(6f + level * 1.5f);
            if (total < 4) total = 4;

            // 3개 랜덤 절단점 [1, total-1] — 중복 방지
            var cuts = new System.Collections.Generic.List<int>(3);
            int safety = 0;
            while (cuts.Count < 3 && safety < 100)
            {
                int cut = Random.Range(1, total);
                if (!cuts.Contains(cut)) cuts.Add(cut);
                safety++;
            }
            while (cuts.Count < 3) cuts.Add(total); // 드물게 부족 시 마지막에 가둠
            cuts.Sort();

            int a = cuts[0];
            int b = cuts[1] - cuts[0];
            int c = cuts[2] - cuts[1];
            int d = total - cuts[2];
            if (b < 0) b = 0;
            if (c < 0) c = 0;
            if (d < 0) d = 0;

            _statAttack = a;
            _statDefense = b;
            _statVitality = c;
            _statAgility = d;
            _statsRolled = true;
        }

        /// <summary>장비 보정 전 순수 공격 스탯 (롤된 값).</summary>
        public int GetStatAttack() { EnsureStatsRolled(); return _statAttack; }
        /// <summary>장비 보정 전 순수 방어 스탯 (롤된 값).</summary>
        public int GetStatDefense() { EnsureStatsRolled(); return _statDefense; }
        /// <summary>장비 보정 전 순수 체력 보너스 (롤된 값).</summary>
        public int GetStatVitality() { EnsureStatsRolled(); return _statVitality; }
        /// <summary>장비 보정 전 순수 민첩 스탯 (롤된 값).</summary>
        public int GetStatAgility() { EnsureStatsRolled(); return _statAgility; }

        /// <summary>아군 임시 버프의 활성치 — 잔여 시간 > 0일 때만 반영(적병사/만료 시 항상 0, 기존 경로 수치 불변).</summary>
        private int ActiveAllyBuff(float buffValue) => _allyBuffRemaining > 0f ? Mathf.RoundToInt(buffValue) : 0;

        /// <summary>총 공격력 = 순수 공격 + 장착 무기 보너스 (GearStatIndex) + [P30-C] 아군 물약 버프.</summary>
        public int GetAttack()
        {
            GetStatAttack();
            int gear = WeaponItem != null
                ? Mathf.RoundToInt(GearStatIndex.GetWeaponAttackBoost(WeaponItem.id))
                : 0;
            return _statAttack + gear + ActiveAllyBuff(_allyAttackBuff);
        }

        /// <summary>총 방어력 = 순수 방어 + 장착 방어구/부속 보너스 합 (GearStatIndex) + [P30-C] 아군 물약 버프.</summary>
        public int GetDefense()
        {
            GetStatDefense();
            return _statDefense + GetGearDefenseBonus() + ActiveAllyBuff(_allyDefenseBuff);
        }

        /// <summary>총 민첩 = 순수 민첩 + [P30-C] 아군 물약 버프 (민첩 장비 보너스는 현재 없음 — 평탄 유지).</summary>
        public int GetAgility()
        {
            GetStatAgility();
            return _statAgility + ActiveAllyBuff(_allyAgilityBuff);
        }

        /// <summary>총 최대체력 = 기본 MaxHP + 체력 스탯 보너스 × 2.</summary>
        public float GetMaxHP()
        {
            GetStatVitality();
            return _maxHP + _statVitality * 2f;
        }

        /// <summary>장착 방어구 5슬롯의 가능한 방어 보너스 합 (정수 반올림).</summary>
        private int GetGearDefenseBonus()
        {
            int sum = 0;
            if (HelmetItem != null) sum += Mathf.RoundToInt(GearStatIndex.GetArmorDefenseBoost(HelmetItem.id));
            if (ArmorItem != null)  sum += Mathf.RoundToInt(GearStatIndex.GetArmorDefenseBoost(ArmorItem.id));
            if (BootsItem != null)  sum += Mathf.RoundToInt(GearStatIndex.GetArmorDefenseBoost(BootsItem.id));
            if (GlovesItem != null) sum += Mathf.RoundToInt(GearStatIndex.GetArmorDefenseBoost(GlovesItem.id));
            if (ShieldItem != null) sum += Mathf.RoundToInt(GearStatIndex.GetArmorDefenseBoost(ShieldItem.id));
            return sum;
        }
        public float Addiction { get => _addiction; set => _addiction = Mathf.Clamp(value, 0, GuardAddictionSystem.MAX_ADDICTION); }
        public bool IsPlayerNearby => _playerNearby;
        public bool IsSelectingItem => _selectionMode != SelectionMode.None;

        // ===== IWorldSpaceHUD 구현 =====
        public Vector3 WorldPosition => transform.position + Vector3.up * 2.5f; // 머리 위
        public bool ShouldShowHUD => !_isDead && gameObject.activeInHierarchy;
        public int HUDLevel => level;
        public float HUDLoyalty => _loyalty;
        public float HUDAddiction => _addiction;
        public string HUDName => guardName;

        // ===== C9-20: RTS =====
        [Header("RTS 이동")]
        [SerializeField] private float _moveSpeed = 3f;
        private bool _isSelected = false;
        private Vector3 _commandTargetPos;
        private bool _hasCommand = false;
        private bool _isAttackCommand = false;

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            // Phase 41-2: Selection Outline 표시/제거
            if (SpecialEffectsController.Instance != null)
            {
                if (selected)
                    SpecialEffectsController.Instance.AddSelectionOutline(this);
                else
                    SpecialEffectsController.Instance.RemoveSelectionOutline(this);
            }
        }
        public bool IsSelected => _isSelected;
        public void SetCommandTarget(Vector3 t, bool a) { _commandTargetPos = t; _isAttackCommand = a; _hasCommand = true; }
        public void ClearCommand() { _hasCommand = false; _isAttackCommand = false; }
        public bool HasCommand => _hasCommand;
        public Vector3 CommandTarget => _commandTargetPos;
        public bool IsAttackCommand => _isAttackCommand;

        // ===== C9-20/C9-21: 명령 실행 루프 =====
        private const float MOVE_CLEAR_RADIUS = 1.0f;       // 이동 명령 해제 반경(m)
        private const float MOVE_STOP_RADIUS = 0.6f;        // 이동 정지 판정 반경(m) — 이내 접근 금지(목표 넘어감 방지)
        private const float ATTACK_ARRIVE_RADIUS = 1.5f;    // 공격 명령 도달 반경(m) — 공격 모션 개시
        private const float ATTACK_MELEE_RANGE = 2.2f;      // 근접 공격 유효 거리(m)
        private const float ATTACK_COOLDOWN_SECONDS = 1.2f; // 공격 쿨다운(초)
        private const float ROTATION_SPEED = 8f;            // 회전 보간 속도 (Slerp 계수)
        private const float TARGET_SEARCH_RADIUS = 2.5f;    // 명령 지점 주변 적 탐색 반경(m)

        private Component _attackTarget;                    // 공격 명령 대상 (IDamageable 구현 컴포넌트 캐시)
        private float _attackCooldown = 0f;                 // 공격 쿨다운 잔여 시간(초)
        private HumanoidClipDriver _clipDriver;             // 공격 모션 드라이버 (지연 캐싱)
        private Rigidbody _rb;                              // Rigidbody (없으면 transform 직접 이동)

        /// <summary>
        /// C9-20/C9-21: 명령 실행 루프 — RTS/전투 명령을 실제 이동·공격으로 수행한다.
        /// GuardPlaceholder는 Rigidbody 없는 단순 생성 프리팹(cube)이므로 transform 이동을 허용하되,
        /// Rigidbody가 존재하면 MovePosition으로 우회한다. 사망 시 어떤 행동도 하지 않는다.
        /// </summary>
        private void ExecuteMovement()
        {
            if (_isDead || !_hasCommand) return;
            // [P14] 실내 활성 시 이동 명령 정지 — 병사가 실내 좌표로 걸어 들어오는 것 차단
            if (ProjectName.Core.UITransitionState.IndoorActive) return;

            float delta = Time.deltaTime;

            // 공격 쿨다운 감소
            if (_attackCooldown > 0f) _attackCooldown -= delta;

            Vector3 current = transform.position;
            Vector3 target = _commandTargetPos;

            // 수평(XZ) 거리 기준 판정 — y는 지형 보정에 따라 흔들리므로 판정에서 제외
            Vector3 toTarget = target - current; toTarget.y = 0f;
            float distXZ = toTarget.magnitude;

            if (_isAttackCommand)
            {
                // ----- 공격 명령: 목표지점 도달(1.5m) 시 공격 모션 + 근접 데미지 -----
                if (!ValidateAttackTarget())
                {
                    // 대상 재탐색 (명령 지점 주변 유효 적)
                    ResolveAttackTarget();
                    if (_attackTarget == null)
                    {
                        // 대상이 죽었거나 유효한 적이 없으면 명령 해제
                        Debug.Log($"[GuardPlaceholder] {guardName} 공격 대상 상실 → 명령 해제");
                        ClearCommand();
                        return;
                    }
                }

                if (distXZ > ATTACK_ARRIVE_RADIUS)
                {
                    // 미도달 → 목표지점으로 이동
                    StepToward(current, target, distXZ, delta);
                }
                else
                {
                    // 도달 → 대상 방향 회전 후 쿨다운 게이트 공격
                    FaceToward(_attackTarget.transform.position - current, delta);

                    Vector3 dirToTarget = _attackTarget.transform.position - current; dirToTarget.y = 0f;
                    if (_attackCooldown <= 0f && dirToTarget.magnitude <= ATTACK_MELEE_RANGE)
                        PerformAttack((IDamageable)_attackTarget);
                }
            }
            else
            {
                // ----- 이동 명령: 도달 반경 1.0m 진입 시 명령 해제 -----
                if (distXZ <= MOVE_CLEAR_RADIUS)
                {
                    ClearCommand();
                    if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
                }
                else
                {
                    StepToward(current, target, distXZ, delta);
                }
            }
        }

        /// <summary>목표를 향해 1프레임 이동 (속도 * delta, 목표 넘어감 방지 클램프 + 지형 y 보정 + 회전).</summary>
        private void StepToward(Vector3 current, Vector3 target, float distXZ, float delta)
        {
            Vector3 dirXZ = target - current; dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            dirXZ.Normalize();

            // 이동량 = _moveSpeed * delta, 목표를 넘어가지 않도록 클램프 (정지 반경 0.6m 유지)
            float step = Mathf.Min(_moveSpeed * delta, Mathf.Max(0f, distXZ - MOVE_STOP_RADIUS));
            if (step <= 0f) return;

            Vector3 next = current + dirXZ * step;
            // 지형 계약: 이동 y는 표면(1 + GetHeightAt)으로 보정 (FarmPlot.TryGetSurfaceY와 동일 수식)
            next.y = TryGetGroundY(next.x, next.z, current.y);

            // Rigidbody가 있으면 MovePosition, 없으면 transform 직접 이동
            if (_rb != null && !_rb.isKinematic) _rb.MovePosition(next);
            else transform.position = next;

            // 이동 방향으로 회전 (Slerp 8f * delta)
            FaceToward(dirXZ, delta);

            // 이동 애니메이션 (SetState는 동일 상태 재호출 시 early-return 하므로 매 프레임 호출 안전)
            if (_rigAnim != null) _rigAnim.SetState(AnimationState.Walk);
        }

        /// <summary>수평 방향으로 부드럽게 회전 (Quaternion.Slerp, ROTATION_SPEED * delta).</summary>
        private void FaceToward(Vector3 dirXZ, float delta)
        {
            dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(dirXZ.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, ROTATION_SPEED * delta);
        }

        /// <summary>
        /// 공격 모션 트리거(HumanoidClipDriver 우선, 없으면 RigAnimationController 폴백)
        /// + 근접 데미지 적용 (공격력 = level * 1.5f, 쿨다운 1.2s).
        /// </summary>
        private void PerformAttack(IDamageable target)
        {
            if (_clipDriver == null) _clipDriver = GetComponent<HumanoidClipDriver>();
            if (_clipDriver != null) _clipDriver.TriggerAttack();
            else if (_rigAnim != null) _rigAnim.SetState(AnimationState.Attack);

            // 데미지 적용 (대상은 ValidateAttackTarget에서 유효성 검증 완료 상태)
            // [2026-09-17] 기존 level × 1.5 → 장착 무기를 반영한 GetAttack() 사용.
            float damage = GetAttack();
            Vector3 dir = transform.forward;
            if (_attackTarget is Component at)
            {
                Vector3 toTarget = at.transform.position - transform.position; toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f) dir = toTarget.normalized;
            }

            target.TakeDamage(damage, dir, "guard");   // [TEST28-69차] "melee"→"guard" — 몬스터 어그로가 플레이어로 플립되는 것 차단(AnimalAI.TakeDamage 게이트)
            _attackCooldown = ATTACK_COOLDOWN_SECONDS;

            // [TEST26-67차] 병사 타격 가시 피드백 — 데미지 숫자 표시.
            // 뿌리(실측): 병사 데미지는 정상 적용됐다(로그 "슬라임 15 데미지! HP=7.7/35") — 그러나
            //   데미지 숫자는 PlayerCombat→CombatFXGate 단일 경로에만 있어 병사 타격엔 숫자가 안 떠
            //   '몬스터 피가 안 달린다'로 보였다. 병사 타격에도 동일 숫자 API로 표시(피격 플래시는
            //   AnimalAI.TakeDamage가 이미 수행).
            try
            {
                if (target is Component tc)
                    CombatVFXController.ShowDamageNumber(
                        tc.transform.position + Vector3.up * 1.6f,
                        Mathf.RoundToInt(damage),
                        new Color(1f, 0.85f, 0.4f),
                        CombatVFXController.DamageNumberType.Normal);
            }
            catch (System.Exception fxEx)
            {
                Debug.LogWarning($"[GuardPlaceholder] 데미지 숫자 표시 실패(전투 계속): {fxEx.Message}");
            }

            // [TEST27-68차] 몬스터 어그로 → 병사 — 병사가 때리면 몬스터의 근접 공격 대상이 병사로 향한다
            // (AnimalAI의 GetAliveAggroDamageable 경로 재사용 — 몬스터가 병사를 공격 → 병사 HP바 하락/사망).
            if (_attackTarget is Component atc)
            {
                var monsterAI = atc.GetComponentInParent<AnimalAI>();
                if (monsterAI != null) monsterAI.NotifyAttacker(gameObject);
            }

            string targetName = (_attackTarget as Component) != null ? (_attackTarget as Component).name : "?";
            Debug.Log($"[GuardPlaceholder] {guardName} 근접 공격! 대상={targetName} dmg={damage:F1}");

            // [TEST28-69차] 킬 크레딧 → 병사 경험치/레벨업 — 내 타격으로 대상이 사망하면 EXP 획득(몬스터/병사 공통)
            // [O3 C-O3-02] 킬 크레딧 XP 동적화 — 고정 15 → 대상 레벨 기반 CalculateGuardKillXP(병사 1/3 지분)
            if (target != null && !target.IsAlive)
                AddEXP(ResolveKillXP(target));
        }

        // [TEST28-69차] 병사 킬 경험치 — [O3 C-O3-02] 킬 시 ResolveKillXP로 대상 레벨 기반 동적 계산하며,
        // KillExpPerTarget=15는 대상 레벨 판별 실패 시 폴백값(레벨업 필요치 = level×50, 레벨업 시 maxHP +10)
        const int KillExpPerTarget = 15;

        /// <summary>
        /// [O3 C-O3-02] 킬 크레딧 XP 동적 계산 — 대상 레벨 기반 CalculateGuardKillXP(병사 1/3 지분).
        /// 몬스터(AnimalAI)면 그 레벨, 병사(GuardPlaceholder)면 victim.level. 레벨 판별 불가 시 KillExpPerTarget(15) 폴백.
        /// </summary>
        private int ResolveKillXP(IDamageable target)
        {
            if (target is Component comp)
            {
                var animal = comp.GetComponentInParent<AnimalAI>();
                if (animal != null)
                    return MonsterLevelSystem.CalculateGuardKillXP(animal.Level); // 몬스터 — 그 레벨의 1/3 지분

                var guard = comp.GetComponentInParent<GuardPlaceholder>();
                if (guard != null)
                    return MonsterLevelSystem.CalculateGuardKillXP(guard.Level); // 병사 — victim.level
            }
            return KillExpPerTarget; // [O3] 레벨 판별 불가 → 고정 15 폴백
        }

        private int _exp;

        /// <summary>병사 경험치 추가 — level×50 도달마다 레벨업(maxHP +10, 풀회복, 데미지는 level×1.5 수식 자동 반영).</summary>
        public void AddEXP(int amount)
        {
            if (amount <= 0 || _isDead) return;
            _exp += amount;
            while (_exp >= level * 50)
            {
                _exp -= level * 50;
                level++;
                _maxHP += 10f;
                _currentHP = _maxHP;   // 레벨업 풀회복
                Debug.Log($"[GuardPlaceholder] ⬆️ {guardName} 레벨업! Lv.{level} (maxHP={_maxHP}, 다음 필요 EXP={level * 50})");
            }
        }

        /// <summary>
        /// 공격 대상 유효성 검사 — 살아있는 적 IDamageable만 허용.
        /// 자기 자신, 다른 병사(GuardPlaceholder), 플레이어는 공격 금지.
        /// </summary>
        private bool ValidateAttackTarget()
        {
            if (_attackTarget == null) return false;

            var dmg = _attackTarget as IDamageable;
            if (dmg == null || !dmg.IsAlive)
            {
                _attackTarget = null;
                return false;
            }

            GameObject go = _attackTarget.gameObject;
            // 자기 자신 또는 다른 병사(GuardPlaceholder)는 공격 금지
            if (go == gameObject || go.GetComponentInParent<GuardPlaceholder>() != null)
            {
                _attackTarget = null;
                return false;
            }
            // 플레이어(및 플레이어 하위 오브젝트)는 공격 금지
            if (_playerCache != null && go.transform.IsChildOf(_playerCache.transform))
            {
                _attackTarget = null;
                return false;
            }
            return true;
        }

        /// <summary>[69차 후속15] 적대 정책 — true면 이 병사는 플레이어와 내(포섭) 병사를 공격 대상으로
        ///   인정한다(ResolveAttackTarget 게이트). GuardHostilitySystem 적대 전환 시 true 세팅.</summary>
        public bool HostileToPlayerFaction { get; set; }

        /// <summary>명령 지점 주변(TARGET_SEARCH_RADIUS)에서 가장 가까운 유효한 적 IDamageable을 탐색해 캐싱한다.</summary>
        private void ResolveAttackTarget()
        {
            _attackTarget = null;

            Collider[] hits = Physics.OverlapSphere(_commandTargetPos, TARGET_SEARCH_RADIUS);
            float bestDist = float.MaxValue;
            Component bestComp = null;

            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                Component comp = dmg as Component;
                if (comp == null) continue;

                GameObject go = comp.gameObject;
                if (go == gameObject) continue; // 자기 자신

                var otherGuard = go.GetComponentInParent<GuardPlaceholder>();
                if (otherGuard != null)
                {
                    // [69차 후속15] 병사 대 병사 — 적대 정책 가드는 내(포섭) 병사만 공격 대상으로 허용
                    //   (기존: 모든 GuardPlaceholder 제외 → 적대 병사가 내 병사를 공격하지 못하는 뿌리).
                    if (HostileToPlayerFaction)
                    {
                        if (!otherGuard.CompareTag("RecruitedSoldier")) continue;
                    }
                    else continue;
                }
                else
                {
                    // 플레이어 — 적대 정책 가드는 공격 대상 허용(기존: 무조건 제외 → 선공 후 명령 해제의 뿌리)
                    if (!HostileToPlayerFaction && _playerCache != null
                        && go.transform.IsChildOf(_playerCache.transform)) continue;
                }

                float d = Vector3.Distance(_commandTargetPos, comp.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestComp = comp;
                }
            }

            _attackTarget = bestComp;
        }

        /// <summary>
        /// 지형 계약 (FarmPlot.TryGetSurfaceY와 동일 수식): 월드 표면 y = 1 + GetHeightAt(x, z, Plains, 42).
        /// TerrainGenerator 미초기화 등 예외 시 fallbackY(현재 y)를 유지해 텔레포트를 방지한다.
        /// </summary>
        private static float TryGetGroundY(float x, float z, float fallbackY)
        {
            try { return 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42); }
            catch { return fallbackY; }
        }

        // ===== C9-21: 전투 AI =====
        private bool _isInCombat = false;
        private float _combatTimer = 0f;

        public void SetInCombat(bool combat) { _isInCombat = combat; _combatTimer = 0f; if (_rigAnim != null) _rigAnim.SetState(combat ? AnimationState.Attack : AnimationState.Idle); }
        public bool IsInCombat => _isInCombat;
        public float CombatTimer => _combatTimer;
        public void UpdateCombatTimer(float delta) { if (_isInCombat) _combatTimer += delta; }
        public void ResetCombatTimer() { _combatTimer = 0f; }
        public bool IsRecruited => _isRecruited;
        public GuardRole Role { get => _role; set => _role = value; }
        public string StatusSummary => GuardStatusSystem.GetStatusSummary(this);

        // ===== P30-A: 적/아군 구분 + 문지기 =====
        /// <summary>적/아군 구분 — 포섭된 병사=아군(true), 그 외=적(false).</summary>
        public bool IsAlly => _isRecruited;

        /// <summary>문지기 여부 — 영지 출입 조건 판정은 후속 단계(Phase5)가 수행.</summary>
        public bool IsGatekeeper { get; set; }

        /// <summary>포섭 상태 설정 (GuardManager 등에서 호출)</summary>
        public void SetRecruited(bool recruited) { _isRecruited = recruited; }

        /// <summary>체력 직접 설정 (GuardManager 부활/회복)</summary>
        public void SetHP(float hp) { _currentHP = Mathf.Clamp(hp, 0, _maxHP); }

        /// <summary>부활 처리 (GuardResurrectionSystem에서 호출)</summary>
        public void Resurrect(float hpPercent = 0.1f)
        {
            _isDead = false;
            _currentHP = _maxHP * Mathf.Clamp01(hpPercent);
            gameObject.SetActive(true);
            _selectionMode = SelectionMode.None;
        }

        // ===== 장비 슬롯 (WeaponPartsSystem 연동) =====
        public PlayerInventory.ItemData WeaponItem { get; set; }
        public PlayerInventory.ItemData ShieldItem { get; set; }
        public PlayerInventory.ItemData HelmetItem { get; set; }
        public PlayerInventory.ItemData ArmorItem { get; set; }
        public PlayerInventory.ItemData BootsItem { get; set; }
        public PlayerInventory.ItemData GlovesItem { get; set; }

        /// <summary>장비 외형 업데이트 (WeaponPartsSystem 연동)</summary>
        public void UpdateVisual() { }

        /// <summary>리스폰 처리 (TerritoryBattleManager 연동)</summary>
        public void Respawn() { Resurrect(0.1f); }
    }
}