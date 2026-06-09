#!/bin/bash

/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

echo "[db] Esperando que SQL Server arranque..."
READY=0
for i in $(seq 1 30); do
    if /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$SA_PASSWORD" -Q "SELECT 1" -C > /dev/null 2>&1; then
        READY=1
        break
    fi
    echo "[db] Intento $i/30, reintentando en 2s..."
    sleep 2
done

if [ $READY -eq 1 ]; then
    DB_EXISTS=$(/opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U SA -P "$SA_PASSWORD" \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='UrlShortenerDB'" \
        -h -1 -C 2>/dev/null | tr -d ' \r\n')

    if [ "$DB_EXISTS" = "0" ]; then
        echo "[db] Inicializando base de datos..."
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$SA_PASSWORD" -i /init.sql -C
        echo "[db] Base de datos creada correctamente."
    else
        echo "[db] La base de datos ya existe, omitiendo init."
    fi
else
    echo "[db] ERROR: SQL Server no respondio en el tiempo esperado."
fi

wait $MSSQL_PID
