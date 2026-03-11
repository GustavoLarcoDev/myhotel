#!/bin/bash
# ═══════════════════════════════════════════════════════════
# setup-server.sh — Configuración inicial del servidor Lightsail
# para MyHotel (ASP.NET Core 9 + SQLite)
#
# Ejecutar UNA SOLA VEZ en el servidor como root
# Usage: sudo bash setup-server.sh TU_DOMINIO
# ═══════════════════════════════════════════════════════════

set -euo pipefail

DOMAIN="${1:?Uso: sudo bash setup-server.sh midominio.com}"

echo "══════════════════════════════════════════"
echo " Configurando servidor para MyHotel"
echo " Dominio: $DOMAIN"
echo "══════════════════════════════════════════"

# ─── 1. Actualizar sistema ───
echo "[1/6] Actualizando sistema..."
apt-get update && apt-get upgrade -y

# ─── 2. Instalar .NET 9 Runtime ───
echo "[2/6] Instalando .NET 9..."
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb
dpkg -i /tmp/packages-microsoft-prod.deb
rm /tmp/packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-9.0

# ─── 3. Instalar Nginx ───
echo "[3/6] Instalando Nginx..."
apt-get install -y nginx
systemctl enable nginx

# ─── 4. Crear estructura de la app ───
echo "[4/6] Creando estructura de directorios..."
mkdir -p /opt/myhotel/releases
mkdir -p /opt/myhotel/backups
mkdir -p /opt/myhotel/data

# Permisos para www-data
chown -R www-data:www-data /opt/myhotel/data

# ─── 5. Crear script de deploy ───
echo "[5/6] Creando scripts..."

# ── Deploy script ──
cat > /opt/myhotel/deploy.sh << 'DEPLOY'
#!/bin/bash
# ═══════════════════════════════════════════════════════════
# deploy.sh — Deploy con rollback automático para MyHotel
# ═══════════════════════════════════════════════════════════
set -euo pipefail

RELEASE_DIR="/opt/myhotel/releases/$(date +%Y%m%d_%H%M%S)"
CURRENT_LINK="/opt/myhotel/current"
DEPLOY_SOURCE="/tmp/myhotel-deploy"
BACKUP_DIR="/opt/myhotel/backups"
DATA_DIR="/opt/myhotel/data"

PREVIOUS_RELEASE=$(readlink -f "$CURRENT_LINK" 2>/dev/null || echo "")

echo "══════════════════════════════════════════"
echo " Deploy MyHotel iniciado"
echo " Nuevo release: $RELEASE_DIR"
echo "══════════════════════════════════════════"

rollback() {
    echo "" >&2
    echo "══════════════════════════════════════════" >&2
    echo " ROLLBACK AUTOMÁTICO INICIADO" >&2
    echo "══════════════════════════════════════════" >&2
    if [ -n "$PREVIOUS_RELEASE" ] && [ -d "$PREVIOUS_RELEASE" ]; then
        ln -sfn "$PREVIOUS_RELEASE" "${CURRENT_LINK}.tmp"
        mv -Tf "${CURRENT_LINK}.tmp" "$CURRENT_LINK"
        systemctl restart myhotel
        sleep 5
        if systemctl is-active --quiet myhotel; then
            echo "Rollback exitoso a: $PREVIOUS_RELEASE" >&2
        else
            echo "CRÍTICO: Rollback falló. Intervención manual requerida." >&2
            echo "  Revisa: sudo journalctl -u myhotel -n 50" >&2
        fi
    else
        echo "No hay release anterior para rollback." >&2
    fi
    exit 1
}

# PASO 1: Backup de SQLite
echo "[1/6] Backup de base de datos..."
if [ -f "$DATA_DIR/myhotel.db" ]; then
    BACKUP_FILE="$BACKUP_DIR/myhotel_$(date +%Y%m%d_%H%M%S).db"
    cp "$DATA_DIR/myhotel.db" "$BACKUP_FILE"
    echo "  Backup: $BACKUP_FILE"
else
    echo "  Primera ejecución, no hay BD que respaldar"
fi

# PASO 2: Copiar archivos
echo "[2/6] Copiando archivos del nuevo release..."
cp -r "$DEPLOY_SOURCE" "$RELEASE_DIR"

