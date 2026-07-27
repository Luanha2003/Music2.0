$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appUrl = "http://localhost:5090"
$appExe = Join-Path $projectRoot "bin\Release\net9.0\Music2.0.exe"
$stateDirectory = Join-Path $env:LOCALAPPDATA "Music2.0"
$pidFile = Join-Path $stateDirectory "Music2.0.pid"

function Test-MusicApp {
    try {
        $listener = Get-NetTCPConnection `
            -LocalPort 5090 `
            -State Listen `
            -ErrorAction Stop |
            Select-Object -First 1
        $process = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
        return [System.IO.Path]::GetFullPath($process.Path).Equals(
            [System.IO.Path]::GetFullPath($appExe),
            [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        return $false
    }
}

function Show-LauncherError([string]$message) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        $message,
        "Music2.0",
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error) | Out-Null
}

try {
    if (-not (Test-MusicApp)) {
        $listener = Get-NetTCPConnection `
            -LocalPort 5090 `
            -State Listen `
            -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($listener) {
            throw "Cổng 5090 đang được một chương trình khác sử dụng."
        }

        if (-not (Test-Path -LiteralPath $appExe)) {
            throw "Chưa tìm thấy bản Release của Music2.0. Hãy build dự án trước."
        }

        $env:ASPNETCORE_URLS = "http://127.0.0.1:5090"
        $env:ASPNETCORE_ENVIRONMENT = "Development"

        $process = Start-Process `
            -FilePath $appExe `
            -WorkingDirectory $projectRoot `
            -WindowStyle Hidden `
            -PassThru

        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
        Set-Content -LiteralPath $pidFile -Value $process.Id

        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if (Test-MusicApp) {
                $ready = $true
                break
            }

            if ($process.HasExited) {
                break
            }

            Start-Sleep -Milliseconds 500
        }

        if (-not $ready) {
            throw "Music2.0 không thể khởi động. Hãy chạy lại hoặc kiểm tra bản build Release."
        }
    }

    Start-Process $appUrl
}
catch {
    Show-LauncherError $_.Exception.Message
    exit 1
}
