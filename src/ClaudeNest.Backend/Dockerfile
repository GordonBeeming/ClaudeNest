# ---------------------------------------------------------
# ClaudeNest Backend — multi-stage Dockerfile
# Build context: repository root
#   docker build -f src/ClaudeNest.Backend/Dockerfile .
# ---------------------------------------------------------

# === Build stage ===
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Aspire workload is needed for the ServiceDefaults project
RUN dotnet workload install aspire

WORKDIR /src

# Copy only .csproj files first to maximise restore layer caching
COPY src/ClaudeNest.Backend/ClaudeNest.Backend.csproj src/ClaudeNest.Backend/
COPY src/ClaudeNest.Shared/ClaudeNest.Shared.csproj src/ClaudeNest.Shared/
COPY src/ClaudeNest.ServiceDefaults/ClaudeNest.ServiceDefaults.csproj src/ClaudeNest.ServiceDefaults/

RUN dotnet restore src/ClaudeNest.Backend/ClaudeNest.Backend.csproj

# Copy full source (filtered by .dockerignore)
COPY src/ src/

RUN dotnet publish src/ClaudeNest.Backend/ClaudeNest.Backend.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# === Runtime stage ===
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "ClaudeNest.Backend.dll"]
