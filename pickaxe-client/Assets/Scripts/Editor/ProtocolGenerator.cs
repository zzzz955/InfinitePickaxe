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
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string protoFile = Path.GetFullPath(Path.Combine(projectRoot, "protocol", "game.proto"));
            string protoDir = Path.GetDirectoryName(protoFile);
            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "Generated"));

            // Windows 경로로 정규화 (백슬래시 통일)
            protoFile = NormalizePath(protoFile);
            protoDir = NormalizePath(protoDir);
            outputDir = NormalizePath(outputDir);

            // 출력 디렉토리 생성
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // proto 파일 존재 확인
            if (!File.Exists(protoFile))
            {
                UnityEngine.Debug.LogError($"proto 파일을 찾을 수 없습니다: {protoFile}");
                return;
            }

            // protoc 실행 경로 (Windows 기준)
            string protocPath = FindProtoc();
            if (string.IsNullOrEmpty(protocPath))
            {
                UnityEngine.Debug.LogError("protoc를 찾을 수 없습니다.\n\n" +
                    "1. protoc을 설치하세요: https://github.com/protocolbuffers/protobuf/releases\n" +
                    "2. protoc.exe를 PATH에 추가하거나\n" +
                    "3. 프로젝트 루트에 tools/protoc.exe를 배치하세요");
                return;
            }

            protocPath = NormalizePath(protocPath);

            // protoc 명령어 구성
            string arguments = $"--csharp_out=\"{outputDir}\" --proto_path=\"{protoDir}\" \"{protoFile}\"";

            UnityEngine.Debug.Log($"프로토콜 생성 중...\nprotoc: {protocPath}\n명령어: {arguments}");

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

        /// <summary>
        /// protoc 실행 파일 경로를 찾습니다
        /// </summary>
        private static string FindProtoc()
        {
            // 1. 프로젝트 루트의 tools 디렉토리에서 찾기
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string localProtoc = Path.Combine(projectRoot, "tools", "protoc.exe");
            if (File.Exists(localProtoc))
            {
                return localProtoc;
            }

            // 2. PATH 환경변수에서 찾기
            string pathEnv = System.Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(';');
                foreach (string path in paths)
                {
                    string protocExe = Path.Combine(path.Trim(), "protoc.exe");
                    if (File.Exists(protocExe))
                    {
                        return protocExe;
                    }
                }
            }

            // 3. 일반적인 설치 경로들 확인
            string[] commonPaths = new string[]
            {
                @"C:\protoc\bin\protoc.exe",
                @"C:\Program Files\protoc\bin\protoc.exe",
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), @".protoc\bin\protoc.exe")
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Windows 경로로 정규화 (슬래시를 백슬래시로 변환)
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 슬래시를 백슬래시로 변환
            return path.Replace('/', '\\');
        }
    }
}
