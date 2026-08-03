# Prueba que un backup se pueda restaurar de verdad. Lo carga en una base
# APARTE llamada "odonto_prueba_restore" -- nunca toca tu base real
# "odonto" -- asi podes confirmar que el archivo sirve sin arriesgar nada.
#
# Uso:
#   .\restaurar-prueba.ps1 .\backups\odonto_backup_2026-08-03_10-00.sql
#
# Hace esto de vez en cuando (por ejemplo, una vez por mes) con el backup
# mas reciente. Un backup que nunca se probo no sirve.

param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivoBackup
)

$ErrorActionPreference = "Stop"

# Ruta al mysql.exe de tu instalacion de MySQL Server (no el de MySQL
# Workbench). Si en algun momento cambia la version instalada, ajustar aca.
$mysql = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"

$credenciales = Join-Path $PSScriptRoot "mysql-backup.cnf"

if (-not (Test-Path $ArchivoBackup)) {
    Write-Host "No encontre el archivo: $ArchivoBackup" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $credenciales)) {
    Write-Host "Falta el archivo mysql-backup.cnf con las credenciales." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $mysql)) {
    Write-Host "No encontre mysql.exe en $mysql. Ajusta la variable `$mysql al principio del script." -ForegroundColor Red
    exit 1
}

Write-Host "Creando base de prueba 'odonto_prueba_restore' (si ya existia, se borra y se crea vacia otra vez)..."
& $mysql --defaults-extra-file="$credenciales" -e "DROP DATABASE IF EXISTS odonto_prueba_restore; CREATE DATABASE odonto_prueba_restore;"

Write-Host "Restaurando '$ArchivoBackup' ahi..."
Get-Content $ArchivoBackup -Raw | & $mysql --defaults-extra-file="$credenciales" odonto_prueba_restore

Write-Host "Verificando que haya datos..."
& $mysql --defaults-extra-file="$credenciales" odonto_prueba_restore -e "SELECT COUNT(*) AS Pacientes FROM Pacientes; SELECT COUNT(*) AS Usuarios FROM Usuarios; SELECT COUNT(*) AS Turnos FROM Turnos;"

Write-Host ""
Write-Host "Si arriba ves numeros (no un error de MySQL), el backup se restaura bien." -ForegroundColor Green
Write-Host "Cuando termines de revisar, borra la base de prueba con:"
Write-Host "  & `"$mysql`" --defaults-extra-file=`"$credenciales`" -e `"DROP DATABASE odonto_prueba_restore;`""
