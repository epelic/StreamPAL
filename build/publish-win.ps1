$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\StreamForge.App\StreamForge.App.csproj'
$output = Join-Path $PSScriptRoot '..\outputs\StreamPAL-win-x64'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $output
Write-Host "Build creato in $output"
