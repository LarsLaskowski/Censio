FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24-amd64@sha256:19a9b6214e3e78ffabc3469de57aeccb4d388bacb14de1e98533e7f94c34f4b3 AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/Censio.Server/Censio.Server.csproj src/Censio.Server/
COPY src/Censio.Contracts/Censio.Contracts.csproj src/Censio.Contracts/
COPY src/Censio.Server.Data/Censio.Server.Data.csproj src/Censio.Server.Data/
RUN dotnet restore src/Censio.Server/Censio.Server.csproj

COPY .editorconfig ./
COPY src/ src/

ARG VERSION=0.0.0
SHELL ["/bin/ash", "-o", "pipefail", "-c"]
RUN case "$VERSION" in \
        ''|*[!0-9A-Za-z.+-]*) echo "VERSION is not a semantic version: $VERSION" >&2; exit 1;; \
    esac \
    && if ! printf '%s\n' "$VERSION" | grep -Eqx \
        '(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?'; \
    then echo "VERSION is not a semantic version: $VERSION" >&2; exit 1; fi
RUN dotnet publish src/Censio.Server/Censio.Server.csproj -c Release --no-restore \
    -o /app/publish "-p:Version=${VERSION}"
RUN ls /app/publish/wwwroot/_framework/blazor.web*.js > /dev/null 2>&1 \
    || { echo "publish output lacks wwwroot/_framework/blazor.web*.js (see docs/decisions/0007)" >&2; exit 1; }

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24-amd64@sha256:ca5fcc45a7632e09463199812ed63c240148ddd2a2bffacfd5e20252ad0c8f90
LABEL org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24-amd64" \
      org.opencontainers.image.base.digest="sha256:fbad312ed75e0f10c4fcba4d48e0c0f133de07219bd0f78054e4a102501c0650" \
      org.opencontainers.image.description="Censio server: live state and history of Anno 117 statistics" \
      org.opencontainers.image.licenses="MIT"
ENV ASPNETCORE_HTTP_PORTS=8080 \
    CENSIO__DATADIRECTORY=/config
WORKDIR /app
COPY --from=build /app/publish .
RUN install -d -o 1654 -g 1654 -m 0755 /config
EXPOSE 8080
USER 1654:1654
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=3 CMD wget -q -T 2 -O /dev/null "http://127.0.0.1:${ASPNETCORE_HTTP_PORTS%%;*}/health" || exit 1
ENTRYPOINT ["dotnet", "Censio.Server.dll"]
