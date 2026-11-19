# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS publish
WORKDIR /src
COPY . .
RUN --mount=type=secret,id=trusted_ca \
    if [ -f /run/secrets/trusted_ca ]; then cat /etc/ssl/certs/ca-certificates.crt /run/secrets/trusted_ca > /tmp/build-ca.pem; export SSL_CERT_FILE=/tmp/build-ca.pem; fi; \
    dotnet restore BabyLog/BabyLog.csproj --locked-mode && dotnet publish BabyLog/BabyLog.csproj -c Release --no-restore -o /out /p:UseAppHost=false
# The ownership of this directory is copied into each fresh named volume.
RUN mkdir -p /data/keys && chown -R 1654:1654 /data

FROM scratch AS artifacts
COPY --from=publish /out/ /

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS standard
WORKDIR /app
COPY --from=publish --chown=1654:1654 /data/ /data/
COPY --from=publish /out/ .
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "BabyLog.dll", "--urls", "http://+:8080"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra@sha256:cae5b962bfb786606def03d250c6a645cc98839095c89e806e4bd8978a68115b AS chiseled
WORKDIR /app
COPY --from=publish --chown=1654:1654 /data/ /data/
COPY --from=publish /out/ .
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "BabyLog.dll", "--urls", "http://+:8080"]
