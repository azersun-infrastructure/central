FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OpsCentral.csproj .
RUN dotnet restore OpsCentral.csproj

COPY . .
RUN dotnet publish OpsCentral.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OpsCentral.dll"]
