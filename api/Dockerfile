FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/ShoMark.Domain/ShoMark.Domain.csproj", "ShoMark.Domain/"]
COPY ["src/ShoMark.Application/ShoMark.Application.csproj", "ShoMark.Application/"]
COPY ["src/ShoMark.Infrastructure/ShoMark.Infrastructure.csproj", "ShoMark.Infrastructure/"]
COPY ["src/ShoMark.Api/ShoMark.Api.csproj", "ShoMark.Api/"]
RUN dotnet restore "ShoMark.Api/ShoMark.Api.csproj"

COPY src/ .
RUN dotnet publish "ShoMark.Api/ShoMark.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShoMark.Api.dll"]
