# Исправленная конфигурация для Outline Telegram Bot

## Проблема
Изначально вы использовали неправильный формат переменных окружения для .NET приложения. Вместо формата `TelegramBot__Token` вы использовали `TELEGRAM_BOT_TOKEN`.

## Решение
В .NET для доступа к вложенным свойствам конфигурации через переменные окружения используется двойное подчеркивание (`__`) в качестве разделителя.

### Правильная структура переменных окружения:
- `TelegramBot__Token` - токен бота
- `TelegramBot__AdminTelegramIds` - ID администраторов
- `TelegramBot__DefaultDataLimitGb` - лимит данных по умолчанию
- `OutlineApi__Url` - URL API Outline сервера
- `OutlineApi__CertSha256` - SHA256 сертификата
- `KeyUpdateService__CheckInterval` - интервал проверки
- `KeyUpdateService__UpdateIntervalDays` - интервал обновления ключей
- `Database__ConnectionString` - строка подключения к БД

## Способы запуска

### 1. Через docker-compose (рекомендуется)
1. Отредактируйте файл `.env`, указав ваши значения
2. Выполните команду:
```bash
docker-compose -f docker-compose.fixed.yml up -d
```

### 2. Через bash скрипт
Используйте файл `start_bot_fixed.sh`, предварительно указав в нем ваши значения:
```bash
chmod +x start_bot_fixed.sh
./start_bot_fixed.sh
```

## Примечания
- Убедитесь, что вы указали правильные значения в переменных окружения
- Путь к базе данных внутри контейнера должен быть `/app/data/vpn_bot.db`
- Для работы бота требуется доступ к Outline серверу по указанному URL
