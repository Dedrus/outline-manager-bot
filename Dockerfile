# Multi-stage Dockerfile for Telegram Outline VPN Bot
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY TgBotVPN/TgBotVPN.csproj ./TgBotVPN/
RUN dotnet restore TgBotVPN/TgBotVPN.csproj

# Copy all source code
COPY TgBotVPN/. ./TgBotVPN/

# Build the application
WORKDIR /src/TgBotVPN
RUN dotnet publish -c Release -o /app/publish

# Final stage - runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Create directory for database
RUN mkdir -p /app/data

# Copy published application
COPY --from=build /app/publish .

# Run the application
ENTRYPOINT ["dotnet", "TgBotVPN.dll"]
