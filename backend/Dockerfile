# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer caching: dependency downloads only rerun when csproj changes)
COPY Clinic.Domain/Clinic.Domain.csproj Clinic.Domain/
COPY Clinic.Application/Clinic.Application.csproj Clinic.Application/
COPY Clinic.Infrastructure/Clinic.Infrastructure.csproj Clinic.Infrastructure/
COPY Clinic.Api/Clinic.Api.csproj Clinic.Api/
RUN dotnet restore Clinic.Api/Clinic.Api.csproj

COPY . .
RUN dotnet publish Clinic.Api/Clinic.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render (and most PaaS) inject PORT; default to 8080 for local docker runs
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Clinic.Api.dll
