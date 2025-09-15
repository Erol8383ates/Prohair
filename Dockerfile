# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
# copy the whole repo as context (Render: set Build Context to ".")
COPY . .
RUN dotnet restore ProHair.NL/ProHair.NL.csproj
RUN dotnet publish ProHair.NL/ProHair.NL.csproj -c Release -o /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
# Bind to Render's PORT and enable full globalization + timezone
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
