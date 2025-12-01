FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MahjongStats.csproj", "."]
RUN dotnet restore "MahjongStats.csproj"
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data

COPY --from=build /app/publish .
EXPOSE 80

# Set default environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Entrypoint with environment variable support
ENTRYPOINT ["dotnet", "MahjongStats.dll"]
