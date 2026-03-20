set windows-shell := ["powershell.exe", "-NoProfile", "-Command"]

project := "src/inputor.WinUI/inputor.WinUI.csproj"
exe := "src/inputor.WinUI/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/inputor.App.exe"

default:
    just --list

build:
    dotnet build inputor.sln

launch:
    if (!(Test-Path '{{exe}}')) { throw 'Build output not found. Run `just build` first.' }; $resolved = Resolve-Path '{{exe}}'; Start-Process -FilePath $resolved

dev:
    $running = @(Get-Process inputor.App -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0); foreach ($process in $running) { try { Stop-Process -Id $process.Id -ErrorAction Stop } catch { Write-Host "Skipping process $($process.Id)" } }; $global:LASTEXITCODE = 0; if (-not $?) { exit 0 }
    dotnet build inputor.sln
    if (!(Test-Path '{{exe}}')) { throw 'Build output not found after build.' }; $resolved = Resolve-Path '{{exe}}'; Start-Process -FilePath $resolved

run-cli *args:
    dotnet run --project {{project}} -- {{args}}
