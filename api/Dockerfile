FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG PROJECT_PATH=src/gateway/ShoMark.Gateway/ShoMark.Gateway.csproj
COPY . .
RUN dotnet restore "$PROJECT_PATH"
RUN dotnet publish "$PROJECT_PATH" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
ARG PROJECT_DLL=ShoMark.Gateway.dll
ENV PROJECT_DLL=$PROJECT_DLL
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet $PROJECT_DLL"]
