#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/nightly/aspnet:9.0-noble AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

LABEL prometheus-job=__meta_docker_container_label_prometheus_email-processos-host

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0.100-rc.2-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Processor/Processor.Host/Processor.Host.csproj"
WORKDIR "/src/Processor/Processor.Host"
RUN dotnet build "./Processor.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Processor.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Processor.Host.dll"]