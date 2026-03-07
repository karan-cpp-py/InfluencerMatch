param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$Host = "localhost",
    [string]$Port = "5432",
    [string]$Database = "InfluencerMatchDb",
    [string]$Username = "postgres"
)

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

Write-Host "Restoring database $Database from $BackupFile"
pg_restore -h $Host -p $Port -U $Username -d $Database --clean --if-exists $BackupFile

if ($LASTEXITCODE -ne 0) {
    throw "Restore failed."
}

Write-Host "Restore completed successfully."
