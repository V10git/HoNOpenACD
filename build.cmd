@echo off
rem This script build HoNOpenACD to single, self-contained, windows 10 x64 executable
dotnet publish HoNOpenACD\HoNOpenACD.csproj -c PubRelease /p:PublishProfileFullPath=%cd%\HoNOpenACD\Properties\PublishProfiles\singlefile.pubxml /p:PublishProfile=singlefile.pubxml /p:PublishProfileRootFolder=%cd%\HoNOpenACD\Properties\PublishProfiles
