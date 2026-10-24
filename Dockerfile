
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ShipGuard.Api.csproj", "./"]
RUN dotnet restore "ShipGuard.Api.csproj"

COPY . .
RUN dotnet publish "ShipGuard.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Trivy executable stage
FROM aquasec/trivy:latest AS trivy

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Git is needed if the API clones repositories for scanning.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=trivy /usr/local/bin/trivy /usr/local/bin/trivy

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ShipGuard.Api.dll"]
