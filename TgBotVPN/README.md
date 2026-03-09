# Telegram Outline VPN Bot

Бот для управления ключами Outline Server через Telegram с функциями:
- Белый список пользователей (whitelist)
- Создание и управление VPN ключами
- Автоматическое обновление лимитов трафика (100 ГБ)
- Фоновый сервис обновления ключей каждые 10 секунд
- SQLite база данных с Entity Framework Core
- Логирование через Serilog
- Типизированная конфигурация через Options pattern

## Требования

- .NET 10.0 SDK или выше
- SQLite (встроена в EF Core)

## Установка и настройка

### 1. Открытие проекта

Откройте файл `TgBotVPN.sln` в Visual Studio (2022+) или VS Code

### 2. Конфигурация

Отредактируйте файл `appsettings.json` в корне проекта:

```json
{
  "TelegramBot": {
    "Token": "YOUR_BOT_TOKEN_FROM_BOTFATHER",
    "AdminTelegramId": YOUR_ADMIN_TELEGRAM_ID
  },
  "Database": {
    "ConnectionString": "Data Source=vpnbot.db",
    "DefaultDataLimitGb": 100
  },
  "OutlineApi": {
    "Url": "https://your-outline-server-api-url",
    "ApiKey": "YOUR_OUTLINE_API_KEY"
  },
  "KeyUpdateService": {
    "CheckIntervalSeconds": 10,
    "UpdateIntervalDays": 30
  }
}
```

**Параметры:**
- `TelegramBot.Token` - токен бота от BotFather
- `TelegramBot.AdminTelegramId` - ID администратора (только этот пользователь может использовать админ-команды)
- `Database.ConnectionString` - путь к SQLite базе данных (например: `Data Source=vpn_bot.db`)
- `Database.DefaultDataLimitGb` - лимит трафика по умолчанию при создании ключа (по умолчанию 100 ГБ)
- `OutlineApi.Url` - URL API вашего Outline сервера (например: `https://193.0.178.250:6532`)
- `OutlineApi.ApiKey` - API ключ Outline сервера
- `KeyUpdateService.CheckIntervalSeconds` - интервал проверки ключей в секундах (по умолчанию 10)
- `KeyUpdateService.UpdateIntervalDays` - количество дней, после которых ключ требует обновления (по умолчанию 30)

### 3. Запуск бота

**Из Visual Studio:**
- Нажмите F5 или кнопку "Run"

**Из терминала:**
```bash
cd C:\dev\work\TgBotVPN
dotnet run
```

**Из скомпилированного exe:**
```bash
C:\dev\work\TgBotVPN\bin\Debug\net10.0\TgBotVPN.exe
```

> **Важно:** Убедитесь, что файл `appsettings.json` находится в той же директории, что и исполняемый файл

## Команды бота

### Для всех пользователей:
- `/start` - регистрация в боте
- `/create_key` - создать новый VPN ключ (требует одобрения)
- `/my_key` - получить текущий ключ
- `/help` - справка по командам

### Админ-команды (только для админа):
- `/admin_add_user <telegram_id>` - добавить пользователя в whitelist
- `/admin_remove_user <telegram_id>` - удалить пользователя из whitelist
- `/admin_set_limit <telegram_id> <limit_gb>` - установить лимит трафика пользователю
- `/admin_pending_users` - список пользователей, ожидающих одобрения
- `/admin_all_keys` - список всех созданных ключей

## Архитектура

### Классы конфигурации (Configuration/):
- **TelegramBotSettings** - настройки Telegram бота
- **OutlineApiSettings** - настройки Outline API
- **DatabaseSettings** - настройки базы данных
- **KeyUpdateServiceSettings** - настройки сервиса обновления ключей

### Модели (Models/):
- **TelegramUser** - информация о пользователе Telegram
- **OutlineKey** - информация о VPN ключе (отношение 1-1 с пользователем)

### Данные (Data/):
- **AppDbContext** - Entity Framework DbContext для SQLite

### Сервисы (Services/):
- **DatabaseService** - работа с БД (CRUD операции)
- **OutlineApiService** - интеграция с API Outline сервера
- **TelegramBotService** - обработка сообщений от Telegram
- **KeyUpdateService** - фоновый сервис для обновления лимитов ключей каждые 10 сек

