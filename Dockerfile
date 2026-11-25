# Imagen de producción de la API de Corteza (.NET 10).
#   docker build -t corteza-api .
#   docker run -p 8080:8080 -e ConnectionStrings__Corteza="..." -e Jwt__Key="..." corteza-api

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauración queda en caché mientras no cambien las dependencias.
COPY Directory.Build.props ./
COPY src/Corteza.Domain/Corteza.Domain.csproj src/Corteza.Domain/
COPY src/Corteza.Application/Corteza.Application.csproj src/Corteza.Application/
COPY src/Corteza.Infrastructure/Corteza.Infrastructure.csproj src/Corteza.Infrastructure/
COPY src/Corteza.Api/Corteza.Api.csproj src/Corteza.Api/
RUN dotnet restore src/Corteza.Api/Corteza.Api.csproj

COPY src/ src/
RUN dotnet publish src/Corteza.Api/Corteza.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Https__Redirect=false \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

EXPOSE 8080
# La imagen aspnet ya trae el usuario no-root "app".
USER app
ENTRYPOINT ["dotnet", "Corteza.Api.dll"]
