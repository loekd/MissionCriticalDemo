FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MissionCriticalDemo.FrontEnd/MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend.csproj", "MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend/"]
COPY ["MissionCriticalDemo.FrontEnd/MissionCriticalDemo.Frontend.Client/MissionCriticalDemo.Frontend.Client.csproj", "MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend.Client/"]
RUN dotnet restore "MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend.csproj"
COPY . .
WORKDIR "/src/MissionCriticalDemo.FrontEnd/MissionCriticalDemo.Frontend"
RUN dotnet build "MissionCriticalDemo.Frontend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MissionCriticalDemo.Frontend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MissionCriticalDemo.Frontend.dll"]
