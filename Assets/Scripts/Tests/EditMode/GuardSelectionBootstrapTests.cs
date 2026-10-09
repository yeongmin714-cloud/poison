using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>Source-level regression coverage for the scene-specific guard-selection bootstrap paths.</summary>
    public class GuardSelectionBootstrapTests
    {
        private static string ReadAssetScript(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
        }

        private static string ExtractMethod(string source, string signature)
        {
            int methodStart = source.IndexOf(signature);
            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), "Expected method was not found: " + signature);

            int bodyStart = source.IndexOf('{', methodStart);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), "Expected method body was not found: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(methodStart, i - methodStart + 1);
            }

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        [Test]
        public void MainSceneBootstrapEnsuresManagerAndKeepsExistingTest10Guard()
        {
            string gameSetup = ReadAssetScript("GameSetup.cs");
            string coreBootstrap = ReadAssetScript("Scripts/Systems/CoreSystemsBootstrap.cs");
            string testSetup = ReadAssetScript("Scripts/Systems/TestTerritoryCombatSetup.cs");
            string startMethod = ExtractMethod(gameSetup, "private void Start()");
            string ensureMethod = ExtractMethod(coreBootstrap, "public static void EnsureGuardSelectionManager()");
            string testEnsure = ExtractMethod(testSetup, "private void EnsureGameManager()");

            Assert.That(startMethod, Does.Contain("CoreSystemsBootstrap.EnsureGuardSelectionManager();"),
                "GameSetup's normal runtime path must create the manager for MainScene.");
            Assert.That(startMethod.IndexOf("if (terrainOnly)", System.StringComparison.Ordinal), Is.LessThan(
                    startMethod.IndexOf("CoreSystemsBootstrap.EnsureGuardSelectionManager();", System.StringComparison.Ordinal)),
                "TerrainOnly mode must continue returning before gameplay bootstrap.");
            Assert.That(ensureMethod, Does.Contain("FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include)"),
                "The shared Ensure must reuse active or inactive scene instances.");
            Assert.That(ensureMethod, Does.Contain("new GameObject(\"GuardSelectionManager\")"),
                "The shared Ensure must create the manager when missing.");
            Assert.That(testEnsure, Does.Contain("FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include)"),
                "Test_10's existing manager creation path must remain idempotent.");
        }
    }
}