## Особенности

1. **Каждый пользователь может иметь только один ключ**
   - При попытке создания второго ключа бот выведет ошибку

2. **Автоматическое обновление лимитов трафика**
   - KeyUpdateService запускается при старте приложения
   - Каждые 10 секунд проверяет базу данных
   - Ищет ключи, не обновленные более 30 дней
   - Отправляет запрос на Outline API для обновления лимита
   - После успешного обновления обновляет время `LastUpdated` в БД

3. **Белый список (Whitelist)**
   - По умолчанию все новые пользователи не одобрены (IsWhitelisted = false)
   - Только администратор может добавлять пользователей в whitelist
   - Неодобренные пользователи не могут создавать ключи

4. **Типизированная конфигурация**
   - Используется Options pattern для типобезопасного доступа к конфигурации
   - Все настройки внедряются через Dependency Injection
   - Строгая типизация вместо hardcoded строк

5. **Логирование**
   - Все операции логируются в консоль через Serilog
   - Формат логов: `[Дата Время] [Уровень] Сообщение`
   - Логируются все важные события: создание ключей, обновления, ошибки

## Структура БД

### Таблица TelegramUsers
- `TelegramId` (Primary Key) - ID пользователя в Telegram
- `Username` - имя пользователя
- `IsWhitelisted` - одобрен ли пользователь
- `IsAdmin` - является ли администратором
- `CreatedAt` - дата создания

### Таблица OutlineKeys
- `Id` (Primary Key) - идентификатор ключа
- `TelegramId` (Foreign Key) - ID пользователя
- `KeyName` - имя ключа
- `AccessUrl` - URL доступа к ключу
- `DataLimitGb` - лимит трафика в ГБ
- `LastUpdated` - дата последнего обновления
- `CreatedAt` - дата создания

## Troubleshooting

### Ошибка "Unable to resolve service for type 'Microsoft.Extensions.Configuration.IConfiguration'"
**Решение:** Убедитесь, что в Program.cs используется Options pattern для конфигурации, а не прямой доступ к IConfiguration.

### Бот не запускается
- Проверьте, что файл `appsettings.json` находится в директории с исполняемым файлом
- Убедитесь, что все конфигурационные параметры заполнены в `appsettings.json`
- Убедитесь, что токен бота корректный и актуальный
- Проверьте интернет-соединение

### Ошибка "Database is locked"
- Убедитесь, что БД не используется другим процессом
- Попробуйте удалить файл `vpn_bot.db` и перезапустить (база создастся заново)

### Ошибка при создании ключа на Outline API
- Убедитесь, что пользователь добавлен в whitelist (/admin_add_user)
- Проверьте доступность API Outline сервера (правильный URL и ApiKey)
- Убедитесь, что сертификат сервера доверен (или отключите проверку SSL если нужно)
- Посмотрите логи в консоли для деталей ошибки

### Ключи не обновляются автоматически
- Убедитесь, что `KeyUpdateService` работает (в логах должно быть сообщение "KeyUpdateService started")
- Проверьте значения `CheckIntervalSeconds` и `UpdateIntervalDays` в `appsettings.json`
- Убедитесь, что у ключей прошло более 30 дней (или измененное значение) с момента последнего обновления
- Проверьте доступность Outline API сервера

## Разработка

### Структура проекта
- **Configuration/** - классы типизированной конфигурации
- **Models/** - модели данных (TelegramUser, OutlineKey)
- **Data/** - слой доступа к данным (AppDbContext)
- **Services/** - основные сервисы приложения
- **bin/Debug/net10.0/** - скомпилированные файлы и база данных

### Как добавить новую команду бота
1. Добавьте обработчик в `TelegramBotService.cs` (например, `HandleMyCommandAsync`)
2. Добавьте проверку команды в `HandleUpdateAsync`
3. Вызовите обработчик в соответствующей ветке if/else

### Миграции БД
Если нужно изменить схему БД:
```bash
dotnet ef migrations add DescriptionOfChange
dotnet ef database update
```

## Лицензия

MIT
