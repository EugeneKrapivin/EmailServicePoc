#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0.100-preview.6-noble AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

LABEL prometheus-job=__meta_docker_container_label_prometheus_email-service

FROM mcr.microsoft.com/dotnet/nightly/runtime:9.0.0-preview.6-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Api/EmailService/EmailService.csproj", "Api/EmailService/"]
RUN dotnet restore "./Api/EmailService/EmailService.csproj"
COPY . .
WORKDIR "/src/Api/EmailService"
RUN dotnet build "./EmailService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EmailService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EmailService.dll"]