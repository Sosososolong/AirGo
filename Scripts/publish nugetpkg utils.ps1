$apikey = Get-Content -Path "D:\Secrets\nugetapikey.txt"
Write-Host $apikey

$currentDirectory = Get-Location
Write-Host $currentDirectory
#Write-Host "currentDirectory: $($currentDirectory)"

# ��ȡ��Ŀ�ļ�����
$projectFileContent = Get-Content -Path "../Sylas.RemoteTasks.Utils/Sylas.RemoteTasks.Utils.csproj"

# ʹ��������ʽƥ��汾��
$versionPattern = '<Version>(.*?)<\/Version>'
$versionMatch = [regex]::Match($projectFileContent, $versionPattern)
if (!$versionMatch.Success) {
	Write-Host "û���ҵ��汾��"
	exit 1
}
# ��ȡ�汾��ֵ
$version = $versionMatch.Groups[1].Value

# ����汾��
Write-Host "�汾�ţ�$version"

$utilsProjDir = Join-Path -Path $currentDirectory -ChildPath ../Sylas.RemoteTasks.Utils
Set-Location $utilsProjDir
dotnet build -c=Release
dotnet nuget push ./bin/Release/Sylas.RemoteTasks.Utils.$version.nupkg -k $apikey -s https://api.nuget.org/v3/index.json --skip-duplicate
Read-Host "Press any key to exit"
#if ($reply -eq "EXIT") { exit; }