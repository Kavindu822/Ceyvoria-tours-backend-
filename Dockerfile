# ==============================================================================
# CEYVORIA TOURS — BACKEND DOCKERFILE (.NET 9 Web API)
# ==============================================================================
# Multi-stage build for production deployment (Render, Railway, Fly.io, AWS, etc.)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and csproj files first for optimal layer caching
COPY ["CeyvoriaTours.sln", "./"]
COPY ["CeyvoriaTours.Domain/CeyvoriaTours.Domain.csproj", "CeyvoriaTours.Domain/"]
COPY ["CeyvoriaTours.Application/CeyvoriaTours.Application.csproj", "CeyvoriaTours.Application/"]
COPY ["CeyvoriaTours.Infrastructure/CeyvoriaTours.Infrastructure.csproj", "CeyvoriaTours.Infrastructure/"]
COPY ["CeyvoriaTours.API/CeyvoriaTours.API.csproj", "CeyvoriaTours.API/"]

RUN dotnet restore "CeyvoriaTours.sln"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/CeyvoriaTours.API"
RUN dotnet publish "CeyvoriaTours.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5050
EXPOSE 5050

ENTRYPOINT ["dotnet", "CeyvoriaTours.API.dll"]
