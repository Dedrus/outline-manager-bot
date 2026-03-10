# Docker Instructions for Outline Manager Bot

This document provides detailed instructions on how to build, publish, and deploy the Outline Manager Telegram bot using Docker or Podman.

## Prerequisites

- Docker or Podman installed on your system
- Git (for cloning the repository)
- Access to a Linux server (Ubuntu 24.04 recommended) for deployment

## Building the Docker Image

### Using Docker

```bash
# Clone the repository
git clone https://github.com/Dedrus/outline-manager-bot.git
cd outline-manager-bot

# Build the image
docker build -t outline-manager-bot:latest .

# Tag for specific registry (optional)
docker tag outline-manager-bot:latest your-registry/outline-manager-bot:latest
```

### Using Podman

```bash
# Clone the repository
git clone https://github.com/Dedrus/outline-manager-bot.git
cd outline-manager-bot

# Build the image
podman build -t outline-manager-bot:latest .

# Tag for specific registry (optional)
podman tag outline-manager-bot:latest your-registry/outline-manager-bot:latest
```

## Publishing the Image

### Option 1: Docker Hub

1. Create an account at https://hub.docker.com
2. Login to Docker Hub:
   ```bash
   # With Docker
   docker login
   
   # With Podman
   podman login docker.io
   ```

3. Tag and push the image:
   ```bash
   # With Docker
   docker tag outline-manager-bot:latest your-dockerhub-username/outline-manager-bot:latest
   docker push your-dockerhub-username/outline-manager-bot:latest
   
   # With Podman
   podman tag outline-manager-bot:latest docker.io/your-dockerhub-username/outline-manager-bot:latest
   podman push docker.io/your-dockerhub-username/outline-manager-bot:latest
   ```

### Option 2: GitHub Container Registry (GHCR)

1. Create a personal access token on GitHub with `write:packages` permission
2. Login to GHCR:
   ```bash
   # With Docker
   echo your-github-token | docker login ghcr.io -u your-github-username --password-stdin
   
   # With Podman
   echo your-github-token | podman login ghcr.io -u your-github-username --password-stdin
   ```

3. Tag and push the image:
   ```bash
   # With Docker
   docker tag outline-manager-bot:latest ghcr.io/your-github-username/outline-manager-bot:latest
   docker push ghcr.io/your-github-username/outline-manager-bot:latest
   
   # With Podman
   podman tag outline-manager-bot:latest ghcr.io/your-github-username/outline-manager-bot:latest
   podman push ghcr.io/your-github-username/outline-manager-bot:latest
   ```

### Option 3: Save to File (for manual transfer)

```bash
# With Docker
docker save outline-manager-bot:latest -o outline-manager-bot.tar

# With Podman
podman save outline-manager-bot:latest -o outline-manager-bot.tar

# Transfer to server (using scp as example)
scp outline-manager-bot.tar user@your-server:/path/to/destination/

# Load on server
# With Docker
docker load -i outline-manager-bot.tar

# With Podman
podman load -i outline-manager-bot.tar
```

## Deploying the Bot

### Method 1: Using docker-compose (Recommended)

1. Create a `.env` file with your configuration:
   ```env
   TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here
   TELEGRAM_ADMIN_ID=your_telegram_user_id_here
   OUTLINE_API_URL=https://your-outline-server:port/api-url-here
   OUTLINE_CERT_SHA256=your_outline_server_cert_sha256_here
   DEFAULT_DATA_LIMIT_GB=100
   CHECK_INTERVAL_SECONDS=10
   UPDATE_INTERVAL_DAYS=30
   ```

2. Run the bot:
   ```bash
   # With Docker
   docker-compose up -d
   
   # With Podman (using podman-compose)
   podman-compose up -d
   ```

### Method 2: Direct Container Run

```bash
# With Docker
docker run -d \
  --name outline-telegram-bot \
  --restart unless-stopped \
  -e TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here \
  -e TELEGRAM_ADMIN_ID=your_telegram_user_id_here \
  -e OUTLINE_API_URL=https://your-outline-server:port/api-url-here \
  -e OUTLINE_CERT_SHA256=your_outline_server_cert_sha256_here \
  -v outline-bot-data:/app/data \
  outline-manager-bot:latest

# With Podman
podman run -d \
  --name outline-telegram-bot \
  --restart unless-stopped \
  -e TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here \
  -e TELEGRAM_ADMIN_ID=your_telegram_user_id_here \
  -e OUTLINE_API_URL=https://your-outline-server:port/api-url-here \
  -e OUTLINE_CERT_SHA256=your_outline_server_cert_sha256_here \
  -v outline-bot-data:/app/data \
  outline-manager-bot:latest
```

## Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| TELEGRAM_BOT_TOKEN | Telegram bot token from BotFather | Yes | - |
| TELEGRAM_ADMIN_ID | Telegram user ID of admin | Yes | - |
| OUTLINE_API_URL | Outline server API URL | Yes | - |
| OUTLINE_CERT_SHA256 | Outline server certificate SHA256 | Yes | - |
| DATABASE_CONNECTION_STRING | SQLite database connection string | No | Data Source=/app/data/vpn_bot.db |
| DEFAULT_DATA_LIMIT_GB | Default data limit for new keys | No | 100 |
| CHECK_INTERVAL_SECONDS | Interval to check keys for update | No | 10 |
| UPDATE_INTERVAL_DAYS | Days after which keys need update | No | 30 |

## Managing the Container

### View logs:
```bash
# With Docker
docker logs outline-telegram-bot -f

# With Podman
podman logs outline-telegram-bot -f
```

### Stop the container:
```bash
# With Docker
docker stop outline-telegram-bot

# With Podman
podman stop outline-telegram-bot
```

### Start the container:
```bash
# With Docker
docker start outline-telegram-bot

# With Podman
podman start outline-telegram-bot
```

### Remove the container:
```bash
# With Docker
docker rm outline-telegram-bot

# With Podman
podman rm outline-telegram-bot
```

## Data Persistence

The bot stores its SQLite database in `/app/data` directory inside the container. 
When using docker-compose, this is mounted to a named volume `outline-bot-data`.
This ensures data persistence across container recreations.

To backup the database:
```bash
# With Docker
docker cp outline-telegram-bot:/app/data/vpn_bot.db ./backup-vpn_bot.db

# With Podman
podman cp outline-telegram-bot:/app/data/vpn_bot.db ./backup-vpn_bot.db
