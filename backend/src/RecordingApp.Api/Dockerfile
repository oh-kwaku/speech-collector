FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RecordingApp.sln ./
COPY src/RecordingApp.Api/RecordingApp.Api.csproj src/RecordingApp.Api/
COPY src/RecordingApp.Domain/RecordingApp.Domain.csproj src/RecordingApp.Domain/
COPY src/RecordingApp.Infrastructure/RecordingApp.Infrastructure.csproj src/RecordingApp.Infrastructure/
COPY tests/RecordingApp.Tests/RecordingApp.Tests.csproj tests/RecordingApp.Tests/
RUN dotnet restore src/RecordingApp.Api/RecordingApp.Api.csproj

COPY . .
RUN dotnet publish src/RecordingApp.Api/RecordingApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RecordingApp.Api.dll"]
