# ClusterFuzzLite's build image for the .NET fuzz targets (tests/Fuzz): its C++ builder, plus .NET,
# SharpFuzz's instrumentation tool and libfuzzer-dotnet (libFuzzer's driver for .NET programs).
# The image is pinned by digest; Dependabot moves the digest when v1 changes.
FROM gcr.io/oss-fuzz-base/base-builder:v1@sha256:1f9cf74bc3d7029b528494542f9d9851b256dceed38d3f420989434dc5ae0760
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates libicu-dev zip && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet \
 && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
 && dotnet tool install --tool-path /opt/sharpfuzz SharpFuzz.CommandLine --version 2.3.0
ENV DOTNET_ROOT=/opt/dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RUN git clone --depth 1 https://github.com/Metalnem/libfuzzer-dotnet "$SRC/libfuzzer-dotnet"
COPY . $SRC/valheim-world-editor
WORKDIR $SRC/valheim-world-editor
COPY .clusterfuzzlite/build.sh $SRC/
