using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.UI;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [198차] 아이콘 per-item 텍스처 오버라이드 계약 —
    /// ①미등록 id 무변경(MPB/머티리얼 무손상) ②등록 id는 MPB 텍스처 교체(머티리얼 인스턴스 금지)
    /// ③리소스 부재 안전 false ④null 가드 ⑤dish 틴트(_BaseColor)와 공존 ⑥테이블 등록분 전부 리소스 실로드.
    /// 운영 등록은 0건(P0 감사 377종 — 교체 이득 대상 부재) — 테스트는 __probe 키 주입으로 기구를 증명하고
    /// TearDown에서 완전 제거(누수 없음).
    /// </summary>
    public class ItemIconTextureOverrideTests
    {
        private const string ProbeResPath = "Textures/icon_override_probe";
        private const string ProbeKey = "__icon_tex_override_probe__";
        private const string MissingKey = "__icon_tex_override_missing__";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Texture2D _probe;
        private readonly List<Object> _temp = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _probe = Resources.Load<Texture2D>(ProbeResPath);
            Assert.That(_probe, Is.Not.Null, "[198] 오버라이드 프로브 텍스처 리소스 수용: " + ProbeResPath);
        }

        [TearDown]
        public void TearDown()
        {
            ItemIconTextureOverrides.Table.Remove(ProbeKey);
            ItemIconTextureOverrides.Table.Remove(MissingKey);
            foreach (var o in _temp)
                if (o != null)
                    Object.DestroyImmediate(o);
            _temp.Clear();
        }

        private GameObject LoadGlb(string key)
        {
            var prefab = Resources.Load<GameObject>("Models/UserProvided/" + key);
            Assert.That(prefab, Is.Not.Null, "[198] GLB 리소스: " + key);
            var go = Object.Instantiate(prefab);
            _temp.Add(go);
            return go;
        }

        private static Texture MatBaseTex(Material m)
        {
            if (m == null)
                return null;
            if (m.HasProperty(BaseMapId))
                return m.GetTexture(BaseMapId);
            if (m.HasProperty(MainTexId))
                return m.GetTexture(MainTexId);
            return null;
        }

        [Test]
        public void TryApply_UnregisteredId_NoChange()
        {
            var node = LoadGlb("wood_sword");
            var rends = node.GetComponentsInChildren<Renderer>();
            Assert.That(rends.Length, Is.GreaterThan(0), "[198] wood_sword 렌더러 존재");
            var before = MatBaseTex(rends[0].sharedMaterial);

            bool applied = ItemIconTextureOverrides.TryApply("wood_sword", node);

            Assert.That(applied, Is.False, "[198] 미등록 id는 무변경 false");
            var after = MatBaseTex(rends[0].sharedMaterial);
            Assert.That(ReferenceEquals(before, after), Is.True,
                "[198] 머티리얼 텍스처 무변경 — MPB만 허용(머티리얼 인스턴스 금지 계약)");
            var mpb = new MaterialPropertyBlock();
            rends[0].GetPropertyBlock(mpb);
            Assert.That(mpb.GetTexture(BaseMapId) == _probe || mpb.GetTexture(MainTexId) == _probe, Is.False,
                "[198] MPB에도 프로브 미설정");
        }

        [Test]
        public void TryApply_RegisteredId_ReplacesTextureViaMpb()
        {
            ItemIconTextureOverrides.Table[ProbeKey] = ProbeResPath;
            var node = LoadGlb("wood_sword");

            bool applied = ItemIconTextureOverrides.TryApply(ProbeKey, node);

            Assert.That(applied, Is.True, "[198] 등록 id는 MPB 텍스처 교체 true");
            foreach (var r in node.GetComponentsInChildren<Renderer>())
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                bool replaced = ReferenceEquals(mpb.GetTexture(BaseMapId), _probe)
                                || ReferenceEquals(mpb.GetTexture(MainTexId), _probe);
                Assert.That(replaced, Is.True,
                    "[198] MPB _BaseMap/_MainTex가 프로브 텍스처로 교체 — " + r.name);
            }
        }

        [Test]
        public void TryApply_MissingResource_SafeFalse()
        {
            ItemIconTextureOverrides.Table[MissingKey] = "Textures/__no_such_override_tex__";
            var node = LoadGlb("wood_sword");
            var rends = node.GetComponentsInChildren<Renderer>();
            var before = MatBaseTex(rends[0].sharedMaterial);

            Assert.DoesNotThrow(() => ItemIconTextureOverrides.TryApply(MissingKey, node),
                "[198] 리소스 부재도 크래시 금지");
            bool applied = ItemIconTextureOverrides.TryApply(MissingKey, node);
            Assert.That(applied, Is.False, "[198] 리소스 부재는 false");
            var after = MatBaseTex(rends[0].sharedMaterial);
            Assert.That(ReferenceEquals(before, after), Is.True, "[198] 리소스 부재 시 무변경");
        }

        [Test]
        public void TryApply_NullGuards()
        {
            var node = LoadGlb("wood_sword");
            Assert.That(ItemIconTextureOverrides.TryApply(null, node), Is.False, "[198] null id 가드");
            Assert.That(ItemIconTextureOverrides.TryApply("", node), Is.False, "[198] 빈 id 가드");
            Assert.That(ItemIconTextureOverrides.TryApply(ProbeKey, null), Is.False, "[198] null 노드 가드");
        }

        [Test]
        public void DishTint_And_Override_Coexist()
        {
            var node = LoadGlb("dish_braise");
            var tint = new Color(1f, 0.5f, 0.25f, 1f);
            DishGlbCatalog.ApplyTint(node, tint);          // 틴트 먼저(_BaseColor MPB 설정)

            ItemIconTextureOverrides.Table[ProbeKey] = ProbeResPath;
            bool applied = ItemIconTextureOverrides.TryApply(ProbeKey, node);

            Assert.That(applied, Is.True, "[198] dish 노드에도 오버라이드 적용");
            foreach (var r in node.GetComponentsInChildren<Renderer>())
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                bool texReplaced = ReferenceEquals(mpb.GetTexture(BaseMapId), _probe)
                                   || ReferenceEquals(mpb.GetTexture(MainTexId), _probe);
                Assert.That(texReplaced, Is.True, "[198] 오버라이드 텍스처 유지 — " + r.name);
                // GetPropertyBlock 선행으로 틴트(_BaseColor) 보존 — 공존 계약
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                    Assert.That(mpb.GetColor(BaseColorId), Is.EqualTo(tint),
                        "[198] _BaseColor 틴트 보존(공존) — " + r.name);
            }
        }

        [Test]
        public void Table_AllEntries_Loadable()
        {
            // [198차] 운영 등록분 정합 — 테이블에 추가된 id 전부 리소스 실로드(부재 등록 방지).
            // 현재 0건(P0 감사 — 교체 이득 대상 부재). 향후 수용 시 자동 감시.
            foreach (var kv in ItemIconTextureOverrides.Table)
            {
                Assert.That(kv.Key, Is.Not.Empty, "[198] 테이블 키 공백 금지");
                var tex = Resources.Load<Texture2D>(kv.Value);
                Assert.That(tex, Is.Not.Null,
                    "[198] 테이블 등록 텍스처 리소스 실존 실패 — id=" + kv.Key + " path=" + kv.Value);
            }
        }

        [Test]
        public void HasOverride_MirrorsTable()
        {
            Assert.That(ItemIconTextureOverrides.HasOverride(ProbeKey), Is.False, "[198] 미등록 HasOverride false");
            ItemIconTextureOverrides.Table[ProbeKey] = ProbeResPath;
            Assert.That(ItemIconTextureOverrides.HasOverride(ProbeKey), Is.True, "[198] 등록 후 HasOverride true");
            Assert.That(ItemIconTextureOverrides.HasOverride(null), Is.False, "[198] null 가드");
        }
    }
}
