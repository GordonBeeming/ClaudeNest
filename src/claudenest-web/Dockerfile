# ---------------------------------------------------------
# ClaudeNest Web Frontend — multi-stage Dockerfile
# Build context: repository root
#   docker build -f src/claudenest-web/Dockerfile .
# ---------------------------------------------------------

# === Build stage ===
FROM node:22-alpine AS build

WORKDIR /app

# Auth0 config must be baked in at build time by Vite
ARG VITE_AUTH0_DOMAIN=""
ARG VITE_AUTH0_CLIENT_ID=""
ARG VITE_AUTH0_AUDIENCE=""
ARG VITE_APPINSIGHTS_CONNECTION_STRING=""

# Copy dependency manifests first for layer caching
COPY src/claudenest-web/package.json src/claudenest-web/package-lock.json ./

RUN npm ci

# Copy the rest of the frontend source
COPY src/claudenest-web/ .

# Vite reads env vars during build to inline them into the bundle
ENV VITE_AUTH0_DOMAIN=$VITE_AUTH0_DOMAIN
ENV VITE_AUTH0_CLIENT_ID=$VITE_AUTH0_CLIENT_ID
ENV VITE_AUTH0_AUDIENCE=$VITE_AUTH0_AUDIENCE
ENV VITE_APPINSIGHTS_CONNECTION_STRING=$VITE_APPINSIGHTS_CONNECTION_STRING

RUN npm run build

# === Runtime stage ===
FROM nginx:alpine AS runtime

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

COPY src/claudenest-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
