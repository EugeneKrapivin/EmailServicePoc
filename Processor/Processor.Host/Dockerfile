#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0.100-preview.6-noble AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

LABEL prometheus-job=__meta_docker_container_label_prometheus_email-processos-host

FROM mcr.microsoft.com/dotnet/nightly/runtime:9.0.0-preview.6-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Processor/Processor.Host/Processor.Host.csproj", "Processor/Processor.Host/"]
COPY ["EmailServicePoc.ServiceDefaults/EmailServicePoc.ServiceDefaults.csproj", "EmailServicePoc.ServiceDefaults/"]
COPY ["Processor/Processor.Grains.Interfaces/Processor.Grains.Interfaces.csproj", "Processor/Processor.Grains.Interfaces/"]
COPY ["Common/EmailService.Models/EmailService.Models.csproj", "Common/EmailService.Models/"]
COPY ["Processor/Processor.Grains/Processor.Grains.csproj", "Processor/Processor.Grains/"]
RUN dotnet restore "./Processor/Processor.Host/Processor.Host.csproj"
COPY . .
WORKDIR "/src/Processor/Processor.Host"
RUN dotnet build "./Processor.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Processor.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Processor.Host.dll"]