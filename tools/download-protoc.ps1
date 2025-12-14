# Protobuf Compiler 다운로드 스크립트
# Protocol Buffer 컴파일러(protoc)를 자동으로 다운로드하고 설치합니다

$ErrorActionPreference = "Stop"

$PROTOC_VERSION = "28.3"
$DOWNLOAD_URL = "https://github.com/protocolbuffers/protobuf/releases/download/v${PROTOC_VERSION}/protoc-${PROTOC_VERSION}-win64.zip"
$TOOLS_DIR = $PSScriptRoot
$TEMP_ZIP = Join-Path $TOOLS_DIR "protoc.zip"

Write-Host "Protobuf Compiler v${PROTOC_VERSION} 다운로드 중..." -ForegroundColor Cyan

try {
    # protoc.exe가 이미 있으면 건너뛰기
    $protocExe = Join-Path $TOOLS_DIR "protoc.exe"
    if (Test-Path $protocExe) {
        Write-Host "protoc.exe가 이미 존재합니다: $protocExe" -ForegroundColor Yellow
        Write-Host "기존 파일을 삭제하고 다시 다운로드하려면 protoc.exe를 먼저 삭제하세요." -ForegroundColor Yellow
        exit 0
    }

    # ZIP 파일 다운로드
    Write-Host "다운로드 URL: $DOWNLOAD_URL" -ForegroundColor Gray
    Invoke-WebRequest -Uri $DOWNLOAD_URL -OutFile $TEMP_ZIP -UseBasicParsing

    # ZIP 압축 해제
    Write-Host "압축 해제 중..." -ForegroundColor Cyan
    $tempExtractDir = Join-Path $TOOLS_DIR "protoc_temp"
    Expand-Archive -Path $TEMP_ZIP -DestinationPath $tempExtractDir -Force

    # bin/protoc.exe를 tools 디렉토리로 이동
    $extractedProtoc = Join-Path $tempExtractDir "bin\protoc.exe"
    if (Test-Path $extractedProtoc) {
        Move-Item -Path $extractedProtoc -Destination $protocExe -Force
        Write-Host "protoc.exe 설치 완료: $protocExe" -ForegroundColor Green
    } else {
        throw "압축 해제된 파일에서 protoc.exe를 찾을 수 없습니다"
    }

    # include 디렉토리도 복사 (google/protobuf/*.proto 파일들)
    $extractedInclude = Join-Path $tempExtractDir "include"
    $toolsInclude = Join-Path $TOOLS_DIR "include"
    if (Test-Path $extractedInclude) {
        if (Test-Path $toolsInclude) {
            Remove-Item -Path $toolsInclude -Recurse -Force
        }
        Move-Item -Path $extractedInclude -Destination $toolsInclude -Force
        Write-Host "include 디렉토리 복사 완료" -ForegroundColor Green
    }

    # 임시 파일 정리
    Remove-Item -Path $TEMP_ZIP -Force
    Remove-Item -Path $tempExtractDir -Recurse -Force

    Write-Host ""
    Write-Host "설치 완료!" -ForegroundColor Green
    Write-Host "protoc 버전 확인:" -ForegroundColor Cyan
    & $protocExe --version

} catch {
    Write-Host "오류 발생: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "수동 설치 방법:" -ForegroundColor Yellow
    Write-Host "1. 다음 URL에서 protoc-${PROTOC_VERSION}-win64.zip 다운로드:" -ForegroundColor Gray
    Write-Host "   https://github.com/protocolbuffers/protobuf/releases" -ForegroundColor Gray
    Write-Host "2. 압축 해제 후 bin/protoc.exe를 tools/ 디렉토리에 복사" -ForegroundColor Gray
    exit 1
}
