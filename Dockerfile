FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY KulturHub.sln ./
COPY KulturHub.Api/KulturHub.Api.csproj KulturHub.Api/
COPY KulturHub.Application/KulturHub.Application.csproj KulturHub.Application/
COPY KulturHub.Domain/KulturHub.Domain.csproj KulturHub.Domain/
COPY KulturHub.Infrastructure/KulturHub.Infrastructure.csproj KulturHub.Infrastructure/
COPY KulturHub.UnitTests/KulturHub.UnitTests.csproj KulturHub.UnitTests/

RUN dotnet restore KulturHub.Api/KulturHub.Api.csproj

COPY KulturHub.Api/ KulturHub.Api/
COPY KulturHub.Application/ KulturHub.Application/
COPY KulturHub.Domain/ KulturHub.Domain/
COPY KulturHub.Infrastructure/ KulturHub.Infrastructure/

RUN dotnet publish KulturHub.Api/KulturHub.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "KulturHub.Api.dll"]
