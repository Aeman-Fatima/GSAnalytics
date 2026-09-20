# Multi-stage build: compile with the full SDK, run on the much smaller ASP.NET runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GSAnalytics.slnx .
COPY src/GSAnalytics.Domain/GSAnalytics.Domain.csproj src/GSAnalytics.Domain/
COPY src/GSAnalytics.Application/GSAnalytics.Application.csproj src/GSAnalytics.Application/
COPY src/GSAnalytics.Infrastructure/GSAnalytics.Infrastructure.csproj src/GSAnalytics.Infrastructure/
COPY src/GSAnalytics.Api/GSAnalytics.Api.csproj src/GSAnalytics.Api/
COPY tests/GSAnalytics.Tests/GSAnalytics.Tests.csproj tests/GSAnalytics.Tests/
RUN dotnet restore src/GSAnalytics.Api/GSAnalytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/GSAnalytics.Api/GSAnalytics.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GSAnalytics.Api.dll"]
