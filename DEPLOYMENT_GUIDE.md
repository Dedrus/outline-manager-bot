# Complete Deployment Guide for Outline Manager Bot

This guide provides step-by-step instructions for deploying the complete Outline VPN solution with Telegram bot management on Ubuntu 24.04.

## Overview

The deployment consists of two independent components:
1. **Outline Server** - VPN server that handles traffic routing
2. **Telegram Bot** - Management interface for users and keys

Both components can be deployed on the same server or different servers for better scalability.

## Prerequisites

- Ubuntu 24.04 server with root or sudo access
- Public IP address with open ports
- Domain name (recommended but optional)
- Telegram account for bot administration

## Step 1: Install Outline Server

### Option A: Using Official Installation Script (Recommended)

1. Connect to your Ubuntu server via SSH
2. Run the official installation script:
   ```bash
   wget -qO- https://raw.githubusercontent.com/Jigsaw-Code/outline-server/master/src/server_manager/install_scripts/install_server.sh | bash
   ```

3. Save the output which contains important configuration details:
   ```json
   {
     "apiUrl": "https://YOUR_SERVER_IP:XXXXX/XXXXXXXXXXXXXXXXXXXXXX/",
     "certSha256": "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
   }
   ```

### Option B: Manual Installation with Docker

Refer to `OUTLINE_SERVER_INSTALLATION.md` for detailed manual installation instructions.

## Step 2: Configure Firewall

Ensure required ports are open:

```bash
# Outline Server ports (adjust according to your installation)
sudo ufw allow 53022/tcp
sudo ufw allow 59123:59222/tcp

# Enable firewall if not already enabled
sudo ufw enable
```

## Step 3: Test Outline Server

1. Verify the server is running:
   ```bash
   curl -k https://YOUR_SERVER_IP:XXXXX/XXXXXXXXXXXXXXXXXXXXXX/server
   ```

2. Test with Outline Manager application to ensure basic functionality

## Step 4: Prepare Telegram Bot

### Create a New Bot

1. Open Telegram and search for @BotFather
2. Start a chat and send `/newbot`
3. Follow the prompts to create a new bot
4. Save the provided bot token (format: `123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ`)

### Get Your Telegram User ID

1. Search for @userinfobot in Telegram
2. Start a chat and it will display your user ID
3. Save this ID as it will be used for admin access

## Step 5: Deploy Telegram Bot with Docker

### Method 1: Using Pre-built Image (Recommended)

1. Create a directory for the bot:
   ```bash
   mkdir -p ~/outline-bot
   cd ~/outline-bot
   ```

2. Create a `.env` file with your configuration:
   ```env
   TELEGRAM_BOT_TOKEN=your_actual_bot_token_here
   TELEGRAM_ADMIN_ID=your_telegram_user_id_here
   OUTLINE_API_URL=https://YOUR_SERVER_IP:XXXXX/XXXXXXXXXXXXXXXXXXXXXX/
   OUTLINE_CERT_SHA256=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
   DEFAULT_DATA_LIMIT_GB=100
   CHECK_INTERVAL_SECONDS=10
   UPDATE_INTERVAL_DAYS=30
   ```

3. Create `docker-compose.yml`:
   ```yaml
   version: '3.8'
   
   services:
     telegram-bot:
       image: ghcr.io/dedrus/outline-manager-bot:latest
       container_name: outline-telegram-bot
       restart: unless-stopped
       env_file:
         - .env
       volumes:
         - bot-data:/app/data
   
   volumes:
     bot-data:
       name: outline-bot-data
   ```

4. Run the bot:
   ```bash
   docker-compose up -d
   ```

