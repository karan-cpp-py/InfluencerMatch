FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better Docker layer caching.
COPY ["backend/InfluencerMatch.API/InfluencerMatch.API.csproj", "backend/InfluencerMatch.API/"]
COPY ["backend/InfluencerMatch.Application/InfluencerMatch.Application.csproj", "backend/InfluencerMatch.Application/"]
COPY ["backend/InfluencerMatch.Domain/InfluencerMatch.Domain.csproj", "backend/InfluencerMatch.Domain/"]
COPY ["backend/InfluencerMatch.Infrastructure/InfluencerMatch.Infrastructure.csproj", "backend/InfluencerMatch.Infrastructure/"]
RUN dotnet restore "backend/InfluencerMatch.API/InfluencerMatch.API.csproj"

# Copy full source and publish.
COPY . .
RUN dotnet publish "backend/InfluencerMatch.API/InfluencerMatch.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Npgsql may need this native dependency for auth providers in some environments.
RUN apt-get update \
	&& apt-get install -y --no-install-recommends libgssapi-krb5-2 \
	&& rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render injects PORT; Program.cs binds to it.
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "InfluencerMatch.API.dll"]
