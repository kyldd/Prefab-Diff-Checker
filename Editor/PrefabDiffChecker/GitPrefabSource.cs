using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrefabDiffChecker
{
    public class GitPrefabInfo
    {
        public string projectRoot;
        public string repositoryRoot;
        public string unityAssetPath;
        public string repositoryRelativePath;
        public string currentBranch;
    }

    public static class GitPrefabSource
    {
        public static bool TryResolve(GameObject prefab, out GitPrefabInfo info, out string error)
        {
            info = null;
            error = null;

            if (prefab == null)
            {
                error = "No prefab selected.";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
            {
                error = "The selected object is not a project asset.";
                return false;
            }

            if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error = "The selected asset is not a prefab.";
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absoluteAssetPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));

            string repositoryRoot;
            if (!GitRunner.TryFindRepositoryRoot(projectRoot, out repositoryRoot, out error))
                return false;

            string relativePath;
            if (!TryMakeRelativePath(repositoryRoot, absoluteAssetPath, out relativePath))
            {
                error = "The selected prefab is not inside the detected Git repository.";
                return false;
            }

            string branch;
            GitRunner.TryGetCurrentBranch(repositoryRoot, out branch);

            info = new GitPrefabInfo
            {
                projectRoot = projectRoot,
                repositoryRoot = repositoryRoot,
                unityAssetPath = assetPath.Replace("\\", "/"),
                repositoryRelativePath = relativePath.Replace("\\", "/"),
                currentBranch = branch
            };

            return true;
        }

        private static bool TryMakeRelativePath(string root, string file, out string relative)
        {
            relative = null;

            try
            {
                string normalizedRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string normalizedFile = Path.GetFullPath(file);
                Uri rootUri = new Uri(normalizedRoot);
                Uri fileUri = new Uri(normalizedFile);

                relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                    .Replace('/', Path.DirectorySeparatorChar);

                if (relative.StartsWith("..", StringComparison.Ordinal))
                {
                    relative = null;
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
