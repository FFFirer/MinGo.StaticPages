# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet publish src/MinGo.StaticPages.Web/MinGo.StaticPages.Web.csproj \
      -c Release -o /app/publish \
      -r linux-x64 --self-contained false \
      -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false \
    && rm -rf /app/publish/BlazorDebugProxy \
    && find /app/publish/wwwroot/lib/bootstrap/dist -type f \
         ! -name 'bootstrap.min.css' ! -name 'bootstrap.min.css.gz' ! -name 'bootstrap.min.css.br' \
         ! -name 'bootstrap.bundle.min.js' ! -name 'bootstrap.bundle.min.js.gz' ! -name 'bootstrap.bundle.min.js.br' \
         -delete \
    && mkdir -p /stage/data && chown 1654:1654 /stage/data

# Ubuntu Chiseled: no shell/package manager; -extra keeps ICU + tzdata. Runs as uid 1654 (app) by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build --chown=1654:1654 /app/publish ./
COPY --from=build /stage/data /data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MinGo.StaticPages.Web.dll"]
