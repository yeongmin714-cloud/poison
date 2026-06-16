using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C11-01: IndoorBuilder EditMode 테스트.
    /// 방 생성, 메시 구조, 콜라이더 확인.
    /// </summary>
    public class IndoorBuilderTests
    {
        private const float TEST_WIDTH = 10f;
        private const float TEST_HEIGHT = 3.5f;
        private const float TEST_DEPTH = 8f;

        private Material _testMat;

        [SetUp]
        public void Setup()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _testMat = new Material(shader);
            _testMat.color = Color.gray;
        }

        [TearDown]
        public void TearDown()
        {
            if (_testMat != null)
                Object.DestroyImmediate(_testMat);
        }

        // ===== 타입 존재 확인 =====

        [Test]
        public void IndoorBuilder_Type_Exists()
        {
            Assert.IsNotNull(typeof(IndoorBuilder), "IndoorBuilder static class must exist");
        }

        // ===== CreateRoom 기본 =====

        [Test]
        public void CreateRoom_ReturnsNonNullGameObject()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            Assert.IsNotNull(room, "CreateRoom must return non-null GameObject");
            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_NameIsRoom()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            Assert.AreEqual("Room", room.name, "Root GameObject must be named 'Room'");
            Object.DestroyImmediate(room);
        }

        // ===== 자식 개수 확인 =====

        [Test]
        public void CreateRoom_Has6ChildObjects()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            Assert.AreEqual(6, room.transform.childCount, "Room must have 6 children (Floor, 4 Walls, Ceiling)");
            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_ChildrenHaveCorrectNames()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            string[] expectedNames = { "Floor", "Wall_Front", "Wall_Back", "Wall_Left", "Wall_Right", "Ceiling" };

            foreach (string name in expectedNames)
            {
                Transform child = room.transform.Find(name);
                Assert.IsNotNull(child, $"Room must have child named '{name}'");
            }

            Object.DestroyImmediate(room);
        }

        // ===== 메시 구조 확인 =====

        [Test]
        public void CreateRoom_EachChild_HasMeshFilterAndRenderer()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                Assert.IsNotNull(child.GetComponent<MeshFilter>(), $"'{child.name}' must have MeshFilter");
                Assert.IsNotNull(child.GetComponent<MeshRenderer>(), $"'{child.name}' must have MeshRenderer");
            }

            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_EachChild_Has4Vertices()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, $"'{child.name}' must have MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"'{child.name}' mesh must not be null");
                Assert.AreEqual(4, mf.sharedMesh.vertexCount, $"'{child.name}' must have exactly 4 vertices (Quad)");
            }

            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_EachChild_Has2Triangles()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf.sharedMesh);
                // 2 triangles = 6 indices
                Assert.AreEqual(6, mf.sharedMesh.triangles.Length, $"'{child.name}' must have 6 triangle indices (2 triangles)");
            }

            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_EachChild_HasUVs()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf.sharedMesh.uv, $"'{child.name}' must have UVs");
                Assert.AreEqual(4, mf.sharedMesh.uv.Length, $"'{child.name}' must have 4 UV coordinates");
            }

            Object.DestroyImmediate(room);
        }

        // ===== 콜라이더 확인 =====

        [Test]
        public void CreateRoom_EachChild_HasMeshCollider()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshCollider col = child.GetComponent<MeshCollider>();
                Assert.IsNotNull(col, $"'{child.name}' must have MeshCollider");
                Assert.IsNotNull(col.sharedMesh, $"'{child.name}' MeshCollider must have a sharedMesh");
            }

            Object.DestroyImmediate(room);
        }

        // ===== 재질 확인 =====

        [Test]
        public void CreateRoom_EachChild_HasMaterialAssigned()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                Assert.IsNotNull(renderer.sharedMaterial, $"'{child.name}' must have a material assigned");
            }

            Object.DestroyImmediate(room);
        }

        // ===== Normals 확인 =====

        [Test]
        public void CreateRoom_EachChild_HasNormals()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);

            foreach (Transform child in room.transform)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf.sharedMesh.normals, $"'{child.name}' must have normals");
                Assert.AreEqual(4, mf.sharedMesh.normals.Length, $"'{child.name}' must have 4 normals");
            }

            Object.DestroyImmediate(room);
        }

        // ===== 위치 확인 =====

        [Test]
        public void CreateRoom_Floor_AtCorrectPosition()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            Transform floor = room.transform.Find("Floor");
            Assert.IsNotNull(floor);

            Vector3 localPos = floor.localPosition;
            Assert.AreEqual(0f, localPos.x, 0.001f, "Floor X must be 0");
            Assert.AreEqual(0f, localPos.y, 0.001f, "Floor Y must be 0 (ground level)");

            Object.DestroyImmediate(room);
        }

        [Test]
        public void CreateRoom_Ceiling_AtCorrectHeight()
        {
            GameObject room = IndoorBuilder.CreateRoom(TEST_WIDTH, TEST_HEIGHT, TEST_DEPTH, _testMat, _testMat, _testMat);
            Transform ceiling = room.transform.Find("Ceiling");
            Assert.IsNotNull(ceiling);

            Vector3 localPos = ceiling.localPosition;
            Assert.AreEqual(0f, localPos.x, 0.001f, "Ceiling X must be 0");
            Assert.AreEqual(TEST_HEIGHT, localPos.y, 0.001f, "Ceiling Y must equal room height");

            Object.DestroyImmediate(room);
        }
    }
}
