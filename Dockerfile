# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
# Render Build Context is set to "ProHair.NL", so this copies the contents of ProHair.NL/
COPY . .
RUN dotnet restore ProHair.NL.csproj
RUN dotnet publish ProHair.NL.csproj -c Release -o /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
ENV ASPNETCORE_URLS=http://+:${PORT} \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Europe/Brussels
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProHair.NL.dll"]
