$apikey = Get-Content -Path "D:\Secrets\nugetapikey.txt"
Write-Host $apikey

$currentDirectory = Get-Location
Write-Host $currentDirectory
#Write-Host "currentDirectory: $($currentDirectory)"

# 读取项目文件内容
$projectFileContent = Get-Content -Path "../Sylas.RemoteTasks.Utils/Sylas.RemoteTasks.Utils.csproj"

# 使用正则表达式匹配版本号
$versionPattern = '<Version>(.*?)<\/Version>'
$versionMatch = [regex]::Match($projectFileContent, $versionPattern)
if (!$versionMatch.Success) {
	Write-Host "没有找到版本号"
	exit 1
}
# 提取版本号值
$version = $versionMatch.Groups[1].Value

# 输出版本号
Write-Host "版本号：$version"

$utilsProjDir = Join-Path -Path $currentDirectory -ChildPath ../Sylas.RemoteTasks.Utils
Set-Location $utilsProjDir
dotnet build -c=Release
dotnet nuget push ./bin/Release/Sylas.RemoteTasks.Utils.$version.nupkg -k $apikey -s https://api.nuget.org/v3/index.json --skip-duplicate
Read-Host "Press any key to exit"
#if ($reply -eq "EXIT") { exit; }