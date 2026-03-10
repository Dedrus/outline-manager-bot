# Outline Server Installation Guide

This guide provides detailed instructions for installing and configuring Outline Server on Ubuntu 24.04.

## Prerequisites

- Fresh Ubuntu 24.04 server with root or sudo access
- Public IP address
- Firewall configured to allow necessary ports

## Installation Methods

### Method 1: Official Installation Script (Recommended)

The official Outline Server installation script is the easiest and most reliable method.

#### Run the Installation Script

```bash
# Download and run the official installation script
wget -qO- https://raw.githubusercontent.com/Jigsaw-Code/outline-server/master/src/server_manager/install_scripts/install_server.sh | bash
```

#### What the Script Does

1. Installs Docker if not already present
2. Pulls and runs the Outline Server container
3. Configures the server with a self-signed certificate
4. Sets up firewall rules
5. Outputs the configuration details needed for management

#### Post-Installation Output

After successful installation, you'll see output similar to:

```json
{
  "apiUrl": "https://YOUR_SERVER_IP:XXXXX/XXXXXXXXXXXXXXXXXXXXXX/",
  "certSha256": "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
}
```

Save these values as you'll need them to configure your Telegram bot:
- `apiUrl` - This is your OUTLINE_API_URL
- `certSha256` - This is your OUTLINE_CERT_SHA256

### Method 2: Manual Docker Installation

If you prefer manual control over the installation:

```bash
# Install Docker
sudo apt update
sudo apt install -y docker.io

# Create persistent storage for Outline Server
sudo mkdir -p /opt/outline

# Run Outline Server container
sudo docker run -d \
  --name outline-server \
  --restart=always \
  --publish 53022:53022/tcp \
  --publish 59123-59222:59123-59222/tcp \
  --volume /opt/outline:/opt/outline \
  quay.io/outline/shadowbox:stable
```

To get the configuration details after manual installation:

```bash
sudo docker exec outline-server sbconfig
```

## Firewall Configuration

Make sure your firewall allows the necessary ports:

```bash
# If using UFW
sudo ufw allow 53022/tcp
sudo ufw allow 59123:59222/tcp

# Or with iptables
sudo iptables -A INPUT -p tcp --dport 53022 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 59123:59222 -j ACCEPT
```

## Testing the Installation

1. **Verify the server is running:**
   ```bash
   curl -k https://YOUR_SERVER_IP:PORT/XXXXXXXXXXXXXXXXXXXXXX/server
   ```

2. **Test with Outline Manager:**
   - Open Outline Manager application
   - Add your server using the apiUrl and certSha256
   - Create a test key and connect from your device

## Security Considerations

1. **SSH Security:**
   - Change the default SSH port from 22 to a custom port
   - Disable password authentication, use SSH keys only
   - Limit SSH access to specific IP addresses if possible

2. **Server Updates:**
   - Regularly update Ubuntu with security patches:
     ```bash
     sudo apt update && sudo apt upgrade -y
     ```

3. **Backup Configuration:**
   - Regularly backup the `/opt/outline` directory
   - Store backups securely offsite

## Migration to New Server

To migrate your Outline Server to a new VPS:

1. **On the old server, backup data:**
   ```bash
   sudo tar -czf outline-backup.tar.gz /opt/outline
   ```

2. **Transfer to new server:**
   ```bash
   scp outline-backup.tar.gz user@new-server:/tmp/
   ```

3. **On the new server, restore data:**
   ```bash
   sudo mkdir -p /opt/outline
   sudo tar -xzf /tmp/outline-backup.tar.gz -C /
   ```

4. **Install Outline Server using the same method as before**

5. **Update DNS records or provide new server IP to users**

## Troubleshooting

### Common Issues

1. **Port conflicts:**
   - Check if ports are already in use: `sudo netstat -tulpn | grep :53022`
   - Modify port mappings in Docker run command if needed

2. **Firewall blocking connections:**
   - Verify firewall rules: `sudo ufw status`
   - Test connectivity: `telnet YOUR_SERVER_IP 53022`

3. **Certificate issues:**
   - The server uses self-signed certificates
   - Accept the security warning when connecting for the first time

4. **Docker not starting:**
   - Check Docker service: `sudo systemctl status docker`
   - Start if needed: `sudo systemctl start docker`

### Logs and Debugging

View Outline Server logs:

```bash
# If installed via official script
sudo journalctl -u outline_proxy_server

# If installed manually with Docker
sudo docker logs outline-server
```

### Restarting the Server

```bash
# If installed via official script
sudo systemctl restart outline_proxy_server

# If installed manually with Docker
sudo docker restart outline-server
```

## Integration with Telegram Bot

Once your Outline Server is running, you'll need the following information to configure your Telegram bot:

1. **API URL** - From the installation output
2. **Certificate SHA256** - From the installation output
3. **Server IP** - Your server's public IP address

These values will be used as environment variables when deploying your Telegram bot:

```env
OUTLINE_API_URL=https://YOUR_SERVER_IP:XXXXX/XXXXXXXXXXXXXXXXXXXXXX/
OUTLINE_CERT_SHA256=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

## Performance Tuning

For better performance with many users:

1. **Increase file descriptor limits:**
   ```bash
   echo "* soft nofile 65536" | sudo tee -a /etc/security/limits.conf
   echo "* hard nofile 65536" | sudo tee -a /etc/security/limits.conf
   ```

2. **Optimize sysctl settings:**
   ```bash
   echo "net.core.somaxconn = 65535" | sudo tee -a /etc/sysctl.conf
   echo "net.ipv4.ip_local_port_range = 1024 65535" | sudo tee -a /etc/sysctl.conf
   sudo sysctl -p
   ```

Remember to restart the server after making these changes.
