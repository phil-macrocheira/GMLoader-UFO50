@echo off

:: Publish
dotnet msbuild -t:PublishAll

:: Copy to Mod Loader
robocopy "publish\win-x64" "C:\Users\Phil\source\repos\UFO 50 Mod Loader\UFO 50 Mod Loader\bin\Debug\net9.0\win-x64\GMLoader" /E /NJH /NJS /NDL /NC /NS /NP >nul
robocopy "publish\win-x64" "C:\Users\Phil\source\repos\UFO 50 Mod Loader\GMLoader" /E /NJH /NJS /NDL /NC /NS /NP >nul

pause