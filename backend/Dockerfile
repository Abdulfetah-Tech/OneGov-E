# Build context: repository root.   docker build -f backend/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/Directory.Build.props backend/
COPY backend/src/OneGov.Domain/OneGov.Domain.csproj backend/src/OneGov.Domain/
COPY backend/src/OneGov.Application/OneGov.Application.csproj backend/src/OneGov.Application/
COPY backend/src/OneGov.Infrastructure/OneGov.Infrastructure.csproj backend/src/OneGov.Infrastructure/
COPY backend/src/OneGov.Api/OneGov.Api.csproj backend/src/OneGov.Api/
RUN dotnet restore backend/src/OneGov.Api/OneGov.Api.csproj

COPY backend/src backend/src
RUN dotnet publish backend/src/OneGov.Api/OneGov.Api.csproj \
    -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OneGov.Api.dll"]
