# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS source
WORKDIR /src
COPY global.json nuget.config ./
COPY BabyLog/Domain/Activity.cs BabyLog/Domain/
COPY Distributed/ Distributed/
RUN mkdir -p /data/keys && chown -R 1654:1654 /data

FROM source AS activities-build
RUN --mount=type=secret,id=trusted_ca \
    if [ -f /run/secrets/trusted_ca ]; then cat /etc/ssl/certs/ca-certificates.crt /run/secrets/trusted_ca > /tmp/build-ca.pem; export SSL_CERT_FILE=/tmp/build-ca.pem; fi; \
    dotnet restore Distributed/Activities.Api/Activities.Api.csproj --locked-mode && \
    dotnet publish Distributed/Activities.Api/Activities.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM source AS summaries-build
RUN --mount=type=secret,id=trusted_ca \
    if [ -f /run/secrets/trusted_ca ]; then cat /etc/ssl/certs/ca-certificates.crt /run/secrets/trusted_ca > /tmp/build-ca.pem; export SSL_CERT_FILE=/tmp/build-ca.pem; fi; \
    dotnet restore Distributed/Summaries.Api/Summaries.Api.csproj --locked-mode && \
    dotnet publish Distributed/Summaries.Api/Summaries.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra@sha256:cae5b962bfb786606def03d250c6a645cc98839095c89e806e4bd8978a68115b AS runtime
WORKDIR /app
COPY --from=source --chown=1654:1654 /data/ /data/
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0
USER 1654:1654
EXPOSE 8080

FROM runtime AS activities
COPY --from=activities-build /out/ ./
ENTRYPOINT ["dotnet", "BabyLog.Distributed.Activities.Api.dll"]

FROM runtime AS summaries
COPY --from=summaries-build /out/ ./
ENTRYPOINT ["dotnet", "BabyLog.Distributed.Summaries.Api.dll"]
