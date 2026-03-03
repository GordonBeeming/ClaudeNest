# ---------------------------------------------------------
# ClaudeNest Backend — multi-stage Dockerfile
# Build context: repository root
#   docker build -f src/ClaudeNest.Backend/Dockerfile .
# ---------------------------------------------------------

# === Build stage ===
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /build

# Copy only .csproj files first to maximise restore layer caching
COPY src/ClaudeNest.Backend/ClaudeNest.Backend.csproj ClaudeNest.Backend/
COPY src/ClaudeNest.Shared/ClaudeNest.Shared.csproj ClaudeNest.Shared/
COPY src/ClaudeNest.ServiceDefaults/ClaudeNest.ServiceDefaults.csproj ClaudeNest.ServiceDefaults/

RUN dotnet restore ClaudeNest.Backend/ClaudeNest.Backend.csproj

# Copy only the .NET source directories the backend needs
COPY src/ClaudeNest.Backend/ ClaudeNest.Backend/
COPY src/ClaudeNest.Shared/ ClaudeNest.Shared/
COPY src/ClaudeNest.ServiceDefaults/ ClaudeNest.ServiceDefaults/

RUN dotnet publish ClaudeNest.Backend/ClaudeNest.Backend.csproj \
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
