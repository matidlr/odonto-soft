# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos solo los .csproj primero para aprovechar el cache de layers de Docker
COPY ["src/Odonto.Api/Odonto.Api.csproj", "src/Odonto.Api/"]
COPY ["src/Odonto.Application/Odonto.Application.csproj", "src/Odonto.Application/"]
COPY ["src/Odonto.Domain/Odonto.Domain.csproj", "src/Odonto.Domain/"]
COPY ["src/Odonto.Infrastructure/Odonto.Infrastructure.csproj", "src/Odonto.Infrastructure/"]
RUN dotnet restore "src/Odonto.Api/Odonto.Api.csproj"

COPY . .
WORKDIR /src/src/Odonto.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Odonto.Api.dll"]
