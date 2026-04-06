# VibeMQ Docker

Build and run the broker image from the **repository root**:

```bash
docker build -f docker/Dockerfile -t vibemq .
docker run -p 2925:2925 vibemq
```

With Docker Compose:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Configuration: environment variables (e.g. `VibeMQ__Port`, `VibeMQ__Authorization__SuperuserUsername`, `VibeMQ__Authorization__SuperuserPassword`) or optional config file. See [Docker documentation](../docs/docs/docker.rst) in the docs.
