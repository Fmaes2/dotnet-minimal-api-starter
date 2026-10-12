FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StarterKit.Api/StarterKit.Api.csproj StarterKit.Api/
RUN dotnet restore StarterKit.Api/StarterKit.Api.csproj

COPY StarterKit.Api/ StarterKit.Api/
RUN dotnet publish StarterKit.Api/StarterKit.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir /app/logs && chown $APP_UID /app/logs

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "StarterKit.Api.dll"]