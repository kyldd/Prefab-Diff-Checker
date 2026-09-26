using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PrefabDiffChecker
{
    public class GitCommandResult
    {
        public bool success;
        public int exitCode;
        public string output;
        public string error;
        public byte[] binaryOutput;
    }

    public static class GitRunner
    {
        public static bool TryFindRepositoryRoot(string startDirectory, out string repositoryRoot, out string error)
        {
            repositoryRoot = null;
            error = null;

            if (string.IsNullOrEmpty(startDirectory))
            {
                error = "Start directory is empty.";
                return false;
            }

            GitCommandResult result = RunText(startDirectory, "rev-parse", "--show-toplevel");
            if (!result.success)
            {
                error = string.IsNullOrEmpty(result.error) ? "Could not locate Git repository." : result.error.Trim();
                return false;
            }

            repositoryRoot = NormalizeFullPath(result.output.Trim());
            return !string.IsNullOrEmpty(repositoryRoot);
        }

        public static bool TryGetCurrentBranch(string repositoryRoot, out string branch)
        {
            branch = "";

            GitCommandResult result = RunText(repositoryRoot, "rev-parse", "--abbrev-ref", "HEAD");
            if (!result.success)
                return false;

            branch = result.output.Trim();
            if (string.IsNullOrEmpty(branch))
                branch = "HEAD";

            return true;
        }

        public static bool RevisionExists(string repositoryRoot, string revision)
        {
            if (string.IsNullOrWhiteSpace(revision))
                return false;

            GitCommandResult result = RunText(repositoryRoot, "rev-parse", "--verify", revision.Trim());
            return result.success;
        }

        public static bool TryGetFileAtRevision(
            string repositoryRoot,
            string revision,
            string repositoryRelativePath,
            out byte[] contents,
            out string error)
        {
            contents = null;
            error = null;

            if (string.IsNullOrWhiteSpace(revision))
            {
                error = "Git revision is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(repositoryRelativePath))
            {
                error = "Repository-relative path is empty.";
                return false;
            }

            string objectSpec = revision.Trim() + ":" + repositoryRelativePath.Replace("\\", "/");
            GitCommandResult result = RunBinary(repositoryRoot, "show", objectSpec);

            if (!result.success)
            {
                error = string.IsNullOrEmpty(result.error)
                    ? "Git could not retrieve " + objectSpec + "."
                    : result.error.Trim();
                return false;
            }

            contents = result.binaryOutput;
            return true;
        }

        private static GitCommandResult RunText(string workingDirectory, params string[] arguments)
        {
            return Run(workingDirectory, false, arguments);
        }

        private static GitCommandResult RunBinary(string workingDirectory, params string[] arguments)
        {
            return Run(workingDirectory, true, arguments);
        }

        private static GitCommandResult Run(string workingDirectory, bool binary, params string[] arguments)
        {
            GitCommandResult result = new GitCommandResult();

            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = BuildArguments(arguments)
                };

                using (Process process = new Process())
                {
                    process.StartInfo = info;
                    process.Start();

                    if (binary)
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            process.StandardOutput.BaseStream.CopyTo(stream);
                            result.binaryOutput = stream.ToArray();
                        }
                    }
                    else
                    {
                        result.output = process.StandardOutput.ReadToEnd();
                    }

                    result.error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    result.exitCode = process.ExitCode;
                    result.success = process.ExitCode == 0;
                }
            }
            catch (Exception exception)
            {
                result.success = false;
                result.exitCode = -1;
                result.error = exception.Message;
            }

            return result;
        }

        private static string BuildArguments(string[] arguments)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append(' ');

                builder.Append(QuoteArgument(arguments[i]));
            }

            return builder.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            if (argument == null)
                return "\"\"";

            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
