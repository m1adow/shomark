FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_KEYCLOAK_URL
ARG VITE_KEYCLOAK_REALM
ARG VITE_KEYCLOAK_CLIENT_ID
ARG VITE_API_BASE_URL

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM node:22-alpine
RUN npm i -g serve
WORKDIR /app
COPY --from=build /app/dist .
EXPOSE 3000
CMD ["serve", "-s", ".", "-l", "3000"]
