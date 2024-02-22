#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MissionCriticalDemo.DispatchApi/MissionCriticalDemo.MissionCriticalDemo.DispatchApi.csproj", "MissionCriticalDemo.DispatchApi/"]
COPY ["Shared/MissionCriticalDemo.Shared.csproj", "MissionCriticalDemo.Shared/"]
COPY ["Client/MissionCriticalDemo.FrontEnd.csproj", "MissionCriticalDemo.FrontEnd/"]
RUN dotnet restore "MissionCriticalDemo.DispatchApi/MissionCriticalDemo.DispatchApi.csproj"
COPY . .
WORKDIR "/src/Server"
RUN dotnet build "MissionCriticalDemo.DispatchApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MissionCriticalDemo.DispatchApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MissionCriticalDemo.DispatchApi.dll"]