#!/usr/bin/env bash
# Builds the add-in and copies the result to dist/. Works on Windows (dotnet SDK 8) and on Linux
# (the .NET SDK needs the WindowsDesktop Sdk folder from a Microsoft build of the SDK, see README).
set -euo pipefail
cd "$(dirname "$0")"
dotnet build src/ProjectBrowserPlus/ProjectBrowserPlus.csproj -c Release
# XAML that compiles but would crash at runtime (e.g. a Style setting a non-dependency property)
REF="$HOME/.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/.NETFramework/v4.8"
dotnet build tools/XamlCheck -c Release -v q -nologo >/dev/null
dotnet tools/XamlCheck/bin/Release/net8.0/XamlCheck.dll "$REF" src/ProjectBrowserPlus/bin/Release/net48/ProjectBrowserPlus.dll $(find src -name "*.xaml" ! -name Icons.xaml)
mkdir -p dist
cp src/ProjectBrowserPlus/bin/Release/net48/ProjectBrowserPlus.dll dist/
cp src/ProjectBrowserPlus/bin/Release/net48/ProjectBrowserPlus.pdb dist/
cp src/ProjectBrowserPlus/ProjectBrowserPlus.addin dist/
echo "dist/ ready"
