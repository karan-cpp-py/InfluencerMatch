param(
    [string]$Host = "localhost",
    [string]$Port = "5432",
    [string]$Database = "InfluencerMatchDb",
    [string]$Username = "postgres",
    [string]$BackupFolder = "E:\InfluencerAI\backups",
    [int]$RetentionDays = 14
)

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFile = Join-Path $BackupFolder "$Database-$timestamp.dump"

if (-not (Test-Path $BackupFolder)) {
    New-Item -Path $BackupFolder -ItemType Directory | Out-Null
}

Write-Host "Creating backup: $backupFile"
pg_dump -h $Host -p $Port -U $Username -Fc -f $backupFile $Database

if ($LASTEXITCODE -ne 0) {
    throw "Backup failed."
}

Write-Host "Applying retention policy ($RetentionDays days)..."
Get-ChildItem -Path $BackupFolder -Filter "$Database-*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } |
    Remove-Item -Force

Write-Host "Backup completed successfully."
