using System.Diagnostics;
using UnityEngine;

namespace PropertyHistoryTool
{
    public static class GitUtils
    {
        /// <summary>
        /// Runs a Git command and returns its standard output.
        /// </summary>
        /// <param name="arguments">The arguments to pass to git.exe.</param>
        /// <returns>The output from the command, or an error message.</returns>
        public static string RunGitCommand(string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "git"; // Assumes git is in the system's PATH
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // Set the working directory to the Unity Project root
                process.StartInfo.WorkingDirectory = Application.dataPath.Replace("/Assets", "");

                try
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        return error;
                    }

                    return output.Trim();
                }
                catch (System.Exception e)
                {
                    return $"Error executing git command: {e.Message}";
                }
            }
        }
    }
}
