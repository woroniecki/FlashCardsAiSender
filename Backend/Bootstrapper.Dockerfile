FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Development
WORKDIR /src

# Kopiowanie wszystkich plików projektu
COPY . .

# Przywracanie zależności
RUN pwd
RUN dotnet restore "FlashCardsAiSender/FlashCardsAiSender.csproj"

# Kopiowanie całego kodu źródłowego
COPY . .

# Budowanie projektu
WORKDIR "/src/FlashCardsAiSender"
RUN dotnet build "FlashCardsAiSender.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Etap publikacji
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FlashCardsAiSender.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Finalny obraz
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlashCardsAiSender.dll"]
