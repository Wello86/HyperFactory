# Multi-stage build
# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BrownEvents.sln ./
COPY BrownEvents.Api/BrownEvents.Api.csproj BrownEvents.Api/
COPY BrownEvents.Tests/BrownEvents.Tests.csproj BrownEvents.Tests/
RUN dotnet restore BrownEvents.sln

COPY . .
RUN dotnet publish BrownEvents.Api/BrownEvents.Api.csproj -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80
ENTRYPOINT ["dotnet", "BrownEvents.Api.dll"]
