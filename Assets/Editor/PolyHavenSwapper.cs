#nullable disable
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase FIX: PolyHaven 모델 교체 도구.
    /// 사용자가 새 GLB 파일을 Assets/Models/PolyHeven_New/ 에 넣고
    /// Tools > Swap PolyHaven Models 실행하면 자동 교체됩니다.
    /// </summary>
    public class PolyHavenSwapper : EditorWindow
    {
        [MenuItem("Tools/Swap PolyHaven Models")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<PolyHavenSwapper>(false, "PolyHaven Swapper");
        }

        private void OnGUI()
        {
            GUILayout.Label("PolyHaven Model Swapper", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("1. Place new GLB files in:", EditorStyles.label);
            GUILayout.Label("   Assets/Models/PolyHeven_New/", EditorStyles.boldLabel);
            GUILayout.Space(5);

            GUILayout.Label("Expected filenames:", EditorStyles.label);
            GUILayout.Label("   - fir_tree_01.glb  (replaces fir_tree_01_1k)", EditorStyles.label);
            GUILayout.Label("   - jacaranda_tree.glb  (replaces jacaranda_tree_1k)", EditorStyles.label);
            GUILayout.Label("   - tree_small_02.glb  (replaces tree_small_02_1k)", EditorStyles.label);
            GUILayout.Label("   - searsia_lucida.glb  (replaces searsia_lucida_1k)", EditorStyles.label);
            GUILayout.Space(10);

            if (GUILayout.Button("Scan New Models Folder", GUILayout.Height(30)))
            {
                ScanNewModels();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Swap to New Models", GUILayout.Height(40)))
            {
                SwapToNewModels();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Restore Originals from Backup"))
            {
                RestoreOriginals();
            }

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Current: Placeholder cube trees\n" +
                "Backup: Original high-poly in PolyHeven_Backup/\n" +
                "New: Drop new GLB in PolyHeven_New/",
                MessageType.Info);
        }

        private static readonly string[] TREE_NAMES = { "fir_tree_01", "jacaranda_tree", "tree_small_02", "searsia_lucida" };

        private void ScanNewModels()
        {
            string newDir = "Assets/Models/PolyHeven_New";
            if (!Directory.Exists(newDir))
            {
                Debug.Log("📁 PolyHeven_New/ 폴더가 없습니다. 새로 만듭니다.");
                Directory.CreateDirectory(newDir);
                AssetDatabase.Refresh();
            }

            var files = Directory.GetFiles(newDir, "*.glb");
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                long size = new FileInfo(file).Length;
                Debug.Log($"  Found: {name}.glb ({size / 1024 / 1024:F1} MB)");
            }

            if (files.Length == 0)
                Debug.Log("⚠️ PolyHeven_New/ 폴더에 .glb 파일이 없습니다. GLB 파일을 넣고 다시 시도하세요.");
            else
                Debug.Log($"✅ {files.Length}개 GLB 파일 발견. 'Swap to New Models' 버튼을 누르면 교체됩니다.");
        }

        private void SwapToNewModels()
        {
            string newDir = "Assets/Models/PolyHeven_New";
            string targetDir = "Assets/Models/PolyHeven";
            int swapped = 0;

            if (!Directory.Exists(newDir))
            {
                Debug.LogError("❌ PolyHeven_New/ 폴더가 없습니다.");
                return;
            }

            foreach (string treeName in TREE_NAMES)
            {
                string newPath = Path.Combine(newDir, treeName + ".glb");
                if (!File.Exists(newPath))
                {
                    // Try with numbers suffix
                    var files = Directory.GetFiles(newDir, treeName + "*.glb");
                    if (files.Length > 0) newPath = files[0];
                    else continue;
                }

                // Import the new GLB
                string targetPath = Path.Combine(targetDir, treeName + "_1k.gltf");
                if (Directory.Exists(targetPath))
                {
                    // Rename old to .bak
                    string bakPath = targetPath + ".bak";
                    if (Directory.Exists(bakPath))
                        Directory.Delete(bakPath, true);
                    Directory.Move(targetPath, bakPath);
                }

                // Copy new GLB to target
                string destPath = Path.Combine(targetDir, treeName + ".glb");
                File.Copy(newPath, destPath, true);
                swapped++;
                Debug.Log($"✅ Swapped: {treeName}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"🎉 {swapped}개 모델 교체 완료! (씬에서 확인하려면 PolyHeven_Backup 복원 후 재배치 필요)");
        }

        private void RestoreOriginals()
        {
            string backupDir = "PolyHeven_Backup";
            string targetDir = "Assets/Models/PolyHeven";
            int restored = 0;

            if (!Directory.Exists(backupDir))
            {
                Debug.LogError("❌ PolyHeven_Backup/ 폴더가 없습니다.");
                return;
            }

            foreach (string treeName in TREE_NAMES)
            {
                string srcPath = Path.Combine(backupDir, treeName + "_1k.gltf");
                string dstPath = Path.Combine(targetDir, treeName + "_1k.gltf");

                if (Directory.Exists(srcPath) && !Directory.Exists(dstPath))
                {
                    CopyDirectory(srcPath, dstPath);
                    restored++;
                    Debug.Log($"🔄 Restored: {treeName}");
                }
            }

            // Also restore 4K textures
            foreach (var file in Directory.GetFiles(backupDir, "*_4k.*"))
            {
                string name = Path.GetFileName(file);
                string dst = Path.Combine(targetDir, name);
                if (!File.Exists(dst))
                {
                    File.Copy(file, dst);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"🔄 {restored}개 원본 복원 완료!");
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(dst, name), true);
            }
        }
    }
}