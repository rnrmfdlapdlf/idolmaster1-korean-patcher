using System;
using System.Diagnostics;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class ExternalToolRunner
    {
        public static string Run(string fileName, string arguments, string workingDirectory, string displayName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                output.Append(process.StandardOutput.ReadToEnd());
                output.Append(process.StandardError.ReadToEnd());
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = output.ToString().Trim();
                    if (message.Length == 0)
                    {
                        message = "exit code " + process.ExitCode.ToString();
                    }

                    throw new InvalidOperationException(displayName + " 실행 실패: " + message);
                }
            }

            return output.ToString();
        }

        public static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
