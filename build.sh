#!/usr/bin/env bash
# Builds the add-in and copies the result to dist/. Works on Windows (dotnet SDK 8) and on Linux
# (the .NET SDK needs the WindowsDesktop Sdk folder from a Microsoft build of the SDK, see README).
set -euo pipefail
cd "$(dirname "$0")"
dotnet build src/ProjectBrowserPlus/ProjectBrowserPlus.csproj -c Release
mkdir -p dist
cp src/ProjectBrowserPlus/bin/Release/net48/ProjectBrowserPlus.dll dist/
cp src/ProjectBrowserPlus/bin/Release/net48/ProjectBrowserPlus.pdb dist/
cp src/ProjectBrowserPlus/ProjectBrowserPlus.addin dist/
echo "dist/ ready"
