# RUNBOOK

## Проверка статуса
su - deployer
cd ~/deploy/is-stack
docker compose ps

## Логи
docker compose logs --tail 100 app
docker compose logs --tail 100 mssql

## Проверка доступности
curl -k https://192.168.1.74/version
curl -k https://192.168.1.74/health
curl -k https://192.168.1.74/db/ping

## Обновление
Изменить tag образа app в docker-compose.yml, затем:
docker compose pull app
docker compose up -d app

## Откат
Вернуть предыдущий рабочий tag образа, затем:
docker compose pull app
docker compose up -d app

## Backup MSSQL
docker run --rm --network is-stack_default -e SA_PASSWORD="$SA_PASSWORD" mcr.microsoft.com/mssql-tools sh -lc "/opt/mssql-tools/bin/sqlcmd -S mssql -U sa -P \"$SA_PASSWORD\" -d master -Q \"BACKUP DATABASE [IsLabDb] TO DISK = N'/var/opt/mssql/backup/IsLabDb_full.bak' WITH INIT, COMPRESSION, STATS = 5;\""

## Restore test
docker run --rm --network is-stack_default -e SA_PASSWORD="$SA_PASSWORD" mcr.microsoft.com/mssql-tools sh -lc "/opt/mssql-tools/bin/sqlcmd -S mssql -U sa -P \"$SA_PASSWORD\" -d master -Q \"IF DB_ID(N'IsLabDb_RestoreTest') IS NOT NULL BEGIN ALTER DATABASE [IsLabDb_RestoreTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [IsLabDb_RestoreTest]; END; RESTORE DATABASE [IsLabDb_RestoreTest] FROM DISK = N'/var/opt/mssql/backup/IsLabDb_full.bak' WITH MOVE N'IsLabDb' TO N'/var/opt/mssql/data/IsLabDb_RestoreTest.mdf', MOVE N'IsLabDb_log' TO N'/var/opt/mssql/data/IsLabDb_RestoreTest_log.ldf', REPLACE, STATS = 5;\""

## Политика хранения
ls -1t /opt/backups/mssql/*.bak | tail -n +6 | xargs -r sudo rm -f
