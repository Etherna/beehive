FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "BeehiveManager.sln"
RUN dotnet build "BeehiveManager.sln" -c Release -o /app/build
RUN dotnet test "BeehiveManager.sln" -c Release

FROM build AS publish
RUN dotnet publish "BeehiveManager.sln" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BeehiveManager.dll"]