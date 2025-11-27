FROM mcr.microsoft.com/dotnet/runtime-deps:9.0.9-bookworm-slim-amd64

# create work dir and set permissions as WORKDIR sets permissions as root
RUN mkdir /app && chown -R app:app /app
WORKDIR /app

LABEL maintainer="Digitaal Vlaanderen <digitaal.vlaanderen@vlaanderen.be>"
LABEL registry="extract-bundler"

COPY / /app
WORKDIR /app

RUN apt-get update && \
    apt-get upgrade -y ca-certificates && \
    apt-get install curl jq openssl ca-certificates -y && \
    c_rehash /etc/ssl/certs && \
    update-ca-certificates --fresh --verbose && \
    apt-get install -y --no-install-recommends gdal-bin && \
    chmod +x ./init.sh

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/app/datadog

ENV GDAL_DATA=/usr/share/gdal

# Run the createLogPath script on Linux to ensure the automatic instrumentation logs are generated without permission issues
RUN /app/datadog/createLogPath.sh

# switch to created user
USER app

ENTRYPOINT ["./init.sh"]
