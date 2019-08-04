@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

rmdir /s /q releases 
md releases

rmdir /s /q .\SKProCH's Discord Bot\bin\release 

set winOS=win-x64
set unixOS=osx-x64 linux-x64

for %%N in (%winOS%) do (
	set rid=linux-x64
	dotnet publish ./SKProCH's Discord Bot/SKProCH's Discord Bot.csproj -c release -r linux-x64 /p:PackAsTool=false
	7z a -tzip ./releases/SKProCH's Discord Bot-!rid!.zip ./SKProCH's Discord Bot/bin/release/netcoreapp2.2/!rid!/publish/* -r
	pause >nul
)

for %%N in (%unixOS%) do (
	set rid=%%N
	dotnet publish ./SKProCH's Discord Bot/SKProCH's Discord Bot.csproj -c release -r !rid! /p:PackAsTool=false
	7z a -ttar -so ./releases/SKProCH's Discord Bot-!rid!.tar ./SKProCH's Discord Bot/bin/release/netcoreapp2.2/!rid!/publish/* -r | 7z a -si ./releases/SKProCH's Discord Bot-!rid!.tar.gz
)
pause >nul