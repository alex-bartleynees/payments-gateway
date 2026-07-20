# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props nuget.config ./
COPY src/ ./src/

RUN --mount=type=secret,id=github_packages_token \
  GITHUB_PACKAGES_TOKEN=$(cat /run/secrets/github_packages_token) dotnet restore src/Host/Host.csproj
RUN dotnet publish src/Host/Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Host.dll"]
