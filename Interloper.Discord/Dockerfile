# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Interloper.sln .
COPY Interloper.Core/Interloper.Core.csproj Interloper.Core/
COPY Interloper.Data/Interloper.Data.csproj Interloper.Data/
COPY Interloper.Discord/Interloper.Discord.csproj Interloper.Discord/

RUN dotnet restore Interloper.sln

# Copy all source code and publish
COPY . .
RUN dotnet publish Interloper.Discord/Interloper.Discord.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 botuser && chown -R botuser:botuser /app
USER botuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Interloper.Discord.dll"]
