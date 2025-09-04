FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 1633

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Beehive.sln"
RUN dotnet build "Beehive.sln" -c Release -o /app/build
RUN dotnet test "Beehive.sln" -c Release

FROM build AS publish
RUN dotnet publish "Beehive.sln" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Beehive.dll"]