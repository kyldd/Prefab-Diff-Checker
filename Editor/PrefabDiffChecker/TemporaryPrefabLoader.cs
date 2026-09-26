using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrefabDiffChecker
{
    public static class TemporaryPrefabLoader
    {
        private const string TempRoot = "Assets/__GitPrefabDiffTemp";
        private const string LfsHeader = "version https://git-lfs.github.com/spec/v1";

        public static bool TryBuildSnapshot(
            GitPrefabInfo sourceInfo,
            string revision,
            out PrefabSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            if (sourceInfo == null)
            {
                error = "Git prefab source information is missing.";
                return false;
            }

            byte[] prefabBytes;
            if (!GitRunner.TryGetFileAtRevision(
                    sourceInfo.repositoryRoot,
                    revision,
                    sourceInfo.repositoryRelativePath,
                    out prefabBytes,
                    out error))
            {
                return false;
            }

            if (IsLfsPointer(prefabBytes))
            {
                error = "The selected revision contains a Git LFS pointer instead of prefab data.";
                return false;
            }

            EnsureTempFolder();

            string tempAssetPath = BuildTempAssetPath(sourceInfo.unityAssetPath);
            string absoluteTempPath = AssetPathToAbsolutePath(tempAssetPath);

            try
            {
                string directory = Path.GetDirectoryName(absoluteTempPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(absoluteTempPath, prefabBytes);

                AssetDatabase.ImportAsset(
                    tempAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tempAssetPath);
                if (prefab == null)
                {
                    error = "Unity could not import the prefab retrieved from '" + revision + "'.";
                    return false;
                }

                snapshot = PrefabSnapshotBuilder.Build(prefab);
                if (snapshot == null)
                {
                    error = "Could not build a snapshot from the Git revision prefab.";
                    return false;
                }

                snapshot.assetPath = sourceInfo.unityAssetPath;
                return true;
            }
            catch (Exception exception)
            {
                error = "Failed to import Git prefab:\n" + exception.Message;
                return false;
            }
            finally
            {
                CleanupTempAsset(tempAssetPath);
            }
        }

        public static void CleanupAll()
        {
            if (AssetDatabase.IsValidFolder(TempRoot))
                AssetDatabase.DeleteAsset(TempRoot);
        }

        private static bool IsLfsPointer(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            int length = Math.Min(data.Length, 128);
            string header = Encoding.UTF8.GetString(data, 0, length);
            return header.StartsWith(LfsHeader, StringComparison.Ordinal);
        }

        private static string BuildTempAssetPath(string originalAssetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(originalAssetPath);
            return TempRoot + "/" + fileName + "_" + Guid.NewGuid().ToString("N") + ".prefab";
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempRoot))
                AssetDatabase.CreateFolder("Assets", "__GitPrefabDiffTemp");
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static void CleanupTempAsset(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            string absoluteRoot = AssetPathToAbsolutePath(TempRoot);
            if (!Directory.Exists(absoluteRoot))
                return;

            if (Directory.GetFiles(absoluteRoot).Length == 0 &&
                Directory.GetDirectories(absoluteRoot).Length == 0)
            {
                AssetDatabase.DeleteAsset(TempRoot);
            }
        }
    }
}
