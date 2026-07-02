using System;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-32: 가스 분사기 장착/해제 관리
    /// C8-34: 재장전 상태 추가
    /// Player에 부착하여 가스 분사기 장착 상태를 관리합니다.
    /// 분사(Spray) 로직 포함: 장착 후 StartSpray/StopSpray로 가스 소모.
    /// </summary>
    public class GasSprayerController : MonoBehaviour
    {
        public static GasSprayerController Instance { get; private set; }

        // 현재 장착 상태
        public bool IsEquipped { get; private set; }
        public GasSprayerGrade CurrentGrade { get; private set; }
        public string EquippedSprayerName { get; private set; }  // "나무 가스 분사기"
        public float CurrentSprayTimeRemaining { get; private set; }

        // === C8-32 새 필드: 분사 상태 ===
        [SerializeField] private bool _isSpraying;
        /// <summary>현재 분사 중인지 여부</summary>
        public bool IsSpraying => _isSpraying;

        /// <summary>가스 분사기 등급 (currentGrade 별칭, Unity 컨벤션)</summary>
        public GasSprayerGrade currentGrade
        {
            get => CurrentGrade;
            private set => CurrentGrade = value;
        }

        /// <summary>장착 여부 (isEquipped 별칭)</summary>
        public bool isEquipped
        {
            get => IsEquipped;
            private set => IsEquipped = value;
        }

        /// <summary>남은 가스량 (초 단위, CurrentSprayTimeRemaining 별칭)</summary>
        public float remainingGas
        {
            get => CurrentSprayTimeRemaining;
            set => CurrentSprayTimeRemaining = value;
        }

        /// <summary>분사 중 여부 (isSpraying 별칭)</summary>
        public bool isSpraying
        {
            get => _isSpraying;
            private set => _isSpraying = value;
        }

        // ===== C8-34: 재장전 상태 =====
        [SerializeField] private bool _isReloading;
        /// <summary>재장전 중인지 여부</summary>
        public bool IsReloading => _isReloading;

        /// <summary>재장전 남은 시간 (초)</summary>
        public float ReloadTimeRemaining { get; private set; }

        // 삽입된 물약 정보
        public string LoadedPotionId { get; internal set; }  // 빈 문자열 = 없음
        public int LoadedPotionCount { get; internal set; }

        // ===== C8-33: 물약 장전 설정 =====
        [SerializeField] private float _potionConsumptionMultiplier = 2.0f;  // 물약 장전 시 추가 소모율

        // ===== C8-34: 재장전 이벤트 =====
        /// <summary>재장전 상태 변경 시 알림</summary>
        public event Action OnReloadChanged;
        /// <summary>재장전 완료 시 알림</summary>
        public event Action OnReloadCompleted;

        // Unity Events
        public event Action OnEquipChanged;  // 장착/해제 시 알림
        public event Action OnPotionChanged; // 물약 장전/해제 시 알림

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // ===== C8-34: 재장전 카운트다운 =====
            if (_isReloading)
            {
                ReloadTimeRemaining -= Time.deltaTime;
                if (ReloadTimeRemaining <= 0f)
                {
                    CompleteReload();
                }
            }

            // 분사 중일 때 가스 소모
            if (_isSpraying && IsEquipped && CurrentSprayTimeRemaining > 0f)
            {
                var data = GasSprayerManager.GetGradeData(CurrentGrade);
                if (!data.isUnlimited)
                {
                    float consumption = Time.deltaTime;

                    // C8-33: 물약 장전 시 가스 소모율 증가
                    if (!string.IsNullOrEmpty(LoadedPotionId) && LoadedPotionCount > 0)
                    {
                        consumption *= _potionConsumptionMultiplier;
                    }

                    CurrentSprayTimeRemaining -= consumption;
                    if (CurrentSprayTimeRemaining <= 0f)
                    {
                        CurrentSprayTimeRemaining = 0f;
                        _isSpraying = false;
                        // C8-34: 가스 소진 시 자동 재장전
                        StartReload();
                    }
                }
                // Unlimited (SpecialAlloy) — 소모 없음
            }
        }

        // ===== C8-32: Equip / Unequip (grade 기반) =====

        /// <summary>
        /// GasSprayerGrade로 직접 장착 (인벤토리 상호작용 없음, 테스트/디버그용).
        /// </summary>
        public void Equip(GasSprayerGrade grade)
        {
            if (IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 이미 분사기가 장착되어 있습니다. 먼저 해제하세요.");
                return;
            }

            // C8-34: 재장전 중이면 취소
            CancelReload();

            var data = GasSprayerManager.GetGradeData(grade);

            IsEquipped = true;
            CurrentGrade = grade;
            EquippedSprayerName = data.sprayerName;
            _isSpraying = false;

            // 가스 지속 시간 계산 (초기값: maxSprayTime 기준, 물약 없음 = 기본 maxSprayTime)
            if (data.isUnlimited)
            {
                CurrentSprayTimeRemaining = float.PositiveInfinity;
            }
            else
            {
                CurrentSprayTimeRemaining = data.maxSprayTime;
            }

            Debug.Log($"[GasSprayerController] {EquippedSprayerName} 장착 완료! (Equip)");

            OnEquipChanged?.Invoke();
        }

        /// <summary>
        /// 장착 해제 (인벤토리 상호작용 없음, 단순 상태 초기화). UnequipSprayer()의 단순 버전.
        /// </summary>
        public void Unequip()
        {
            if (!IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 장착된 분사기가 없습니다.");
                return;
            }

            // C8-34: 재장전 중이면 취소
            CancelReload();

            // 상태 초기화
            IsEquipped = false;
            CurrentGrade = default;
            EquippedSprayerName = null;
            _isSpraying = false;
            CurrentSprayTimeRemaining = 0f;
            LoadedPotionId = "";
            LoadedPotionCount = 0;

            Debug.Log("[GasSprayerController] 분사기 해제 완료! (Unequip)");

            OnEquipChanged?.Invoke();
        }

        // ===== C8-33: 물약 장전/해제 =====

        /// <summary>
        /// 물약을 분사기에 직접 장전합니다. (필드만 설정, 인벤토리 처리 없음)
        /// 인벤토리 연동은 GasPotionLoader.LoadPotion() 사용.
        /// </summary>
        /// <param name="potionItemId">장전할 물약 ID</param>
        /// <param name="count">장전할 개수</param>
        public void LoadPotion(string potionItemId, int count)
        {
            if (string.IsNullOrEmpty(potionItemId))
            {
                Debug.LogWarning("[GasSprayerController] 유효하지 않은 물약 ID입니다.");
                return;
            }

            if (count <= 0)
            {
                Debug.LogWarning("[GasSprayerController] 장전 개수는 0보다 커야 합니다.");
                return;
            }

            if (!IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 분사기가 장착되지 않았습니다.");
                return;
            }

            // 이미 다른 물약이 장전되어 있으면 무시
            if (!string.IsNullOrEmpty(LoadedPotionId) && LoadedPotionId != potionItemId)
            {
                Debug.LogWarning($"[GasSprayerController] 이미 {LoadedPotionId}이(가) 장전되어 있습니다. UnloadPotion() 후 다시 시도하세요.");
                return;
            }

            LoadedPotionId = potionItemId;
            LoadedPotionCount += count;

            Debug.Log($"[GasSprayerController] 물약 장전 완료: {potionItemId} x{count} (총 {LoadedPotionCount})");

            NotifyPotionChanged();
        }

        /// <summary>
        /// 분사기에 장전된 물약을 모두 해제합니다. (필드만 초기화, 인벤토리 처리 없음)
        /// 인벤토리 연동은 GasPotionLoader.UnloadPotion() 사용.
        /// </summary>
        public void UnloadPotion()
        {
            if (string.IsNullOrEmpty(LoadedPotionId) || LoadedPotionCount <= 0)
            {
                Debug.LogWarning("[GasSprayerController] 장전된 물약이 없습니다.");
                return;
            }

            Debug.Log($"[GasSprayerController] 물약 해제: {LoadedPotionId} x{LoadedPotionCount}");

            if (_isSpraying)
            {
                StopSpray();
            }

            LoadedPotionId = "";
            LoadedPotionCount = 0;

            NotifyPotionChanged();
        }

        /// <summary>
        /// 현재 장전된 물약 개수 반환
        /// </summary>
        public int GetLoadedPotionCount() => LoadedPotionCount;

        /// <summary>
        /// 물약 변경 이벤트를 외부에서 트리거 (GasPotionLoader용)
        /// </summary>
        public void NotifyPotionChanged()
        {
            OnPotionChanged?.Invoke();
        }

        // ===== C8-32: 분사 (Spray) 제어 =====

        /// <summary>
        /// 분사 시작. 장착 상태이고 가스가 남아있어야 함.
        /// </summary>
        public void StartSpray()
        {
            if (!IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 분사기가 장착되지 않았습니다.");
                return;
            }

            if (_isSpraying)
            {
                Debug.LogWarning("[GasSprayerController] 이미 분사 중입니다.");
                return;
            }

            // C8-34: 재장전 중에는 분사 불가
            if (_isReloading)
            {
                Debug.LogWarning("[GasSprayerController] 재장전 중에는 분사할 수 없습니다.");
                return;
            }

            if (CurrentSprayTimeRemaining <= 0f)
            {
                Debug.LogWarning("[GasSprayerController] 가스가 모두 소진되었습니다.");
                return;
            }

            _isSpraying = true;
            Debug.Log("[GasSprayerController] 분사 시작!");

            // Phase 41-2: SpecialEffectsController를 통해 독안개 생성
            if (SpecialEffectsController.Instance != null)
            {
                SpecialEffectsController.Instance.OnGasSprayStart();
            }
        }

        /// <summary>
        /// 분사 중단.
        /// </summary>
        public void StopSpray()
        {
            if (!_isSpraying)
                return;

            _isSpraying = false;
            Debug.Log("[GasSprayerController] 분사 중단!");
        }

        // ===== 기존 메서드 (유지) =====

        /// <summary>
        /// 가스 분사기 장착. PlayerInventory에서 아이템 제거.
        /// </summary>
        public bool EquipSprayer(string sprayerItemId)
        {
            if (IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 이미 분사기가 장착되어 있습니다.");
                return false;
            }

            // 아이템 ID로 등급 찾기
            GasSprayerGrade grade;
            try
            {
                grade = GetGradeByItemId(sprayerItemId);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[GasSprayerController] 알 수 없는 분사기 ID: {sprayerItemId}");
                return false;
            }

            // PlayerInventory에서 아이템 제거
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[GasSprayerController] PlayerInventory.Instance가 없습니다.");
                return false;
            }

            if (!CanEquipSprayer(sprayerItemId))
            {
                Debug.LogWarning($"[GasSprayerController] 인벤토리에 {sprayerItemId}이(가) 없습니다.");
                return false;
            }

            bool removed = PlayerInventory.Instance.RemoveItem(sprayerItemId, 1);
            if (!removed)
            {
                Debug.LogWarning($"[GasSprayerController] {sprayerItemId} 제거 실패");
                return false;
            }

            // 장착 상태 설정 — Equip(grade)에 위임
            Equip(grade);
            return true;
        }

        /// <summary>
        /// 가스 분사기 해제. PlayerInventory에 아이템 반환.
        /// </summary>
        public bool UnequipSprayer()
        {
            if (!IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 장착된 분사기가 없습니다.");
                return false;
            }

            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[GasSprayerController] PlayerInventory.Instance가 없습니다.");
                return false;
            }

            // 아이템 데이터 생성
            string itemId = GetSprayerItemId(CurrentGrade);
            var data = GasSprayerManager.GetGradeData(CurrentGrade);
            var itemData = new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = data.sprayerName,
                description = $"{data.sprayerName} - 등급: {CurrentGrade}",
                category = PlayerInventory.ItemCategory.Tool,
                maxStack = 1,
                maxDurability = 0
            };

            bool added = PlayerInventory.Instance.AddItem(itemData, 1);
            if (!added)
            {
                Debug.LogWarning("[GasSprayerController] 인벤토리 가득 참 - 분사기 반환 실패");
                return false;
            }

            // 상태 초기화 — Unequip()에 위임
            Unequip();
            return true;
        }

        /// <summary>
        /// 현재 장착된 분사기의 데이터 반환
        /// </summary>
        public GasSprayerData GetCurrentSprayerData()
        {
            if (!IsEquipped)
                return default;
            return GasSprayerManager.GetGradeData(CurrentGrade);
        }

        /// <summary>
        /// 장착 가능한 분사기인지 확인 (PlayerInventory에 해당 아이템이 있는지)
        /// </summary>
        public bool CanEquipSprayer(string sprayerItemId)
        {
            if (string.IsNullOrEmpty(sprayerItemId))
                return false;
            if (PlayerInventory.Instance == null)
                return false;
            return PlayerInventory.Instance.HasItem(sprayerItemId);
        }

        /// <summary>
        /// 분사기 아이템 ID 생성 (컨벤션: "GasSprayer_Wood", "GasSprayer_Stone" 등)
        /// </summary>
        public static string GetSprayerItemId(GasSprayerGrade grade)
        {
            return $"GasSprayer_{grade}";
        }

        // ===== C8-34: 재장전 (Reload) =====

        /// <summary>
        /// 재장전 시작. 가스가 완전히 소진된 상태여야 함.
        /// SpecialAlloy 등급은 재장전 불필요 (0초, 즉시 완료).
        /// </summary>
        public void StartReload()
        {
            if (!IsEquipped)
            {
                Debug.LogWarning("[GasSprayerController] 분사기가 장착되지 않았습니다.");
                return;
            }

            if (_isReloading)
            {
                Debug.LogWarning("[GasSprayerController] 이미 재장전 중입니다.");
                return;
            }

            var data = GasSprayerManager.GetGradeData(CurrentGrade);
            if (data.isUnlimited)
            {
                Debug.Log("[GasSprayerController] SpecialAlloy는 재장전이 필요 없습니다.");
                return;
            }

            if (CurrentSprayTimeRemaining > 0f)
            {
                Debug.Log("[GasSprayerController] 가스가 아직 남아 있습니다. 재장전이 필요하지 않습니다.");
                return;
            }

            float reloadTime = GasSprayerManager.GetReloadTime(CurrentGrade);
            if (reloadTime <= 0f)
            {
                // 즉시 재장전 (SpecialAlloy는 위에서 걸러졌지만, 다른 즉시 완료 등급 대비)
                CurrentSprayTimeRemaining = data.maxSprayTime;
                OnReloadChanged?.Invoke();
                OnReloadCompleted?.Invoke();
                Debug.Log("[GasSprayerController] 즉시 재장전 완료!");
                return;
            }

            _isReloading = true;
            ReloadTimeRemaining = reloadTime;

            // 분사 중이면 중단
            if (_isSpraying)
            {
                _isSpraying = false;
            }

            OnReloadChanged?.Invoke();
            Debug.Log($"[GasSprayerController] 재장전 시작! {reloadTime}초");
        }

        /// <summary>
        /// 재장전 취소. 상태 초기화.
        /// </summary>
        public void CancelReload()
        {
            if (!_isReloading)
                return;

            _isReloading = false;
            ReloadTimeRemaining = 0f;
            CurrentSprayTimeRemaining = 0f;

            OnReloadChanged?.Invoke();
            Debug.Log("[GasSprayerController] 재장전 취소!");
        }

        /// <summary>
        /// 재장전 완료 처리. 가스 리필 후 이벤트 발생.
        /// </summary>
        private void CompleteReload()
        {
            _isReloading = false;
            ReloadTimeRemaining = 0f;

            var data = GasSprayerManager.GetGradeData(CurrentGrade);
            CurrentSprayTimeRemaining = data.maxSprayTime;

            OnReloadChanged?.Invoke();
            OnReloadCompleted?.Invoke();
            Debug.Log("[GasSprayerController] 재장전 완료!");
        }

        /// <summary>
        /// 아이템 ID로 GasSprayerGrade 찾기
        /// </summary>
        private static GasSprayerGrade GetGradeByItemId(string sprayerItemId)
        {
            string prefix = "GasSprayer_";
            if (!sprayerItemId.StartsWith(prefix))
                throw new ArgumentException($"Invalid sprayer item ID format: {sprayerItemId}");

            string gradeName = sprayerItemId.Substring(prefix.Length);
            if (Enum.TryParse<GasSprayerGrade>(gradeName, out var grade))
                return grade;

            throw new ArgumentException($"Unknown GasSprayerGrade in item ID: {sprayerItemId}");
        }
    }
}
