# Deploy MyHotel a Amazon Lightsail — Guia Paso a Paso

## Costo: ~$10/mes (instancia + IP estatica)

SQLite no necesita SQL Server, asi que una instancia mas pequena es suficiente.

---

## PASO 1: Crear cuenta AWS

1. Ve a https://aws.amazon.com
2. Click "Create an AWS Account"
3. Pon tu email, nombre, tarjeta de credito
4. Verificacion por telefono
5. Selecciona el plan "Basic Support (Free)"

---

## PASO 2: Crear instancia Lightsail

1. Ve a https://lightsail.aws.amazon.com
2. Click **"Create instance"**
3. Configuracion:
   - Region: **Oregon (us-west-2)** o la mas cercana
   - Platform: **Linux/Unix**
   - Blueprint: **OS Only -> Ubuntu 22.04 LTS**
   - Instance plan: **$10 USD/month** (2 vCPUs, 2 GB RAM, 60 GB SSD)
   - Instance name: `myhotel-prod`
4. Click **"Create instance"**
5. Espera 2 minutos

---

## PASO 3: IP estatica

1. En Lightsail, ve a **Networking** tab
2. Click **"Create static IP"**
3. Attach to instance: `myhotel-prod`
4. Name: `myhotel-ip`
5. Click **"Create"**
6. **ANOTA LA IP** (ej: `44.230.xxx.xxx`)

---

## PASO 4: Abrir puertos

1. En tu instancia -> tab **Networking**
2. En **IPv4 Firewall**, agrega:
   - SSH (22) - ya viene
   - HTTP (80) - ya viene
   - HTTPS (443) - **click "Add rule"**, selecciona HTTPS

---

## PASO 5: Apuntar dominio

En tu registrador de dominio (GoDaddy, Namecheap, etc.):

| Tipo | Host | Valor | TTL |
|------|------|-------|-----|
| A | @ | `TU_IP_ESTATICA` | 300 |
| A | www | `TU_IP_ESTATICA` | 300 |

Espera 5-15 minutos a que propague.

---

## PASO 6: Descargar SSH key

1. En Lightsail -> **Account** -> **SSH keys**
2. Click **"Download"** en la key de tu region
3. Se descarga un archivo `.pem`
4. **GUARDA ESTE ARCHIVO**

---

## PASO 7: Crear repo en GitHub

```bash
cd ~/Desktop/my-hotel/MyHotel.Web
git init
git add .
git commit -m "initial commit"
```

1. Crea un repo nuevo en GitHub (ej: `MyHotel`)
2. Conecta:
```bash
git remote add origin https://github.com/TU_USUARIO/MyHotel.git
git branch -M main
git push -u origin main
```

---

## PASO 8: Configurar GitHub Secrets

1. Ve a tu repo en GitHub
2. **Settings** -> **Secrets and variables** -> **Actions**
3. Agrega estos 2 secrets:

| Nombre | Valor |
|--------|-------|
| `SSH_PRIVATE_KEY` | Contenido completo del archivo `.pem` |
| `SERVER_HOST` | Tu IP estatica (ej: `44.230.xxx.xxx`) |

---

## PASO 9: Setup del servidor (UNA SOLA VEZ)

### 9.1 Conectarte

```bash
chmod 400 ~/Downloads/LightsailDefaultKey-us-west-2.pem
ssh -i ~/Downloads/LightsailDefaultKey-us-west-2.pem ubuntu@TU_IP_ESTATICA
```

### 9.2 Subir el script

Desde OTRA terminal local:

```bash
scp -i ~/Downloads/LightsailDefaultKey-us-west-2.pem \
  ~/Desktop/my-hotel/MyHotel.Web/deploy/setup-server.sh \
  ubuntu@TU_IP_ESTATICA:/tmp/
```

### 9.3 Ejecutar el setup

De vuelta en el servidor:

```bash
sudo bash /tmp/setup-server.sh tudominio.com
```

El script tarda ~3-5 minutos. Cuando termine veras "SETUP COMPLETO".

---

## PASO 10: Primer deploy

```bash
git push origin main
```

GitHub Actions hace todo automaticamente (~2-3 min).

Ve a https://github.com/TU_USUARIO/MyHotel/actions para ver el progreso.

### Verificar

En tu navegador: `http://TU_IP_ESTATICA`

Si no funciona:
```bash
sudo journalctl -u myhotel -n 50
```

---

## PASO 11: Activar HTTPS

SSH al servidor:

```bash
sudo certbot --nginx -d tudominio.com -d www.tudominio.com
```

---

## LISTO

### Deploy futuro

```bash
git add -A
git commit -m "descripcion del cambio"
git push origin main
# GitHub Actions se encarga del resto (~2-3 min)
```

### Comandos utiles (SSH al servidor)

```bash
# Logs en tiempo real
sudo journalctl -u myhotel -f

# Reiniciar app
sudo systemctl restart myhotel

# Status
sudo systemctl status myhotel

# Backup manual
sudo /opt/myhotel/backup-db.sh

# Ver backups
ls -lh /opt/myhotel/backups/

# Disco
df -h

# RAM
free -h
```

### Estructura en el servidor

```
/opt/myhotel/
├── current -> releases/20260308_143000/    <- version activa
├── releases/
│   ├── 20260308_143000/                    <- ultima version
│   └── ...                                 <- max 5 releases
├── data/
│   └── myhotel.db                          <- base de datos SQLite
├── backups/
│   └── myhotel_20260308_030000.db          <- backup diario
├── deploy.sh                               <- script de deploy
└── backup-db.sh                            <- script de backup
```
