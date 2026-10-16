# InteractiveAI Frontend (ASP.NET Core 8 MVC) - Linux container

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InteractiveAI.Frontend.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish InteractiveAI.Frontend.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    Kestrel__Endpoints__Http__Url=http://0.0.0.0:5001
EXPOSE 5001
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:5001/ -o /dev/null || exit 1
ENTRYPOINT ["dotnet", "InteractiveAI.Frontend.dll"]
