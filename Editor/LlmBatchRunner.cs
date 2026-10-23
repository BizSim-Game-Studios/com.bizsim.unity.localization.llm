using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;

namespace BizSim.Unity.Localization.Llm.Editor
{
    /// <summary>
    /// Runs the studio llm-batch tool. Its path and the Python executable are per machine
    /// (EditorPrefs); the tool reads its own API key, so nothing secret passes through Unity.
    /// </summary>
    public static class LlmBatchRunner
    {
        const string ToolPathPref = "BizSim.LocalizationLlm.ToolPath";
        const string PythonPref = "BizSim.LocalizationLlm.Python";
        const string DefaultTool = "D:/Projects/studio-ops/shared-tools/llm-batch/llm_batch.py";

        public static string ToolPath
        {
            get => EditorPrefs.GetString(ToolPathPref, DefaultTool);
            set => EditorPrefs.SetString(ToolPathPref, value);
        }

        public static string Python
        {
            get => EditorPrefs.GetString(PythonPref, "python");
            set => EditorPrefs.SetString(PythonPref, value);
        }

        /// <summary>Returns (exit code, stdout + stderr).</summary>
        public static (int code, string output) Run(params string[] args)
        {
            if (!File.Exists(ToolPath))
                return (-1, "llm-batch not found at " + ToolPath + " (set it in the window)");
            var psi = new ProcessStartInfo
            {
                FileName = Python,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            var line = new StringBuilder(Quote(ToolPath));
            foreach (var a in args)
                line.Append(' ').Append(Quote(a));
            psi.Arguments = line.ToString();
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return (p.ExitCode, (stdout + stderr).Trim());
            }
        }

        // Windows command-line quoting: wrap in quotes, escape embedded quotes. Paths here hold spaces
        // ("JYT BizSim") but never quotes or trailing backslashes.
        static string Quote(string arg) => "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