if [ ! -f "$RELEASE_DIR/MyHotel.Web.dll" ]; then
    echo "ERROR: MyHotel.Web.dll no encontrado!" >&2
    rm -rf "$RELEASE_DIR"
    exit 1
fi

chown -R www-data:www-data "$RELEASE_DIR"

# PASO 3: Swap atómico
echo "[3/6] Cambiando symlink..."
ln -sfn "$RELEASE_DIR" "${CURRENT_LINK}.tmp"
mv -Tf "${CURRENT_LINK}.tmp" "$CURRENT_LINK"

# PASO 4: Restart
echo "[4/6] Reiniciando servicio..."
systemctl restart myhotel

# PASO 5: Verificar servicio
echo "[5/6] Verificando servicio..."
sleep 5
if ! systemctl is-active --quiet myhotel; then
    echo "FALLO: Servicio no arrancó." >&2
    rollback
fi

# PASO 6: Health check
echo "[6/6] Health check..."
sleep 3
if ! curl -sf --max-time 10 http://localhost:5000/health > /dev/null 2>&1; then
    echo "FALLO: Health check no respondió." >&2
    rollback
fi

# Limpieza: mantener 5 releases
cd /opt/myhotel/releases
ls -dt */ | tail -n +6 | xargs -r rm -rf

# Mantener 7 backups
ls -dt "$BACKUP_DIR"/myhotel_*.db 2>/dev/null | tail -n +8 | xargs -r rm -f

rm -rf "$DEPLOY_SOURCE"

echo ""
echo "══════════════════════════════════════════"
echo " Deploy completado exitosamente"
echo "══════════════════════════════════════════"
DEPLOY

chmod +x /opt/myhotel/deploy.sh

# ── Backup script ──
cat > /opt/myhotel/backup-db.sh << 'BACKUP'
#!/bin/bash
DATA_DIR="/opt/myhotel/data"
BACKUP_DIR="/opt/myhotel/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
if [ -f "$DATA_DIR/myhotel.db" ]; then
    cp "$DATA_DIR/myhotel.db" "$BACKUP_DIR/myhotel_daily_$TIMESTAMP.db"
    # Mantener últimos 7 backups diarios
    find "$BACKUP_DIR" -name "myhotel_daily_*.db" -mtime +7 -delete
fi
BACKUP
chmod +x /opt/myhotel/backup-db.sh

# Cron: backup diario a las 3:00 AM
(crontab -l 2>/dev/null; echo "0 3 * * * /opt/myhotel/backup-db.sh >> /var/log/myhotel-backup.log 2>&1") | crontab -

# ─── 6. Servicios del sistema ───
echo "[6/6] Configurando systemd + Nginx..."

# ── Systemd service ──
cat > /etc/systemd/system/myhotel.service << SERVICE
[Unit]
Description=MyHotel ASP.NET Core App
After=network.target

[Service]
WorkingDirectory=/opt/myhotel/current
ExecStart=/usr/bin/dotnet /opt/myhotel/current/MyHotel.Web.dll
Restart=always
RestartSec=5
SyslogIdentifier=myhotel
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ConnectionStrings__DefaultConnection=Data Source=/opt/myhotel/data/myhotel.db

[Install]
WantedBy=multi-user.target
SERVICE

systemctl daemon-reload
systemctl enable myhotel

# ── Nginx config ──
cat > /etc/nginx/sites-available/myhotel << NGINX
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        proxy_buffering off;
        client_max_body_size 10M;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/myhotel /etc/nginx/sites-enabled/myhotel
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl restart nginx

# Instalar certbot para HTTPS
apt-get install -y certbot python3-certbot-nginx

echo ""
echo "══════════════════════════════════════════"
echo " SETUP COMPLETO"
echo "══════════════════════════════════════════"
echo ""
echo " Siguiente paso: hacer primer deploy"
echo " desde GitHub (git push main) y luego:"
echo ""
echo "   sudo certbot --nginx -d $DOMAIN -d www.$DOMAIN"
echo ""
echo " para activar HTTPS gratis."
echo ""
echo "══════════════════════════════════════════"
