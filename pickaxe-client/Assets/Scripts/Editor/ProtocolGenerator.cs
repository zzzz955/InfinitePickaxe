using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;

namespace InfinitePickaxe.Client.Editor
{
    /// <summary>
    /// Protobuf 프로토콜을 C# 코드로 생성하는 에디터 도구
    /// </summary>
    public class ProtocolGenerator : EditorWindow
    {
        [MenuItem("Tools/Protocol/Generate C# from Proto")]
        public static void GenerateProtocol()
        {
            // 프로젝트 루트 경로 (Assets의 상위 폴더의 상위 폴더)
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string protoFile = Path.Combine(projectRoot, "protocol/game.proto");
            string outputDir = Path.Combine(Application.dataPath, "Scripts/Generated");

            // 출력 디렉토리 생성
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // protoc 실행 경로 (Windows 기준)
            string protocPath = "protoc"; // PATH에 protoc이 있다고 가정

            // protoc 명령어 구성
            string arguments = $"--csharp_out=\"{outputDir}\" --proto_path=\"{Path.GetDirectoryName(protoFile)}\" \"{protoFile}\"";

            UnityEngine.Debug.Log($"프로토콜 생성 중...\nprotoc {arguments}");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = protocPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        UnityEngine.Debug.Log($"프로토콜 생성 완료!\n출력 경로: {outputDir}\n{output}");
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"프로토콜 생성 실패!\n{error}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"protoc 실행 실패: {ex.Message}\n\nprotoc이 설치되어 있고 PATH에 등록되어 있는지 확인하세요.\n다운로드: https://github.com/protocolbuffers/protobuf/releases");
            }
        }
    }
}
