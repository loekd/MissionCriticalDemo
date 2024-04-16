#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM nginx AS base
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MissionCriticalDemo.Shared/MissionCriticalDemo.Shared.csproj", "MissionCriticalDemo.Shared/"]
COPY ["MissionCriticalDemo.FrontEnd/MissionCriticalDemo.FrontEnd.csproj", "MissionCriticalDemo.FrontEnd/"]
RUN dotnet restore "MissionCriticalDemo.FrontEnd/MissionCriticalDemo.FrontEnd.csproj"
COPY . .
WORKDIR "/src/MissionCriticalDemo.FrontEnd"
RUN dotnet build "MissionCriticalDemo.FrontEnd.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MissionCriticalDemo.FrontEnd.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY  ./config/nginx.conf /etc/nginx/nginx.conf