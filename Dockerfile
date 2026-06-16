FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y libssl3 ca-certificates libicu72 && rm -rf /var/lib/apt/lists/*
COPY forge-linux-x64 /usr/local/bin/forge
RUN chmod +x /usr/local/bin/forge
ENTRYPOINT ["forge"]
