FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY UrlShortenerAPI/UrlShortenerAPI.csproj UrlShortenerAPI/
RUN dotnet restore UrlShortenerAPI/UrlShortenerAPI.csproj

COPY UrlShortenerAPI/ UrlShortenerAPI/
RUN dotnet publish UrlShortenerAPI/UrlShortenerAPI.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "UrlShortenerAPI.dll"]
