#!/bin/bash

# Переменные (можно заменить или вынести в отдельный config файл)
TOKEN="token"
ADMIN_IDS="[172699423]"
OUTLINE_URL="https://localhost:53369/abcs"
OUTLINE_CERT="sha"

# Остановить и удалить старый контейнер если есть
docker stop outline-telegram-bot 2>/dev/null
docker rm outline-telegram-bot 2>/dev/null

# Запустить новый
docker run -d \
  --name outline-telegram-bot \
  --restart unless-stopped \
  -p 8080:8080 \
  -v ~/telegram-bot-data:/app/data \
  -e TelegramBot__Token="$TOKEN" \
  -e TelegramBot__AdminTelegramIds="$ADMIN_IDS" \
  -e TelegramBot__DefaultDataLimitGb="100" \
  -e OutlineApi__Url="$OUTLINE_URL" \
  -e OutlineApi__CertSha256="$OUTLINE_CERT" \
  -e KeyUpdateService__CheckInterval="00:15:00" \
  -e KeyUpdateService__UpdateIntervalDays="30" \
  -e Database__ConnectionString="Data Source=/app/data/vpn_bot.db" \
  ghcr.io/dedrus/outline-telegram-bot:latest

# Показать логи
docker logs -f outline-telegram-bot
