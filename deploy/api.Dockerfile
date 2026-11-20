FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/TheSplashLab.Poc.sln ./
COPY backend/src/TheSplashLab.Core/TheSplashLab.Core.csproj src/TheSplashLab.Core/
COPY backend/src/TheSplashLab.Infrastructure/TheSplashLab.Infrastructure.csproj src/TheSplashLab.Infrastructure/
COPY backend/src/TheSplashLab.Api/TheSplashLab.Api.csproj src/TheSplashLab.Api/
RUN dotnet restore src/TheSplashLab.Api/TheSplashLab.Api.csproj
COPY backend/src/ ./src/
RUN dotnet publish src/TheSplashLab.Api/TheSplashLab.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Path=/data/splashlab-poc.db
RUN mkdir -p /data
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TheSplashLab.Api.dll"]
