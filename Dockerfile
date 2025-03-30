# Build-Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopieren Sie zuerst nur die Projektdateien für die Wiederherstellung
COPY *.sln .
COPY *.csproj ./

# Restore Dependencies
RUN dotnet restore

# Dann kopieren Sie den restlichen Code
COPY . .

# Debug: Überprüfen, welche Dateien vorhanden sind
RUN ls -la

# Build und Veröffentlichen mit detaillierter Ausgabe
RUN dotnet build --configuration Release
RUN dotnet publish --configuration Release --no-build -o /app

# Runtime-Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80
ENTRYPOINT ["dotnet", "Projektmanagement.dll"]
