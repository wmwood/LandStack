# SDK/runtime patch tags are updated together with global.json after servicing validation.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build-env
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080

# Docker copies this ownership into a new empty named volume. Existing volumes are retained.
RUN mkdir -p /app/App_Data && chown "$APP_UID:$APP_UID" /app/App_Data
COPY --from=build-env /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LandStack.Api.dll"]
