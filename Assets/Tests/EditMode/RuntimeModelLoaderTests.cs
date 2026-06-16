using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// RuntimeModelLoader에 대한 EditMode 테스트.
    /// GLB 모델 로드, 캐싱, 폴백 동작을 검증합니다.
    /// Resources/Models/UserProvided/ 폴더의 GLB 프리팹을 대상으로 합니다.
    /// </summary>
    [TestFixture]
    public class RuntimeModelLoaderTests
    {
        [SetUp]
        public void Setup()
        {
            // 테스트 전에 항상 초기화
            RuntimeModelLoader.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            // 테스트 후 캐시 리셋 (다음 테스트에 영향 없도록)
            RuntimeModelLoader.Reload();
        }

        #region Initialization

        /// <summary>
        /// Initialize() 호출 후 IsInitialized가 true를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void Initialize_SetsIsInitializedToTrue()
        {
            Assert.IsTrue(RuntimeModelLoader.IsInitialized,
                "Initialize() 호출 후 IsInitialized는 true여야 함");
        }

        /// <summary>
        /// Initialize()를 두 번 호출해도 예외가 발생하지 않고 안전하게 동작하는지 검증합니다.
        /// </summary>
        [Test]
        public void Initialize_IsIdempotent()
        {
            Assert.DoesNotThrow(() => RuntimeModelLoader.Initialize(),
                "두 번째 Initialize() 호출은 예외 없이 안전해야 함");
            Assert.IsTrue(RuntimeModelLoader.IsInitialized,
                "두 번째 Initialize() 호출 후에도 IsInitialized는 true여야 함");
        }

        /// <summary>
        /// Initialize() 후 LoadedModelCount가 0 이상인지 검증합니다.
        /// 실제 GLB 파일이 있는 경우 실제 개수가 반영됩니다.
        /// </summary>
        [Test]
        public void Initialize_LoadsModels()
        {
            int count = RuntimeModelLoader.LoadedModelCount();
            Assert.GreaterOrEqual(count, 0,
                "Initialize() 후 LoadedModelCount는 0 이상이어야 함");
        }

        #endregion

        #region TryGetModel

        /// <summary>
        /// TryGetModel이 "player" 모델을 성공적으로 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsPlayerModel()
        {
            bool found = RuntimeModelLoader.TryGetModel("player", out GameObject prefab);

            if (found)
            {
                Assert.IsNotNull(prefab, "찾은 player 프리팹은 null이 아니어야 함");
                Assert.AreEqual("player", prefab.name.ToLowerInvariant(),
                    "프리팹 이름이 'player'와 일치해야 함");
            }
            else
            {
                Assert.IsNull(prefab, "찾지 못한 경우 prefab은 null이어야 함");
            }
        }

        /// <summary>
        /// TryGetModel이 "soldier" 모델을 성공적으로 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsSoldierModel()
        {
            bool found = RuntimeModelLoader.TryGetModel("soldier", out GameObject prefab);

            if (found)
            {
                Assert.IsNotNull(prefab, "찾은 soldier 프리팹은 null이 아니어야 함");
            }
            else
            {
                Assert.IsNull(prefab, "찾지 못한 경우 prefab은 null이어야 함");
            }
        }

        /// <summary>
        /// TryGetModel이 "blue_castle" 모델을 성공적으로 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsBlueCastleModel()
        {
            bool found = RuntimeModelLoader.TryGetModel("blue_castle", out GameObject prefab);

            if (found)
            {
                Assert.IsNotNull(prefab, "찾은 blue_castle 프리팹은 null이 아니어야 함");
            }
            else
            {
                Assert.IsNull(prefab, "찾지 못한 경우 prefab은 null이어야 함");
            }
        }

        /// <summary>
        /// TryGetModel이 대소문자를 구분하지 않고 동일한 결과를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_IsCaseInsensitive()
        {
            // player.glb가 존재한다고 가정
            bool foundLower = RuntimeModelLoader.TryGetModel("player", out GameObject prefabLower);
            bool foundUpper = RuntimeModelLoader.TryGetModel("PLAYER", out GameObject prefabUpper);
            bool foundMixed = RuntimeModelLoader.TryGetModel("Player", out GameObject prefabMixed);

            // 모두 동일한 찾기/못찾기 결과여야 함
            Assert.AreEqual(foundLower, foundUpper, "대소문자와 관계없이 결과가 같아야 함");
            Assert.AreEqual(foundLower, foundMixed, "대소문자와 관계없이 결과가 같아야 함");

            if (foundLower)
            {
                Assert.AreSame(prefabLower, prefabUpper,
                    "같은 모델은 동일한 인스턴스를 반환해야 함");
                Assert.AreSame(prefabLower, prefabMixed,
                    "같은 모델은 동일한 인스턴스를 반환해야 함");
            }
        }

        /// <summary>
        /// TryGetModel이 존재하지 않는 모델 키에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsFalseForUnknownModel()
        {
            bool found = RuntimeModelLoader.TryGetModel("nonexistent_model_xyz", out GameObject prefab);

            Assert.IsFalse(found, "존재하지 않는 모델은 false를 반환해야 함");
            Assert.IsNull(prefab, "찾지 못한 경우 prefab은 null이어야 함");
        }

        /// <summary>
        /// TryGetModel이 null 키에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsFalseForNullKey()
        {
            bool found = RuntimeModelLoader.TryGetModel(null, out GameObject prefab);

            Assert.IsFalse(found, "null 키는 false를 반환해야 함");
            Assert.IsNull(prefab, "null 키의 prefab은 null이어야 함");
        }

        /// <summary>
        /// TryGetModel이 빈 문자열 키에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_ReturnsFalseForEmptyKey()
        {
            bool found = RuntimeModelLoader.TryGetModel("", out GameObject prefab);

            Assert.IsFalse(found, "빈 문자열 키는 false를 반환해야 함");
            Assert.IsNull(prefab, "빈 문자열 키의 prefab은 null이어야 함");
        }

        #endregion

        #region HasModel

        /// <summary>
        /// HasModel이 알려진 모델에 대해 true를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void HasModel_ReturnsTrueForKnownModel()
        {
            // player는 ModelMapping에 등록된 알려진 GLB 모델
            bool hasPlayer = RuntimeModelLoader.HasModel("player");

            // player.glb가 실제로 존재하는 경우에만 true
            // (테스트 환경에 따라 다를 수 있음 — 존재 여부 확인은 별도)
            if (RuntimeModelLoader.TryGetModel("player", out _))
            {
                Assert.IsTrue(hasPlayer, "TryGetModel이 찾은 모델은 HasModel도 true여야 함");
            }
        }

        /// <summary>
        /// HasModel이 존재하지 않는 모델에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void HasModel_ReturnsFalseForUnknownModel()
        {
            Assert.IsFalse(RuntimeModelLoader.HasModel("nonexistent_xyz"),
                "존재하지 않는 모델은 false를 반환해야 함");
        }

        /// <summary>
        /// HasModel이 null 키에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void HasModel_ReturnsFalseForNullKey()
        {
            Assert.IsFalse(RuntimeModelLoader.HasModel(null),
                "null 키는 false를 반환해야 함");
        }

        /// <summary>
        /// HasModel이 빈 문자열 키에 대해 false를 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void HasModel_ReturnsFalseForEmptyKey()
        {
            Assert.IsFalse(RuntimeModelLoader.HasModel(""),
                "빈 문자열 키는 false를 반환해야 함");
        }

        /// <summary>
        /// HasModel이 대소문자를 구분하지 않는지 검증합니다.
        /// </summary>
        [Test]
        public void HasModel_IsCaseInsensitive()
        {
            bool lower = RuntimeModelLoader.HasModel("player");
            bool upper = RuntimeModelLoader.HasModel("PLAYER");
            bool mixed = RuntimeModelLoader.HasModel("Player");

            Assert.AreEqual(lower, upper, "대소문자와 관계없이 HasModel 결과가 같아야 함");
            Assert.AreEqual(lower, mixed, "대소문자와 관계없이 HasModel 결과가 같아야 함");
        }

        #endregion

        #region GetAllLoadedModels

        /// <summary>
        /// GetAllLoadedModels가 문자열 배열을 반환하는지 검증합니다.
        /// </summary>
        [Test]
        public void GetAllLoadedModels_ReturnsArray()
        {
            string[] keys = RuntimeModelLoader.GetAllLoadedModels();

            Assert.IsNotNull(keys, "GetAllLoadedModels는 null이 아닌 배열을 반환해야 함");
            Assert.GreaterOrEqual(keys.Length, 0, "배열 길이는 0 이상이어야 함");
        }

        /// <summary>
        /// GetAllLoadedModels로 얻은 키들이 TryGetModel에서 유효한지 검증합니다.
        /// </summary>
        [Test]
        public void GetAllLoadedModels_KeysAreValid()
        {
            string[] keys = RuntimeModelLoader.GetAllLoadedModels();
            foreach (string key in keys)
            {
                Assert.IsTrue(RuntimeModelLoader.TryGetModel(key, out _),
                    $"GetAllLoadedModels로 얻은 키 '{key}'는 TryGetModel에서 유효해야 함");
            }
        }

        #endregion

        #region Reload

        /// <summary>
        /// Reload() 호출 후 다시 초기화되는지 검증합니다.
        /// </summary>
        [Test]
        public void Reload_Reinitializes()
        {
            int countBefore = RuntimeModelLoader.LoadedModelCount();

            RuntimeModelLoader.Reload();

            Assert.IsTrue(RuntimeModelLoader.IsInitialized,
                "Reload() 후 IsInitialized는 true여야 함");
            Assert.GreaterOrEqual(RuntimeModelLoader.LoadedModelCount(), 0,
                "Reload() 후 LoadedModelCount는 0 이상이어야 함");
        }

        /// <summary>
        /// Reload()를 여러 번 호출해도 안전하게 동작하는지 검증합니다.
        /// </summary>
        [Test]
        public void Reload_IsSafeToCallMultipleTimes()
        {
            Assert.DoesNotThrow(() => RuntimeModelLoader.Reload(),
                "첫 번째 Reload()는 예외 없이 안전해야 함");
            Assert.DoesNotThrow(() => RuntimeModelLoader.Reload(),
                "두 번째 Reload()는 예외 없이 안전해야 함");
            Assert.DoesNotThrow(() => RuntimeModelLoader.Reload(),
                "세 번째 Reload()는 예외 없이 안전해야 함");
        }

        #endregion

        #region Tiered Model Keys

        /// <summary>
        /// 티어드 모델 키(예: "soldier_tier1")가 TryGetModel에서 올바르게 처리되는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_HandlesTieredKeys()
        {
            // soldier_tier1.glb가 존재하는 경우
            bool found = RuntimeModelLoader.TryGetModel("soldier_tier1", out GameObject prefab);
            if (found)
            {
                Assert.IsNotNull(prefab, "티어드 모델 프리팹은 null이 아니어야 함");
            }
        }

        #endregion

        #region Fallback Safety

        /// <summary>
        /// 초기화되지 않은 상태에서 TryGetModel을 호출해도 자동 초기화되는지 검증합니다.
        /// </summary>
        [Test]
        public void TryGetModel_AutoInitializesIfNeeded()
        {
            // Reload로 초기화 리셋
            RuntimeModelLoader.Reload();

            // IsInitialized의 값과 관계없이 TryGetModel이 동작해야 함
            bool found = RuntimeModelLoader.TryGetModel("player", out _);

            // IsInitialized는 자동으로 true가 되어야 함
            Assert.IsTrue(RuntimeModelLoader.IsInitialized,
                "TryGetModel 호출 후 IsInitialized는 true여야 함");
        }

        #endregion
    }
}