FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Playgo.Domain/Playgo.Domain.csproj src/Playgo.Domain/
COPY src/Playgo.Application/Playgo.Application.csproj src/Playgo.Application/
COPY src/Playgo.Infrastructure/Playgo.Infrastructure.csproj src/Playgo.Infrastructure/
COPY src/Playgo.API/Playgo.API.csproj src/Playgo.API/

RUN dotnet restore src/Playgo.API/Playgo.API.csproj

COPY src/ src/

RUN dotnet publish src/Playgo.API/Playgo.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN mkdir -p wwwroot/uploads/images wwwroot/uploads/videos

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Playgo.API.dll"]
