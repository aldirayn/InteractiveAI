# InteractiveAI Backend (ASP.NET Core 8) - Linux container
# Model AI TIDAK dimasukkan ke image: di-mount dari server lewat docker-compose (model\ & Models\).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InteractiveAI.Backend.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish InteractiveAI.Backend.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# libgomp1: dibutuhkan runtime native ONNX/Whisper; curl: healthcheck
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgomp1 curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:5000/health || exit 1
ENTRYPOINT ["dotnet", "InteractiveAI.Backend.dll"]
