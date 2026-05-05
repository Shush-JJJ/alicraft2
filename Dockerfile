# syntax=docker/dockerfile:1
# Multi-stage build for the Alicraft2 ASP.NET Core 10 app.

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore NuGet packages first (cached layer when only source changes).
COPY ["Alicraft2.csproj", "./"]
RUN dotnet restore "Alicraft2.csproj"

# Copy the rest of the source and publish a Release build.
COPY . .
RUN dotnet publish "Alicraft2.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Npgsql probes for Kerberos/GSSAPI libraries during connection negotiation
# even when not using integrated auth. The base aspnet image doesn't ship them,
# so installing libgssapi-krb5-2 prevents "libgssapi_krb5.so.2: cannot open
# shared object file" crashes when connecting to Postgres (e.g. Neon).
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render (and most container hosts) inject a $PORT env var. We expand it at runtime via sh,
# falling back to 8080 when running the image locally.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Alicraft2.dll"]
