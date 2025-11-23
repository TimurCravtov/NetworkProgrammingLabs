FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LeaderApp/LeaderApp.csproj", "LeaderApp/"]
COPY ["CommonLibrary/CommonLibrary.csproj", "CommonLibrary/"]
RUN dotnet restore "LeaderApp/LeaderApp.csproj"
COPY . .
WORKDIR "/src/LeaderApp"
RUN dotnet build "./LeaderApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LeaderApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LeaderApp.dll"]