### Method 2: Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/Dedrus/outline-manager-bot.git
   cd outline-manager-bot
   ```

2. Create a `.env` file with your configuration (same as above)

3. Build and run with docker-compose:
   ```bash
   docker-compose up -d --build
   ```

## Step 6: Verify Bot Deployment

1. Check container status:
   ```bash
   docker-compose ps
   ```

2. View logs:
   ```bash
   docker-compose logs -f
   ```

3. Test the bot:
   - Open Telegram
   - Search for your bot by username
   - Send `/start` command
   - Verify you receive a welcome message

## Step 7: Configure Bot Administration

1. Send `/help` to your bot to see available commands
2. As admin, you can now:
   - Add users with `/admin_add_user <telegram_id>`
   - Create keys for users
   - Broadcast messages with `/admin_broadcast <message>`
   - Monitor all keys with `/admin_all_keys`

## Step 8: Production Hardening

### Secure SSH Access

1. Change SSH port from default 22:
   ```bash
   sudo nano /etc/ssh/sshd_config
   # Change Port 22 to a custom port like 2222
   sudo systemctl restart ssh
   ```

2. Disable password authentication:
   ```bash
   # In /etc/ssh/sshd_config
   PasswordAuthentication no
   PubkeyAuthentication yes
   ```

### Enable Automatic Updates

```bash
sudo apt update
sudo apt install unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades
```

### Backup Strategy

1. Regular database backups:
   ```bash
   docker cp outline-telegram-bot:/app/data/vpn_bot.db ./backup-vpn_bot-$(date +%Y%m%d).db
   ```

2. Outline server configuration backup:
   ```bash
   sudo tar -czf outline-backup-$(date +%Y%m%d).tar.gz /opt/outline
   ```

## Step 9: Monitoring and Maintenance

### Monitor Container Health

```bash
# Check container status
docker-compose ps

# View resource usage
docker stats outline-telegram-bot

# Check logs
docker-compose logs --tail 100
```

### Restart Services

```bash
# Restart bot only
docker-compose restart telegram-bot

# Full restart
docker-compose down && docker-compose up -d
```

## Migration to New Server

### Migrating Outline Server

1. On old server, backup data:
   ```bash
   sudo tar -czf outline-backup.tar.gz /opt/outline
   ```

2. Transfer to new server:
   ```bash
   scp outline-backup.tar.gz user@new-server:/tmp/
   ```

3. On new server, restore data:
   ```bash
   sudo mkdir -p /opt/outline
   sudo tar -xzf /tmp/outline-backup.tar.gz -C /
   ```

4. Install Outline Server on new server using the same method

### Migrating Telegram Bot

1. On old server, backup database:
   ```bash
   docker cp outline-telegram-bot:/app/data/vpn_bot.db ./vpn_bot.db
   ```

2. Transfer to new server:
   ```bash
   scp vpn_bot.db user@new-server:/tmp/
   ```

3. On new server, deploy bot following steps above

4. Restore database:
   ```bash
   # After starting the container once to create the volume
   docker cp /tmp/vpn_bot.db outline-telegram-bot:/app/data/vpn_bot.db
   docker-compose restart telegram-bot
   ```

## Troubleshooting

### Common Issues

1. **Bot not responding:**
   - Check container logs: `docker-compose logs telegram-bot`
   - Verify environment variables in `.env` file
   - Ensure bot token is correct

2. **Cannot create keys:**
   - Verify Outline API URL and certificate SHA256
   - Check Outline server logs: `sudo journalctl -u outline_proxy_server`
   - Ensure firewall allows required ports

3. **Database issues:**
   - Check disk space: `df -h`
   - Verify database file permissions
   - Look for "database is locked" errors in logs

### Useful Commands

```bash
# View bot logs
docker-compose logs -f telegram-bot

# Execute commands in container
docker-compose exec telegram-bot ls -la /app/data

# Stop services
docker-compose down

# Update to latest version
docker-compose pull && docker-compose up -d
```

## Scaling Considerations

### Multiple Outline Servers

The Telegram bot can manage multiple Outline servers by:
1. Deploying additional Outline servers on different machines
2. Using the bot to create keys on specific servers
3. Managing user access across all servers

### High Availability

For production environments:
1. Use a load balancer for Outline servers
2. Deploy bot with replication
3. Use external database instead of SQLite
4. Implement monitoring and alerting

## Security Best Practices

1. **Regular Updates:**
   - Keep Ubuntu updated with security patches
   - Update Docker images regularly
   - Monitor for Outline server updates

2. **Access Control:**
   - Limit SSH access to specific IPs
   - Use strong passwords for all services
   - Regularly rotate bot tokens if needed

3. **Network Security:**
   - Use firewalls to restrict access
   - Monitor for unusual traffic patterns
   - Implement intrusion detection systems

This deployment guide provides a robust foundation for running your Outline VPN service with Telegram bot management. Regular maintenance and monitoring will ensure optimal performance and security.
