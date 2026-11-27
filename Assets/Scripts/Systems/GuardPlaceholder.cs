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

        [Header("실내 배치")]
        [SerializeField] private int _indoorBedIndex = -1;   // [37차] 실내 침대 배치 번호(1..20, -1=미배치) — 병사배치 UI에서 각각 재배치

        // 가스 반응 — 방독면 부착 여부. 미착용(false)이면 가스에 노출 시 경직 반응.
        [Header("방독면 (가스 반응)")]
        [SerializeField] private bool _isGasMaskEquipped = false;

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

        private enum SelectionMode { None, SelectingFood, SelectingDrug, SelectingEquipment }   // [202차] 장비 착용 모드
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
            // [AI 고용 복원] 저장본에서 복원된 병사는 HP를 만피로 덮어쓰지 않는다.
            if (!_restoredFromRoster) _currentHP = _maxHP;

            // 플레이어 캐싱
            _playerCache = GameObject.FindGameObjectWithTag("Player");

            // 기본 Idle 애니메이션
            if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);

            // C32-04~06: 병사 장비 자동 생성 및 장착
            GuardEquipmentSpawner.SpawnEquipment(gameObject, level);

            // [2026-09-17] 랜덤 스탯 배분 — 먼저 롤된 장비와 무관하게, 레벨 기반 총합을 4개 능력치로 분배.
            // 저장본 복원 병사는 리롤하지 않는다(ApplyPersistentRosterEntry 값 유지).
            if (!_restoredFromRoster) RollStats();
        }

        /// <summary>공격 쿨다운 감소(0 하한). 순수 함수 — 테스트 가능한 시간 입력(delta) 분리.</summary>
        private static float TickAttackCooldown(float cooldown, float delta)
        {
            return cooldown > 0f ? Mathf.Max(0f, cooldown - delta) : cooldown;
        }

        private void Update()
        {
            // [TEST28-69차] 사망 시 업데이트 정지 — 쓰러짐 연출 중 시체가 명령/이동하지 않도록
            if (_isDead) return;

            // 캐시된 참조 갱신 (null이거나 비활성화된 경우 재탐색)
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            var player = _playerCache;

            // 홀드 등 명령 없음 상태 — ExecuteMovement가 !_hasCommand에서 조기 반환하므로 여기서 쿨다운을 감소시킨다(명령 보유 시는 ExecuteMovement가 1회만 감소).
            if (!_hasCommand) _attackCooldown = TickAttackCooldown(_attackCooldown, Time.deltaTime);

            // C9-20/C9-21: 명령 실행 루프 — 플레이어 부재와 무관하게 RTS/전투 명령(이동·공격)을 수행한다
            ExecuteMovement();

            // [15차] 홀드 자동 반격 — 제자리 고정 상태에서 근접 사거리 내 적만 스캔(0.2s 간격)
            if (_isHolding)
            {
                _holdScanTimer -= Time.deltaTime;
                if (_holdScanTimer <= 0f)
                {
                    _holdScanTimer = 0.2f;
                    TryHoldEngage();
                }
            }

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
                    : _selectionMode == SelectionMode.SelectingEquipment
                    ? slot.item.category == PlayerInventory.ItemCategory.Weapon
                      || slot.item.category == PlayerInventory.ItemCategory.Armor   // [202차] 무기 16종+투구/갑옷/신발/장갑/방패(shield=Armor)
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
                SetStatusMessage("아이템이 부족합니다.");
                return;
            }

            PlayerInventory.Instance.RemoveItem(item.id);

            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Food:
                    float heal = 5f + item.displayName.Length * 0.5f;
                    _currentHP = Mathf.Min(_maxHP, _currentHP + heal);
                    GuardLoyaltySystem.GiveGift(this, 30);
                    SetStatusMessage($"{guardName}: \\\"음식 고맙다!\\\" ❤️ 호감도 UP");
                    break;

                case PlayerInventory.ItemCategory.Potion:
                    _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    SetStatusMessage($"{guardName}: \\\"약을 주다니 고맙군!\\\" ❤️ 호감도 UP");
                    break;

                case PlayerInventory.ItemCategory.Drug:
                    GuardLoyaltySystem.GiveDrug(this, 2);
                    SetStatusMessage($"{guardName}: \\\"어.. 뭔가 이상한 기분이...\\\" 💊 중독+10");
                    break;
            }
        }

        // ===== [P13] UTK 상호작용 창용 public 래퍼 — 기존 private 로직 재사용(데이터/규칙 불변) =====
        /// <summary>병사 상태 메시지 (SoldierInteractUTK 표시용 — 말걸기/지급/포섭 응답 등).</summary>
        public string StatusMessage => _statusMessage;

        // ===== [73차 Phase B] 상태 메시지 이벤트 + 세터 래핑 — 월드 말풍선(GuardTalkBubble) 피드백 경로 =====
        /// <summary>
        /// [73차 Phase B] 상태 메시지 변경 이벤트 — SetStatusMessage 경유 변경마다 발화.
        /// GuardTalkBubble(월드 말풍선)가 구독해 대상 머리 위에 4초 표시한다(기존 로직 무변경 — 대입 방식만 교체).
        /// </summary>
        public event System.Action<string> StatusMessageChanged;

        /// <summary>[73차 Phase B] _statusMessage 대입 유일 경로 — 직접 대입 금지. 변경 시 StatusMessageChanged 발화.</summary>
        private void SetStatusMessage(string message)
        {
            _statusMessage = message ?? string.Empty;
            StatusMessageChanged?.Invoke(_statusMessage);
        }

        /// <summary>🗣️ 말걸기 — 기존 OnTalk 경로.</summary>
        public void BeginTalk() { OnTalk(); }

        /// <summary>🤝 포섭 — 기존 OnRecruit 경로 (GuardRecruitSystem 규칙 불변).</summary>
        public void BeginRecruit() { OnRecruit(); }

        /// <summary>🥩 음식 선택 모드 진입 — 기존 _selectionMode 세팅만 수행 (UTK 창이 호출).</summary>
        public void BeginFoodSelection() { _selectionMode = SelectionMode.SelectingFood; }

        /// <summary>💊 약 선택 모드 진입 — 기존 _selectionMode 세팅만 수행 (UTK 창이 호출).</summary>
        public void BeginDrugSelection() { _selectionMode = SelectionMode.SelectingDrug; }

        /// <summary>[202차] 🛡️ 장비 선택 모드 진입 — EquipAllyItem(P30-C) UI 배선용. 아군 전용(UTK가 IsAlly 게이트).</summary>
        public void BeginEquipmentSelection() { _selectionMode = SelectionMode.SelectingEquipment; }

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
                SetStatusMessage($"{guardName}: 버프 효과가 사라졌다.");
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
            if (!IsAlly) { SetStatusMessage("아군병사에게만 물약 버프를 줄 수 있습니다."); return; }
            if (item == null) return;
            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.HasItem(item.id)) { SetStatusMessage("아이템이 부족합니다."); return; }
            inv.RemoveItem(item.id);

            if (item.category == PlayerInventory.ItemCategory.Drug)
            {
                // 아군에게 마약 — 중독 금지(적병사 전용 규칙). 소량 회복만.
                _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
                GuardLoyaltySystem.GiveGift(this, 30);
                SetStatusMessage($"{guardName}: \"이런 건 좀... 그래도 고맙군.\"");
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
                    SetStatusMessage($"{guardName}: \"몸에 힘이 넘치는군!\" ⚡ 버프 ON ({eff.buffSeconds:0}초)");
                }
                else
                {
                    // 즉효 물약 — 즉시 회복만 (potion_hp_small/big)
                    _currentHP = Mathf.Min(_maxHP, _currentHP + eff.healFlat + _maxHP * eff.healPercent);
                    GuardLoyaltySystem.GiveGift(this, 50);
                    SetStatusMessage($"{guardName}: \"약을 주다니 고맙군!\" ❤️ 호감도 UP");
                }
                return;
            }

            // 미등록 물약 — 기존 GiveItemToGuard.Potion 케이스와 동일 소량 회복 폴백
            _currentHP = Mathf.Min(_maxHP, _currentHP + 10f);
            GuardLoyaltySystem.GiveGift(this, 50);
            SetStatusMessage($"{guardName}: \"약을 주다니 고맙군!\" ❤️ 호감도 UP");
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
            if (!IsAlly) { SetStatusMessage("적병사에게는 장비를 직접 등록할 수 없습니다."); return false; }
            if (item == null) return false;

            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.HasItem(item.id)) { SetStatusMessage("아이템이 부족합니다."); return false; }

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
                    SetStatusMessage($"{item.displayName}: 병사가 착용할 수 있는 장비가 아닙니다.");
                    return false;
            }

            inv.RemoveItem(item.id);
            if (prev != null) inv.AddItem(prev);   // 교체된 이전 장비 반환(GuardEquipmentSystem.ReturnToInventory 동일 규칙)
            SetStatusMessage($"{guardName}: {item.displayName} 장착 완료!");
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
                    SetStatusMessage($"{guardName}: '낯... 머리가 아득하다... 네가 뭐래도 따르겠다...'");
                    return;
                }

                // 호감도 4단계 (GuardLoyaltySystem.GetAffinityGrade — 호감>=60 / 보통 30~60 / 경계 0~30 / 위험<0)
                switch (GuardLoyaltySystem.GetAffinityGrade(_loyalty))
                {
                    case AffinityGrade.Friendly:
                        SetStatusMessage($"{guardName}: 호의적인 미소로 '어서 오게, 잘 지내고 있소.'");
                        return;
                    case AffinityGrade.Normal:
                        SetStatusMessage($"{guardName}: '무슨 일이냐? 말해 보게.'");
                        return;
                    case AffinityGrade.Cautious:
                        SetStatusMessage($"{guardName}: 경계하며 '...멀리서 말하게.'");
                        return;
                    case AffinityGrade.Danger:
                        SetStatusMessage($"{guardName}: 노려보며 '가까이 오지 마라!'");
                        return;
                }
            }

            SetStatusMessage(guardName + ": \\\"무슨 일이냐?\\\"");
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
            SetStatusMessage($"{guardName}: '똑똑하군...' 💰 뇌물 {cost}골드 → 호감도 {Loyalty:F0}");
            return true;
        }

        // ===== C9-15: 포섭 =====
        private void OnRecruit()
        {
            if (_isRecruited)
            {
                SetStatusMessage($"{guardName}: \\\"이미 영지에 소속되어 있네.\"");
                return;
            }

            var result = GuardRecruitSystem.AttemptRecruit(this);
            if (result.success)
            {
                // 입단비 결제 성공 시에만 편입 확정 (결제 실패 → 상태 불변)
                if (!SoldierWageSystem.TryChargeGuardAdmissionFee(this, result.method, out _))
                {
                    SetStatusMessage($"{guardName}: 입단비가 부족하오.");
                    return;
                }
                _isRecruited = true;

                // [119차] 방랑자 포섭 — 방랑 상태 해제(제자리에서 아군 복귀)
                if (IsWanderer) EndWanderer(transform.position);

                // P30-A: 중독 임계 포섭(method="addiction") → 호감도 MAX — 중독으로 절대복종
                if (result.method == "addiction")
                    Loyalty = GuardLoyaltySystem.MAX_LOYALTY;

                SetStatusMessage(result.message);
            }
            else
            {
                SetStatusMessage(result.message);
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

            // [2026-10-08] 가스 반응 — 방독면 미착용이면 경직(플린치) 반응.
            // (몬스터는 자체 HitReaction 경로로 이미 가스에도 반응하므로 병사만 보정.)
            GasHitHelper.TryApplyGuardGasStun(this, hitDirection, weaponType);

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
            // 입단비 결제 실패(골드 부족) 시 포획 보류 → 일반 사망 처리로 진행(상태 불변).
            if (IsWartimeCapturable && !_isRecruited && SoldierWageSystem.TryChargeGuardAdmissionFee(this, "capture", out _))
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

        /// <summary>입단비 변동 해시용 개체 ID — 생성 순서 기반 결정론(세션 내). 영속 저장은 미구현.</summary>
        private static int _stableIdSeq;
        private string _stableId;
        public string StableId => _stableId ??= "G" + (++_stableIdSeq).ToString("D6");
        public int Level => level;
        public string Nation => nation;
        public string JobTitle { get => jobTitle; set => jobTitle = value; }
        public float HP => _currentHP;
        public float Loyalty { get => _loyalty; set => _loyalty = Mathf.Clamp(value, -100, 100); }

        // ===== [09-28] 영지 난이도 기반 장비 희귀도 보너스 (TerritoryBuilder.CreateGuard가 설정) =====
        // 0=Ring1, 1=Ring2, 2=Ring3, 3=Ring4, 4=Empire. 장비 등급 승격 용도.
        private int _gearDifficultyBonus = 0;
        public int GearDifficultyBonus { get => _gearDifficultyBonus; set => _gearDifficultyBonus = Mathf.Clamp(value, 0, 4); }

        // ===== [AI 고용 영속화] AI 고용 병사 식별/스탯 복원 API =====
        /// <summary>AI 고용 시장이 고용한 병사 여부 — true인 병사만 ToPersistentRosterEntry로 내보낸다.</summary>
        private bool _isAIHired;
        public bool IsAIHired { get => _isAIHired; set => _isAIHired = value; }

        /// <summary>Start()의 HP 초기화/스탯 리롤을 건너뛰게 하는 플래그 (ApplyPersistentRosterEntry로 복원된 병사).</summary>
        private bool _restoredFromRoster;

        /// <summary>
        /// AI 고용 병사의 현재 상태를 로스터 DTO로 내보낸다. AI 고용 병사가 아니면 null.
        /// 스탯은 장비/버프 보정 전 순수 롤 값(ATK/DEF/VIT/AGI)으로 저장해 재로드 시 이중 가산을 막는다.
        /// </summary>
        public AIHiredGuardSaveEntry ToPersistentRosterEntry(string territoryId)
        {
            if (!_isAIHired) return null;
            return new AIHiredGuardSaveEntry
            {
                stableId = StableId,
                territoryId = territoryId ?? string.Empty,
                guardName = guardName,
                level = level,
                nation = nation,
                hp = _currentHP,
                maxHP = GetMaxHP(),
                loyalty = _loyalty,
                atk = GetStatAttack(),
                def = GetStatDefense(),
                vit = GetStatVitality(),
                agi = GetStatAgility(),
                isAlive = !_isDead,
                gearDifficulty = _gearDifficultyBonus
            };
        }

        /// <summary>
        /// 저장된 AI 고용 병사 DTO를 이 병사에 적용한다(리롤 없이 정체성/스탯/HP 복원).
        /// Start 전/후 어느 쪽에서 호출해도 안전하다. 복원된 StableId 번호만큼 _stableIdSeq를 끌어올려
        /// 이후 신규 발급 ID가 복원 ID와 겹치지 않게 한다.
        /// </summary>
        public void ApplyPersistentRosterEntry(AIHiredGuardSaveEntry e)
        {
            if (e == null) return;

            _restoredFromRoster = true;
            _isAIHired = true;

            // 정체성 + ID 충돌 방지 (seq bump)
            if (!string.IsNullOrEmpty(e.stableId))
            {
                _stableId = e.stableId;
                if (e.stableId.Length > 1 && e.stableId[0] == 'G'
                    && int.TryParse(e.stableId.Substring(1), out int n) && n > _stableIdSeq)
                    _stableIdSeq = n;
            }
            if (!string.IsNullOrEmpty(e.guardName)) guardName = e.guardName;
            if (!string.IsNullOrEmpty(e.nation)) nation = e.nation;
            level = Mathf.Max(1, e.level);
            _loyalty = Mathf.Clamp(e.loyalty, -100f, 100f);
            _gearDifficultyBonus = Mathf.Clamp(e.gearDifficulty, 0, 4);

            // 스탯 — 리롤 금지, 롤 완료 상태로 표시
            _statAttack = Mathf.Max(0, e.atk);
            _statDefense = Mathf.Max(0, e.def);
            _statVitality = Mathf.Max(0, e.vit);
            _statAgility = Mathf.Max(0, e.agi);
            _statsRolled = true;

            // GetMaxHP() = _maxHP + vit*2 역산
            _maxHP = Mathf.Max(1f, e.maxHP - _statVitality * 2f);

            // HP / 생존
            _isDead = !e.isAlive;
            _currentHP = _isDead ? 0f : Mathf.Clamp(e.hp, 0f, GetMaxHP());
        }

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

        /// <summary>[125차] 훈련 완료 적용 — 레벨 +1 및 능력치 가산(TrainingHallSystem 전용 진입점).
        /// MaxHP는 기존 산식(e.maxHP - _statVitality*2f, L903)과 등가로 _maxHP += vitGain*2f 가산(기저 유지).</summary>
        public void ApplyTrainingLevelUp(int atkGain, int defGain, int vitGain)
        {
            EnsureStatsRolled();
            level++;
            _statAttack += Mathf.Max(0, atkGain);
            _statDefense += Mathf.Max(0, defGain);
            _statVitality += Mathf.Max(0, vitGain);
            _maxHP += Mathf.Max(0, vitGain) * 2f;
            _currentHP = Mathf.Min(_currentHP + Mathf.Max(0, vitGain) * 2f, _maxHP);
        }

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

            // [2026-10-11 사용자 요구 — 링 단일화] 레거시 SpecialEffectsController 선택 아웃라인
            //   (평평한 Cylinder 디스크) 호출 폐기 — 파란 SelectionRing 외의 이중 표시 제거.
            //   2026-09-30 사용자 시각 계약: 선택 병사 발밑 = 파란 링 하나만.
        }
        public bool IsSelected => _isSelected;
        public void SetCommandTarget(Vector3 t, bool a)
        {
            _isHolding = false;   // [15차] 새 명령이 홀드를 해제한다
            _commandTargetPos = t;
            _isAttackCommand = a;
            _hasCommand = true;
            _lastCommandTarget = t;   // [53차] 새 명령 = 새 정체 관측 창
            // [무번호 28-S2] 이동 시작 킥 — 이동 명령 수락 순간 발밑 퍼프 1회(도착 퍼프와 대칭 완성).
            //   20-Q2 플레이어 선례 준용: 이동 명령 전용(a==false) + 1.5m 거리 게이트(제자리 재명령 스팸 방지).
            //   거리 게이트 = 1.5f * 1.5f(제곱 거리) — [무번호 22-Q2] 배선 원형 보존(아래 기존 블록 유지).
            // [무번호 22-Q2] 이동 시작 킥 — 새 **이동** 명령(공격 제외)이 1.5m 이상 떨어진 목표면
            //   발밑 먼지 킥 1회(도착 퍼프 20-I2와 대칭 — 출발/도착 프레임 완성). 런타임 전용.
            if (!a)
            {
                Vector3 here = transform.position;
                if ((t - here).sqrMagnitude >= 2.25f)
                {
                    Vector3 gp = here;
                    gp.y = TryGetGroundY(gp.x, gp.z, gp.y);
                    // [무번호 29-S2-2] 시작 킥 강도 = 목표 거리 비례(가속 연동) — 슬롯 선행 설정(잠금 호출 문자열 보존).
                    ShockwaveRingFX.SetNextPuffIntensity(Mathf.Clamp(0.8f + (t - here).sqrMagnitude / 36f, 0.8f, 1.5f));
                    try { ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(gp.x, gp.y + 0.06f, gp.z)); } catch (System.Exception) { }
                }
            }
            // A new order designates a new target. Do not briefly display an old resolved
            // combat target while this order waits for its first execution update.
            _attackTarget = null;
            _attackTargetOffset = Vector3.zero;
            _hasResolvedAttackTarget = false;
            _explicitAttackTarget = null;   // An ordinary command drops any explicit entity focus.
        }
        public void ClearCommand()
        {
            _hasCommand = false;
            _isAttackCommand = false;
            _moveStuckTimer = 0f;
            _lastMoveDistXZ = float.MaxValue;
            _attackTarget = null;
            _attackTargetOffset = Vector3.zero;
            _hasResolvedAttackTarget = false;
            _explicitAttackTarget = null;
        }

        /// <summary>
        /// Phase 2C explicit focus — attack command anchored at <paramref name="anchor"/> that pins the given
        /// entity as the attack target (not the nearest enemy). Non-Component targets fall back to a plain
        /// attack command at the anchor. The entity is re-validated by the normal attack rules each resolve;
        /// if it dies or becomes invalid the explicit identity is dropped and nearest-enemy retarget resumes.
        /// </summary>
        public void SetExplicitAttackTarget(IDamageable target, Vector3 anchor)
        {
            SetCommandTarget(anchor, true);
            _explicitAttackTarget = target as Component;
        }
        public bool HasCommand => _hasCommand;
        public Vector3 CommandTarget => _commandTargetPos;

        // ===== [15차 사용자 확정] 지휘 핫키 — H=홀드(제자리 방어) / X=스톱(선택 시만) =====
        private bool _isHolding;
        private float _holdScanTimer;
        private Vector3 _lastCommandTarget;   // [53차] 정체 오탐 방지 — 명령 목표 이동 추적
        private float _holdNewTargetDelay;    // [61차] 홀드 반격 초기 딜레이 잔여 시간(초) — 새 타겟 최초 감지 시 0.4s
        private Component _holdLastTarget;    // [61차] 직전 홀드 반격 타겟 — 동일 타겟 재감지 시 딜레이 재무장 방지

        /// <summary>홀드 — 제자리 고정 + 사정거리 내 적 자동 반격(후퇴/추격 금지). 새 명령이 홀드를 해제한다.</summary>
        public bool IsHolding => _isHolding;

        public void SetHold(bool hold)
        {
            _isHolding = hold;
            if (hold)
            {
                ClearCommand();
                SetInCombat(false);
                if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
                // [61차] 재홀드 시 반격 초기 딜레이 재적용 — 이전 홀드의 타겟 기억/잔여 딜레이를 제거해
                //   홀드 해제 후 재홀드해도 새 타겟 관찰 딜레이가 정상 적용된다.
                _holdLastTarget = null;
                _holdNewTargetDelay = 0f;
            }
        }

        /// <summary>스톱 — 이동/공격/홀드 즉시 취소 + 제자리 유휴.</summary>
        public void StopNow()
        {
            _isHolding = false;
            ClearCommand();
            SetInCombat(false);
            if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
        }

        /// <summary>홀드 자동 반격 — 제자리에서 근접 사거리 내 적만 쿨다운 게이트 교전(이동 없음).</summary>
        // [64차 P1-B] 수비병 반격 0건 판별 계측 — 같은 병사×같은 태그 1회만(경고 스팸 방지).
        private static readonly System.Collections.Generic.HashSet<int> _holdDiagLogged = new System.Collections.Generic.HashSet<int>();
        private void HoldDiagOnce(string tag)
        {
            int key = (GetInstanceID() * 17 + tag.Length) & 0x3FFFFFFF;
            if (_holdDiagLogged.Contains(key)) return;
            _holdDiagLogged.Add(key);
            Debug.Log($"[홀드계측] {guardName} {tag}");
        }

        private void TryHoldEngage()
        {
            HoldDiagOnce(_attackCooldown > 0f ? "스캔 — cooldown>0 대기" : "스캔 — 준비");
            if (_attackCooldown > 0f) return;
            Collider[] hits = Physics.OverlapSphere(transform.position, ATTACK_MELEE_RANGE);
            Component best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                var comp = dmg as Component;
                if (comp == null || comp.gameObject == gameObject) continue;
                var otherGuard = comp.GetComponentInParent<GuardPlaceholder>();
                // 홀드 반격은 현재 병사의 전투 진영과 다른 국가의 병사에게만 허용한다.
                // HostileToPlayerFaction은 플레이어 관계 플래그이지 동국가 아군 공격 허가가 아니다.
                if (otherGuard != null && !IsEnemyGuard(otherGuard)) continue;
                float d = Vector3.Distance(transform.position, comp.transform.position);
                if (d < bestDist) { bestDist = d; best = comp; }
            }
            if (best == null) HoldDiagOnce("적 미감지 — OverlapSphere 사거리 내 대상 없음");
            if (best == null) return;
            FaceToward(best.transform.position - transform.position, Time.deltaTime);
            // [61차] 홀드 반격 초기 딜레이 — 새 타겟은 0.4s 관찰 후 반격(도착 정렬 시간 확보 — Test_16 실측 밸런스:
            //   병사 도착 ≤0.2s 반격 선공으로 3타 전 사망). FaceToward는 유지(적 방향 바라보기는 즉시), 공격만 딜레이.
            if (_holdLastTarget != best) { _holdLastTarget = best; _holdNewTargetDelay = 0.4f; HoldDiagOnce("새 타겟 감지 — 0.4s 관찰 딜레이 진입"); }
            if (_holdNewTargetDelay > 0f) { _holdNewTargetDelay -= Time.deltaTime; return; }
            HoldDiagOnce("딜레이 경과 — 반격 실행 시도");
            if (_attackCooldown <= 0f) PerformAttack((IDamageable)best);
        }
        public bool IsAttackCommand => _isAttackCommand;
        /// <summary>Resolved execution target for the current attack command; null until target resolution succeeds.</summary>
        public Component CurrentAttackTarget => _hasCommand && _isAttackCommand ? _attackTarget : null;

        // ===== C9-20/C9-21: 명령 실행 루프 =====
        // [2026-10-11 사용자 요구 — 지정 포인트 정확 도착] 기존 1.0/0.6m는 지정점 0.6~1.0m 손전 정지의 원인.
        private const float MOVE_CLEAR_RADIUS = 0.25f;      // 이동 명령 해제 반경(m) — 지정점 ±0.25m 이내 도착
        // [무번호 20-I1] 트레일 먼지 간격 — 이동 거리 0.6m마다 1회 스폰(프레임마다 스폰 폐기 — 연속 자취+동시 스폰 수 급감)
        private const float TrailDustGap = 0.6f;
        private const float TrailDustGapSq = TrailDustGap * TrailDustGap;
        private Vector3 _lastTrailDustPos;
        private bool _hasTrailDustPos;
        private bool _stepFlip;        // [무번호 28-S1] 스텝 좌우 교차(발자국 리듬 — 결정론)
        private float _lastFrameSpeed;   // [무번호 29-S2-2] 직전 프레임 이동 속도(m/s) — 기동/정지 강도 연동
        private const float MOVE_STOP_RADIUS = 0.15f;       // 이동 정지 판정 반경(m) — 이내 접근 금지(목표 넘어감 방지)
        private const float MOVE_STUCK_CLEAR_SECONDS = 0.6f; // 이동 진행 정체 해제 시간(초) — collider 막힘 대비
        private int _stuckRetries;                     // [47차] 정체 우회 재시도 횟수(최대 2 — 실패 시 해제)
        private const float ATTACK_ARRIVE_RADIUS = 1.5f;    // 공격 명령 도달 반경(m) — 공격 모션 개시
        private const float ATTACK_MELEE_RANGE = 2.2f;      // 근접 공격 유효 거리(m)
        private const float ATTACK_COOLDOWN_SECONDS = 1.2f; // 공격 쿨다운(초)
        private const float ROTATION_SPEED = 8f;            // 회전 보간 속도 (Slerp 계수)
        private const float TARGET_SEARCH_RADIUS = 2.5f;    // 명령 지점 주변 적 탐색 반경(m)

        private Component _attackTarget;                    // 공격 명령 대상 (IDamageable 구현 컴포넌트 캐시)
        private Component _explicitAttackTarget;            // 명시적 집중 대상(SetExplicitAttackTarget) — null이면 최근접 적 탐색
        private readonly List<Component> _attackCandidates = new List<Component>(); // Phase 2C.3 후보 버퍼(재사용, 할당 없음)
        private float _lastMoveDistXZ = float.MaxValue;     // [2026-10-11] 이동 정체 감지 — 직전 프레임 목표 거리
        private float _moveStuckTimer;                      // [2026-10-11] 거리 무감소 누적 시간
        private Vector3 _attackTargetOffset;                 // 이동 타겟의 현재 위치 기준 공격 접근점 오프셋
        private bool _hasResolvedAttackTarget;               // 명령 중 실제 타겟을 한 번이라도 확보했는지
        private float _attackCooldown = 0f;                 // 공격 쿨다운 잔여 시간(초)
        private HumanoidClipDriver _clipDriver;             // 공격 모션 드라이버 (지연 캐싱)
        private Rigidbody _rb;                              // Rigidbody (없으면 transform 직접 이동)

        /// <summary>
        /// C9-20/C9-21: 명령 실행 루프 — RTS/전투 명령을 실제 이동·공격으로 수행한다.
        /// GuardPlaceholder는 Rigidbody 없는 단순 생성 프리팹(cube)이므로 transform 이동을 허용하되,
        /// Rigidbody가 존재하면 MovePosition으로 우회한다. 사망 시 어떤 행동도 하지 않는다.
        /// </summary>
        public void ExecuteMovement()
        {
            if (_isDead || !_hasCommand) return;
            // [P14] 실내 활성 시 이동 명령 정지 — 병사가 실내 좌표로 걸어 들어오는 것 차단
            // [39차] 예외: 배치 UI가 의도한 명령(출동/수비 보행)은 허용 — IndoorMoveExempt.
            if (ProjectName.Core.UITransitionState.IndoorActive && !IndoorMoveExempt) return;

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
                    if (_hasResolvedAttackTarget)
                    {
                        // 지정 타겟이 사망/무효화되어도 공격 명령은 유지한다. 현재 대상 참조만 비우고
                        // 명령 지점 주변에서 다음 유효한 적을 재탐색한다(없으면 명령 지점에서 대기).
                        Debug.Log($"[GuardPlaceholder] {guardName} 공격 대상 사망/상실 → 명령 유지, 주변 재탐색");
                        _attackTargetOffset = Vector3.zero;
                        _hasResolvedAttackTarget = false;
                    }

                    // 타겟이 아직 검색되지 않은 명시적 공격 명령은 버리지 않는다.
                    // 명령 지점으로 계속 이동하고, 다음 프레임에 주변 타겟을 재탐색한다.
                    ResolveAttackTarget();
                    if (_attackTarget != null)
                    {
                        _commandTargetPos = _attackTarget.transform.position + _attackTargetOffset;
                        target = _commandTargetPos;
                        toTarget = target - current;
                        toTarget.y = 0f;
                        distXZ = toTarget.magnitude;
                    }
                }

                // 이미 유효한 타겟이 이동한 경우에도 같은 접근 오프셋을 유지하며 추적한다.
                if (_attackTarget != null)
                {
                    _commandTargetPos = _attackTarget.transform.position + _attackTargetOffset;
                    target = _commandTargetPos;
                    toTarget = target - current;
                    toTarget.y = 0f;
                    distXZ = toTarget.magnitude;
                }

                if (distXZ > ATTACK_ARRIVE_RADIUS)
                {
                    // 미도달 → 목표지점으로 이동
                    StepToward(current, target, distXZ, delta);
                }
                else if (_attackTarget != null)
                {
                    // 도달 → 대상 방향 회전 후 쿨다운 게이트 공격
                    FaceToward(_attackTarget.transform.position - current, delta);

                    Vector3 dirToTarget = _attackTarget.transform.position - current; dirToTarget.y = 0f;
                    if (_attackCooldown <= 0f && dirToTarget.magnitude <= ATTACK_MELEE_RANGE)
                        PerformAttack((IDamageable)_attackTarget);
                }
                // 아직 타겟이 없는 공격 명령은 도착 지점에서 대기하며 다음 프레임에 다시 탐색한다.
            }
            else
            {
                // ----- 이동 명령: 도달 반경 0.25m 진입 시 명령 해제 -----
                if (distXZ <= MOVE_CLEAR_RADIUS)
                {
                    ClearCommand();
                    ClearIndoorMoveExemptOnArrival();   // [43차] 실내 예외 플래그 정리(도착 시)
                    // [무번호 20-I2] 도착 먼지 퍼프 — 이동 명령 완료 순간 1회 소형 확산(F1 트레일과 별개 신규 FX).
                    //   트레일 간격 플래그 리셋 — 다음 이동 첫 걸음부터 즉시 자취 시작.
                    _hasTrailDustPos = false;
                    {
                        Vector3 gp = transform.position;
                        gp.y = TryGetGroundY(gp.x, gp.z, gp.y);
                        // [무번호 29-S2-2] 도착 강도 = 도착 직전 속도 비례(감속 연동 — approach 감속 변수 계승).
                        ShockwaveRingFX.SetNextPuffIntensity(Mathf.Clamp(0.7f + _lastFrameSpeed / 3.5f, 0.7f, 1.5f));
                        try { ShockwaveRingFX.SpawnArrivalPuff(new Vector3(gp.x, gp.y + 0.02f, gp.z)); } catch (System.Exception) { }
                    }
                    if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
                    // [59차] 도착 즉시 정지 — 드라이버 스무딩/홀드 스냅(제자리걸음 잔상 제거).
                    //   도착 1회 실행이라 GetComponent 캐시 불필요.
                    var driver = GetComponent<HumanoidClipDriver>();
                    if (driver != null) driver.NotifyStop();
                }
                else
                {
                    StepToward(current, target, distXZ, delta);
                    // [2026-10-11 이동 정확도] 진행 정체 감지 — collider 막힘 등으로 목표 거리가 줄지 않으면
                    // 명령을 해제한다(영구 멈춤/잔존 지속 마커 방지). 도착 판정은 기존 XZ 반경 계약 유지.
                    // [47차 수리 — "여전히 안 움직이는 병사"] 기존 0.6s 정체 → 즉시 명령 해제는
                    //   침대 열 사이/가구 옆에 집결 슬롯이 걸린 병사가 그대로 서 있게 만든 원인.
                    //   수리: 해제 전 **우회 재시도 2회**(목표를 ±90° 측면 0.8m 밀어 재명령) —
                    //   둘 다 실패 시에만 해제(기존 동작). 실내 배치 이동(IndoorMoveExempt)은
                    //   사용자 의도 명령이라 포기 금지 — 무한 재시도 대신 마지막에 로그로 남긴다.
                    // [53차 수리 — "이동이 자꾸 끊긴다"] 명령 목표가 이동 중(플레이어 추종 재발행·타겟 추적)이면
                    //   distXZ 기준점이 매번 바뀌어 정체 오탐 → 우회 반복 → 끊김의 뿌리. 목표가 전 프레임 대비
                    //   0.5m 이상 이동했으면 정체가 아니라 추적이므로 타이머 리셋.
                    if ((target - _lastCommandTarget).sqrMagnitude > 0.25f)
                    {
                        _moveStuckTimer = 0f;
                        _stuckRetries = 0;
                        _lastCommandTarget = target;
                    }
                    if (distXZ < _lastMoveDistXZ - 0.001f)
                    {
                        _moveStuckTimer = 0f;
                        _stuckRetries = 0;
                    }
                    else
                        _moveStuckTimer += delta;
                    _lastMoveDistXZ = distXZ;
                    if (_moveStuckTimer >= MOVE_STUCK_CLEAR_SECONDS)
                    {
                        if (_stuckRetries < 2)
                        {
                            _stuckRetries++;
                            // 우회 목표 — 진행 방향 기준 좌/우 교대로 90° 측면 0.8m
                            Vector3 dirXZ = (target - current); dirXZ.y = 0f;
                            if (dirXZ.sqrMagnitude > 0.0001f)
                            {
                                dirXZ.Normalize();
                                float sign = _stuckRetries % 2 == 1 ? 1f : -1f;
                                Vector3 side = new Vector3(-dirXZ.z, 0f, dirXZ.x) * sign;   // 진행 방향의 측면
                                _commandTargetPos = target + side * 0.8f;
                                _moveStuckTimer = 0f;
                                Debug.Log($"[GuardPlaceholder] {guardName} 이동 정체 → 우회 재시도 {_stuckRetries}/2 (측면 {0.8f:0.0}m)");
                            }
                        }
                        else
                        {
                            _stuckRetries = 0;
                            Debug.Log($"[GuardPlaceholder] {guardName} 이동 정체(우회 2회 실패) → 명령 해제 (목적지 {target.x:0.0},{target.z:0.0})");
                            ClearCommand();
                            if (_rigAnim != null) _rigAnim.SetState(AnimationState.Idle);
                        }
                    }
                }
            }
        }

        /// <summary>목표를 향해 1프레임 이동 (속도 * delta, 목표 넘어감 방지 클램프 + 지형 y 보정 + 회전).</summary>
        private void StepToward(Vector3 current, Vector3 target, float distXZ, float delta)
        {
            Vector3 dirXZ = target - current; dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            dirXZ.Normalize();

            // [59차] 도착 감속 — 마지막 0.6m에서 선형 감속(도착 하드스톱 → 0.5s 제자리걸음의 뿌리 제거).
            // MOVE_STOP_RADIUS(0.15) 계약 유지, 공격 명령은 ATTACK_ARRIVE_RADIUS(1.5)에서 먼저 분기하므로 영향 없음.
            float approach = Mathf.Clamp01((distXZ - MOVE_STOP_RADIUS) / 0.6f);
            // 이동량 = _moveSpeed * delta, 목표를 넘어가지 않도록 클램프 (정지 반경 0.6m 유지)
            float step = Mathf.Min(_moveSpeed * delta * approach, Mathf.Max(0f, distXZ - MOVE_STOP_RADIUS));
            if (step <= 0f) return;

            // [무번호 29-S2-2] 속도 추적 — 기동/정지 강도 연동용(감속(approach) 재사용 계약).
            _lastFrameSpeed = delta > 0.0001f ? step / delta : 0f;

            Vector3 next = current + dirXZ * step;
            // 지형 계약: 이동 y는 표면(1 + GetHeightAt)으로 보정 (FarmPlot.TryGetSurfaceY와 동일 수식)
            next.y = TryGetGroundY(next.x, next.z, current.y);

            // Rigidbody가 있으면 MovePosition, 없으면 transform 직접 이동
            if (_rb != null && !_rb.isKinematic) _rb.MovePosition(next);
            else transform.position = next;

            // [무번호 19-F1 + 20-I1] 이동 먼지 트레일 — 이동 중 발밑 흙색 먼지. 20-I1: 프레임마다 스폰 →
            //   **거리 간격(0.6m) 게이팅** — 연속 자취 연출 + 동시 스폰 수 급감(성능/가독). 실패 시 무음 스킵 유지.
            if (!_hasTrailDustPos || (next - _lastTrailDustPos).sqrMagnitude >= TrailDustGapSq)
            {
                _lastTrailDustPos = next;
                _hasTrailDustPos = true;
                // [무번호 19-F1 + 20-I1 + 21-K2] 지면 링(스윕) + 공기 입자 퍼프(부양) 2레이어 — 젤다식 먼지.
                //   0.6m 게이팅 유지 — 스폰 수 증가 없음(링 1 + 입자 5/지점).
                // [무번호 28-S1] 스텝 리듬 — 발자국 좌우 교차(이동 방향의 횡축 오프셋, 결정론).
                //   거리 게이팅(0.6m)·2레이어 구조 무손상 — 스폰 위치만 좌우로 벗겨 "걸음" 리듬을 만든다.
                Vector3 stepSide = new Vector3(-dirXZ.z, 0f, dirXZ.x) * (_stepFlip ? 0.12f : -0.12f);
                _stepFlip = !_stepFlip;
                // [무번호 29-S1-2] 스텝 페이즈 동기 — 드라이버 CurrentSpeed 조회만(애니 시스템 무수정 — 계획 계약).
                //   걷기 사이클 속도비를 먼지 강도에 반영: 애니가 빠를수록(발자국 촘촘) 강도 상승 — 결정론.
                float animSpeed = _rigAnim != null ? _rigAnim.CurrentSpeed : 0f;
                float speedRatio = _moveSpeed > 0.01f ? animSpeed / _moveSpeed : 1f;
                ShockwaveRingFX.SetNextPuffIntensity(Mathf.Clamp(0.7f + 0.6f * speedRatio, 0.7f, 1.5f));   // [무번호 29-S2-2]
                try
                {
                    ShockwaveRingFX.SpawnMoveTrailDust(new Vector3(next.x, next.y + 0.02f, next.z) + stepSide);
                    ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(next.x, next.y + 0.06f, next.z) + stepSide);
                }
                catch (System.Exception) { }
            }

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

            // [169-P6] 호 텔레그래프 + 2페이즈 — 예고 0.5s 표시 후 부채꼴 재판정 타격(95차 F5 몬스터 선례).
            //   회피: 예고 중 이탈(거리/호 밖) 시 빈 스윙, 타격 순간 구르기(C21-02 무적).
            Component arcTargetComp = target as Component ?? _attackTarget as Component;
            Vector3 arcDir = transform.forward;
            if (arcTargetComp != null)
            {
                Vector3 d = arcTargetComp.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) arcDir = d.normalized;
            }
            // [193차 제거] 몬스터 적색링(공격 텔레그래프 호) 폐지 — 사용자 지시("여전히 몬스터 적색링 남아있고").
            //   190차(AnimalAI·AfterlifeEnemy)에 이어 병사형(GuardPlaceholder) 잔존 스폰 삭제 — 시각만 제거.
            //   [169-P6] 호 판정(부채꼴 재판정)은 유지 — arcDir는 PerformAttackStrike 재판정 입력으로 그대로 사용.
            _attackCooldown = ATTACK_COOLDOWN_SECONDS + TelegraphArcIndicator.TelegraphSeconds;   // 예고 중 재발동 차단(타격 시 쿨다운 재설정)

            StartCoroutine(PerformAttackStrike(target, arcTargetComp, arcDir));
        }

        /// <summary>[169-P6] 병사 타격 2페이즈 — 예고 종료 후 거리/부채꼴 재판정 타격(빈 스윙) 또는 피해 적용.</summary>
        private System.Collections.IEnumerator PerformAttackStrike(IDamageable target, Component targetComp, Vector3 arcDir)
        {
            float elapsed = 0f;
            while (elapsed < TelegraphArcIndicator.TelegraphSeconds)
            {
                if (target is Component lt && !lt.gameObject.activeInHierarchy) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 타격 순간 재판정 — 이탈(거리/호 밖) 시 빈 스윙 [169-P6]
            if (targetComp == null || !targetComp.gameObject.activeInHierarchy) yield break;
            if (target != null && !target.IsAlive) yield break;   // [169-P6] 예고 중 사망 타겟 무타격
            Vector3 flatTo = targetComp.transform.position - transform.position; flatTo.y = 0f;
            if (flatTo.magnitude > ATTACK_MELEE_RANGE)
            {
                Debug.Log($"[GuardPlaceholder] 빈 스윙 — 예고 중 이탈 (거리 {flatTo.magnitude:F2} > 사정거리 {ATTACK_MELEE_RANGE:F2}) [169-P6]");
                yield break;
            }
            if (!TelegraphArcIndicator.IsInFrontArc(transform.position, arcDir, targetComp.transform.position, ATTACK_MELEE_RANGE))
            {
                Debug.Log($"[GuardPlaceholder] 빈 스윙 — 예고 중 호 밖 이탈 [169-P6]");
                yield break;
            }
            // 데미지 적용 (대상은 ValidateAttackTarget에서 유효성 검증 완료 상태)
            // [2026-09-17] 기존 level × 1.5 → 장착 무기를 반영한 GetAttack() 사용.
            // [56차 후속 — "적 체력이 안 준다" 실측 수리] 무장비(테스트 씬 GuardEquipmentSystem 미부팅)
            //   + 스탯 롤 하한 붕괴(Lv8 롤 1~5 — Editor.log 실측 dmg=1.0) → 타당 체력바 4%라 가시
            //   데미지가 성립하지 않았다. 하한 = level 복원: 롤/무기 보너스가 level을 넘으면 그대로,
            //   못 넘으면 level 보장(Lv8→최소 8, Lv9→9 — 상호 교전 양측 가시화).
            float damage = Mathf.Max(GetAttack(), level);
            // [163차 선수리] 구 지역 선언 삭제 — targetComp는 PerformAttackStrike 파라미터(L1466)로
            //   호출부가 target as Component ?? _attackTarget as Component를 주입(동일 의미).
            //   잔존 시 CS0136/CS0841 15건으로 전체 배치컴파일 차단. [169-P6] 로직 무변경.
            // [56차 후속] 홀드 반격(TryHoldEngage)은 로컬 타겟을 인자로 넘겨 _attackTarget 필드가 null —
            //   기존은 필드에서 방향/이름을 읽어 반격 로그 "대상=?" + 방향 폴백(transform.forward)이었다.
            //   인자로 받은 타겟 우선으로 방향·이름을 정확화(동작 변경 없음).
            Vector3 dir = transform.forward;
            if (targetComp != null)
            {
                Vector3 toTarget = targetComp.transform.position - transform.position; toTarget.y = 0f;
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

            string targetName = targetComp != null ? targetComp.name : "?";
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
            if (dmg == null || !dmg.IsAlive || !_attackTarget.gameObject.activeInHierarchy)
            {
                _attackTarget = null;
                return false;
            }

            GameObject go = _attackTarget.gameObject;
            // 자기 자신은 공격 금지
            if (go == gameObject)
            {
                _attackTarget = null;
                return false;
            }
            // [54차 근원 수리 — "남부 침공군이 한 걸음도 못 감"] 병사 대 병사 판정을 51차 국가 간
            //   교전 규칙(ResolveAttackTarget.IsEnemyGuard)과 **정합**시킨다. 기존: 모든
            //   GuardPlaceholder를 무조건 거부 → ResolveAttackTarget이 허용한 이국 병사 타겟이
            //   다음 프레임 여기서 즉시 무효화 → "공격 대상 사망/상실 → 명령 해제" 루프
            //   (이동진단 실측: 타겟 확정 후 pos 덤프 0회 = 이동 0의 뿌리).
            var otherGuard = go.GetComponentInParent<GuardPlaceholder>();
            if (otherGuard != null)
            {
                // [119차] 아웃로(방랑자)도 동국가 병사의 공격 대상으로 유효
                if (!IsEnemyGuard(otherGuard) && !otherGuard.HostileToPlayerFaction && !otherGuard.IsOutlawWanderer)
                {
                    _attackTarget = null;
                    return false;
                }
            }
            else
            {
                // 플레이어(및 플레이어 하위 오브젝트)는 공격 금지
                if (_playerCache != null && go.transform.IsChildOf(_playerCache.transform))
                {
                    _attackTarget = null;
                    return false;
                }
            }
            return true;
        }

        /// <summary>[69차 후속15] 적대 정책 — true면 이 병사는 플레이어와 내(포섭) 병사를 공격 대상으로
        ///   인정한다(ResolveAttackTarget 게이트). GuardHostilitySystem 적대 전환 시 true 세팅.</summary>
        public bool HostileToPlayerFaction { get; set; }

        // ===== [119차] 방랑자(아웃로) 상태 — 방출·감축·탈영으로 소속을 잃고 세계를 배회하는 병사 =====
        /// <summary>[119차] 방랑자 상태 — 소속 상실 병사가 세계를 배회하며 플레이어·영지를 공격한다(WandererDriver 구동).</summary>
        public bool IsWanderer { get; private set; }
        /// <summary>[119차] 아웃로 판정 — 방랑자는 모든 진영의 유효 공격 대상이며 스스로도 전 진영을 공격한다(국가 게이트 우회).</summary>
        public bool IsOutlawWanderer => IsWanderer;

        /// <summary>
        /// [119차] 방랑자 전환 — 소속 상실(방출/AI 감축/탈영) 병사를 세계 배회 상태로 전환.
        ///  · Territory_ 계층 → 씬 루트 재부모(영지 재구축 파괴·AI 경제 계층 스캔 이탈 방지)
        ///  · 문지기 해제 + 플레이어 아군 해제(이중 방어) + 기존 명령/전투 정리
        ///  · WandererDriver 부착(멱등) + Begin 구동. 사망 병사·이미 방랑자는 무시(멱등).
        ///  · EditMode(비 Play) 안전 — 물리/코루틴 미사용, 드라이버 제거는 Application.isPlaying 분기
        /// </summary>
        public void BecomeWanderer(string reason)
        {
            if (_isDead) return;
            if (IsWanderer) { Debug.Log($"[Wanderer] {guardName} 이미 방랑자 — 스킵(멱등)"); return; }
            IsWanderer = true;
            SetRecruited(false);
            IsGatekeeper = false;
            ClearCommand();
            SetInCombat(false);
            try { transform.SetParent(null, true); }
            catch (System.Exception ex) { Debug.LogWarning($"[Wanderer] 재부모 실패 — {name}: {ex.Message}"); }
            var driver = GetComponent<WandererDriver>();
            if (driver == null) driver = gameObject.AddComponent<WandererDriver>();
            driver.Begin(this, string.IsNullOrEmpty(reason) ? "방랑자 전환" : reason);
            Debug.Log($"[Wanderer] {guardName} 방랑자 전환 — {reason} (국가={nation}, Lv.{level})");
        }

        /// <summary>[119차] 방랑 해제 — AI 재고용/플레이어 포섭 시. 드라이버 제거 후 지정 지점 보행 명령(멱등).</summary>
        public void EndWanderer(Vector3 gotoPosition)
        {
            if (!IsWanderer) return;
            IsWanderer = false;
            var driver = GetComponent<WandererDriver>();
            if (driver != null)
            {
                if (Application.isPlaying) Object.Destroy(driver); else Object.DestroyImmediate(driver);
            }
            ClearCommand();
            SetInCombat(false);
            HostileToPlayerFaction = false;
            SetCommandTarget(gotoPosition, false);
            Debug.Log($"[Wanderer] {guardName} 방랑 해제 → ({gotoPosition.x:0.0},{gotoPosition.z:0.0}) 보행");
        }

        /// <summary>[51차] 병사 간 적대 판정 — 국가 문자열 상이면 서로 적(영주 간 전쟁의 가시 교전:
        ///   서부 침공군 ↔ 남부/내 문지기). 무소속(null/빈 값)은 중립. 기존 HostileToPlayerFaction
        ///   (플레이어 진영 적대) 정책은 그대로 유지 — 이 헬퍼는 "병사 대 병사" 국가 차원의 추가.</summary>
        public bool IsEnemyGuard(GuardPlaceholder other)
        {
            if (other == null || other == this) return false;
            if (string.IsNullOrEmpty(Nation) || string.IsNullOrEmpty(other.Nation)) return false;
            return !string.Equals(Nation, other.Nation, System.StringComparison.Ordinal);
        }

        /// <summary>명령 지점 주변(TARGET_SEARCH_RADIUS)에서 가장 가까운 유효한 적 IDamageable을 탐색해 캐싱한다.</summary>
        private void ResolveAttackTarget()
        {
            // Explicit focus: pin the clicked entity while it passes the same rules as nearest-enemy
            // resolution (ValidateAttackTarget). If it is dead/invalid, drop the identity and search normally.
            if (_explicitAttackTarget != null)
            {
                _attackTarget = _explicitAttackTarget;
                if (ValidateAttackTarget())
                {
                    _attackTargetOffset = _commandTargetPos - _attackTarget.transform.position;
                    _hasResolvedAttackTarget = true;
                    return;
                }
                _explicitAttackTarget = null;
            }

            _attackTarget = null;

            Collider[] hits = Physics.OverlapSphere(_commandTargetPos, TARGET_SEARCH_RADIUS);
            _attackCandidates.Clear();

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
                    if (otherGuard == this) continue;   // 자기 자신(자식 콜라이더가 부모로 자신을 반환하는 케이스)
                    // [51차] 병사 대 병사 — **국가가 다르면 서로 적**(영주 간 전쟁의 가시 교전: 서부 침공군 ↔
                    //   남부/내 문지기). 기존: HostileToPlayerFaction 병사는 내(포섭) 병사만 공격 가능 +
                    //   비적대 병사는 병사 대상 전부 제외 → 서부 침공군이 남부 문지기 앞에 도달해도 교전 불가(무교전 원인).
                    //   같은 국가(또는 무소속)는 계속 제외. HostileToPlayerFaction 정책(내 병사 공격)은 그대로 유효.
                    // [119차] 아웃로(방랑자)는 동국가라도 유효 공격 후보 — 무소속 적대
                    if (!IsEnemyGuard(otherGuard) && !otherGuard.IsOutlawWanderer) continue;
                }
                else
                {
                    // 플레이어 — 적대 정책 가드는 공격 대상 허용(기존: 무조건 제외 → 선공 후 명령 해제의 뿌리)
                    if (!HostileToPlayerFaction && _playerCache != null
                        && go.transform.IsChildOf(_playerCache.transform)) continue;
                }

                _attackCandidates.Add(comp);
            }

            Component bestComp = PickGroupAttackCandidate();
            _attackTarget = bestComp;
            if (_attackTarget != null)
            {
                _attackTargetOffset = _commandTargetPos - _attackTarget.transform.position;
                _hasResolvedAttackTarget = true;
                // [53차] 진단 — 공격 명령이 실제 적을 붙잡았는지 즉명(남부 침공군 교전 성립 검증용).
                Debug.Log($"[이동진단] {guardName} 공격 타겟 확정 → {bestComp.name} ({bestComp.transform.position.x:0.0},{bestComp.transform.position.z:0.0})");
            }
        }

        /// <summary>
        /// Phase 2C.3 group load balancing: among the eligible candidates collected by ResolveAttackTarget,
        /// prefer the enemy with the fewest OTHER living guards already attacking it; ties go to the candidate
        /// nearest the shared attack anchor, then to ordinal name order. Runs only on initial resolution or
        /// after the resolved target dies (ExecuteMovement keeps a valid _attackTarget), so it is never per-frame.
        /// A single candidate skips the guard scan entirely.
        /// </summary>
        private Component PickGroupAttackCandidate()
        {
            int count = _attackCandidates.Count;
            if (count == 0) return null;
            if (count == 1) return _attackCandidates[0];

            GuardPlaceholder[] guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            Component best = null;
            int bestLoad = int.MaxValue;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Component candidate = _attackCandidates[i];
                int load = 0;
                for (int g = 0; g < guards.Length; g++)
                {
                    GuardPlaceholder other = guards[g];
                    if (other == null || other == this || other.IsDead) continue;
                    if (other.CurrentAttackTarget == candidate) load++;
                }

                float dist = Vector3.Distance(_commandTargetPos, candidate.transform.position);
                bool better;
                if (best == null) better = true;
                else if (load != bestLoad) better = load < bestLoad;
                else if (dist != bestDist) better = dist < bestDist;
                else better = string.CompareOrdinal(candidate.name, best.name) < 0;

                if (better)
                {
                    best = candidate;
                    bestLoad = load;
                    bestDist = dist;
                }
            }
            return best;
        }

        /// <summary>
        /// 지형 계약 (FarmPlot.TryGetSurfaceY와 동일 수식): 월드 표면 y = 1 + GetHeightAt(x, z, Plains, 42).
        /// TerrainGenerator 미초기화 등 예외 시 fallbackY(현재 y)를 유지해 텔레포트를 방지한다.
        /// [43차] 실내(IndoorActive)는 월드 지형 y 보정을 스킵 — IndoorScene은 월드 언로드 별도 씬이라
        ///   GetHeightAt가 존재하지 않는 표면을 반환해 병사가 실내 바닥 y에서 이탈하는 원인이었다.
        /// </summary>
        private static float TryGetGroundY(float x, float z, float fallbackY)
        {
            if (ProjectName.Core.UITransitionState.IndoorActive) return fallbackY;   // [43차] 실내 y 보정 금지
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

        /// <summary>[37차] 실내 침대 배치 번호(1..20, -1=미배치) — 병사배치 UI 표시/재배치용.</summary>
        public int IndoorBedIndex => _indoorBedIndex;
        public void SetIndoorBedIndex(int bedNumber) => _indoorBedIndex = bedNumber;

        /// <summary>[39차] 배치 UI가 의도한 실내 이동 명령 허용 플래그(출동/수비 보행) — P14 게이트 예외.</summary>
        public bool IndoorMoveExempt { get; set; }

        /// <summary>[43차] 실내 목적지 도착 시 예외 해제 — 예외 플래그 잔존으로 무명령 이동이 열리는 것 차단.
        ///   ExecuteMovement의 이동 명령 도착(ClearCommand) 시점에 호출.</summary>
        public void ClearIndoorMoveExemptOnArrival()
        {
            if (_hasCommand) return;     // 아직 명령 진행 중 — 해제 금지(도착 시점에만)
            IndoorMoveExempt = false;
        }
        /// <summary>방독면 착용 여부 — 착용 시 가스 경직 반응을 받지 않는다.</summary>
        public bool IsGasMaskEquipped => _isGasMaskEquipped;
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

        /// <summary>가스 구름의 아군 버프 적용.</summary>
        public void ApplyGasCloudBuff(float attack, float defense, float duration)
        {
            if (!IsAlly) return;
            _allyAttackBuff += attack;
            _allyDefenseBuff += defense;
            _allyBuffRemaining += duration;
        }

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