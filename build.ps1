$ErrorActionPreference="Stop"
dotnet restore .\Vimon.sln
dotnet build .\Vimon.sln -c Release
dotnet publish .\src\Vimon.Cli\Vimon.Cli.csproj -c Release -f net8.0-windows -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
Write-Host "Built: src\Vimon.Cli\bin\Release\net8.0-windows\win-x64\publish\vimon.exe" -ForegroundColor Green
