FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/RecordingApp.sln ./
COPY backend/src/RecordingApp.Api/RecordingApp.Api.csproj src/RecordingApp.Api/
COPY backend/src/RecordingApp.Domain/RecordingApp.Domain.csproj src/RecordingApp.Domain/
COPY backend/src/RecordingApp.Infrastructure/RecordingApp.Infrastructure.csproj src/RecordingApp.Infrastructure/
COPY backend/tests/RecordingApp.Tests/RecordingApp.Tests.csproj tests/RecordingApp.Tests/
RUN dotnet restore src/RecordingApp.Api/RecordingApp.Api.csproj

COPY backend/. .
RUN dotnet publish src/RecordingApp.Api/RecordingApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RecordingApp.Api.dll"]
