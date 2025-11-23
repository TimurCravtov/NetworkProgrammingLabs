FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FollowerApp/FollowerApp.csproj", "FollowerApp/"]
COPY ["CommonLibrary/CommonLibrary.csproj", "CommonLibrary/"]
RUN dotnet restore "FollowerApp/FollowerApp.csproj"
COPY . .
WORKDIR "/src/FollowerApp"
RUN dotnet build "./FollowerApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FollowerApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FollowerApp.dll"]
