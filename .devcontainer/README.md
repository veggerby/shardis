# Shardis Development Container

This directory contains the Docker Compose-based development environment for Shardis.

## Architecture

The devcontainer setup follows a modular, service-oriented architecture with optional observability components:

### Core Services (Always Running)

- **devcontainer** - Main development container with .NET SDK 8.0 & 9.0
- **postgres** - PostgreSQL 16 for testing and samples
- **pgadmin** - Web-based PostgreSQL administration tool

### Optional Observability Stack

Enable the full observability stack by using Docker Compose profiles:

```bash
# Start with observability tools
docker-compose --profile observability up

# Start everything
docker-compose --profile full up
```

**Observability Services:**

- **prometheus** - Metrics collection and querying (port 9090)
- **grafana** - Metrics visualization with pre-configured datasources (port 3100)
- **otel-collector** - OpenTelemetry Collector for traces/metrics/logs aggregation
- **jaeger** - Distributed tracing UI (port 16686)
- **loki** - Log aggregation and querying (port 3101)

## Quick Start

### Opening in VS Code

1. Install the "Dev Containers" extension
2. Open the workspace folder in VS Code
3. Click "Reopen in Container" when prompted (or use Command Palette: `Dev Containers: Reopen in Container`)

VS Code will automatically:
- Create `docker-compose.override.yml` if it doesn't exist
- Start the core services (devcontainer, postgres, pgadmin)
- Install configured VS Code extensions
- Run post-create scripts

### Manual Docker Compose

```bash
# Core services only
docker-compose up -d

# With observability stack
docker-compose --profile observability up -d

# All services
docker-compose --profile full up -d
```

## Port Mappings

| Port  | Service                | Description                    |
|-------|------------------------|--------------------------------|
| 5432  | PostgreSQL             | Database server                |
| 5050  | pgAdmin                | Database management UI         |
| 9090  | Prometheus             | Metrics query UI               |
| 3100  | Grafana                | Visualization dashboards       |
| 16686 | Jaeger                 | Distributed tracing UI         |
| 3101  | Loki                   | Log query UI                   |
| 4317  | OTel Collector (gRPC)  | OTLP receiver                  |
| 4318  | OTel Collector (HTTP)  | OTLP receiver                  |

## Environment Variables

Configure services using environment variables. Create a `.env` file in the `.devcontainer` directory:

```bash
# PostgreSQL
POSTGRES_DB=shardis
POSTGRES_USER=shardis
POSTGRES_PASSWORD=shardis_dev_password

# pgAdmin
PGADMIN_DEFAULT_EMAIL=admin@shardis.dev
PGADMIN_DEFAULT_PASSWORD=admin

# OpenTelemetry
OBSERVABILITY_ENABLED=true
OTEL_SERVICE_NAME=shardis-dev

# Grafana
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=admin
```

Default values are provided in `docker-compose.yml`.

## Customization

### Local Overrides

Edit `docker-compose.override.yml` (git-ignored) for local customizations:

```yaml
services:
  postgres:
    ports:
      - "15432:5432"  # Use different local port

  devcontainer:
    environment:
      MY_CUSTOM_VAR: value
```

### Observability Configuration

- **Prometheus**: Edit `prometheus.yml` to add scrape targets
- **Grafana datasources**: Edit `grafana-datasources.yml`
- **OTel Collector**: Edit `otel-collector-config.yaml` for pipeline customization
- **Loki**: Edit `loki-config.yaml` for retention and storage settings

## Connecting to Services

### PostgreSQL

From within the devcontainer:

```bash
# Using connection string (preferred)
echo $POSTGRES_CONNECTION

# Direct connection
psql -h postgres -U shardis -d shardis
```

From host machine:

```bash
psql -h localhost -p 5432 -U shardis -d shardis
```

### pgAdmin

Open http://localhost:5050 in your browser.

**First-time setup:**
1. Login with configured credentials (default: `admin@shardis.dev` / `admin`)
2. Add server:
   - Host: `postgres`
   - Port: `5432`
   - Database: `shardis`
   - Username: `shardis`
   - Password: `shardis_dev_password`

### Observability UIs

- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3100 (default: admin/admin)
- **Jaeger**: http://localhost:16686
- **Loki**: Access via Grafana datasource

## OpenTelemetry Integration

The devcontainer is pre-configured with OpenTelemetry environment variables:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318
OTEL_SERVICE_NAME=shardis-dev
OTEL_TRACES_ENABLED=true
OTEL_METRICS_ENABLED=true
OTEL_LOGS_ENABLED=true
```

Samples and applications can use these to automatically export telemetry.

### .NET Integration Example

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "shardis-app"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

## Profiles

| Profile        | Services Included                                        |
|----------------|----------------------------------------------------------|
| (default)      | devcontainer, postgres, pgadmin                          |
| `observability`| + prometheus, grafana, otel-collector, jaeger, loki      |
| `full`         | All services (same as observability for this setup)      |

## Volume Management

### Persistent Volumes

- `postgres-data` - PostgreSQL database files
- `pgadmin-data` - pgAdmin configuration
- `prometheus-data` - Prometheus metrics database
- `grafana-data` - Grafana dashboards and settings
- `loki-data` - Loki log storage

### Reset/Clean Data

```bash
# Stop all services
docker-compose down

# Remove volumes (WARNING: deletes all data)
docker-compose down -v

# Remove specific volume
docker volume rm shardis_postgres-data
```

## Network

All services communicate over the `shardis-dev` bridge network. Service names are DNS-resolvable within the network:

- `devcontainer` - Development container
- `postgres` - Database server
- `pgadmin` - Admin UI
- `prometheus` - Metrics server
- `grafana` - Visualization server
- `otel-collector` - Telemetry collector
- `jaeger` - Tracing backend
- `loki` - Log aggregation

## Troubleshooting

### Container won't start

```bash
# Check logs
docker-compose logs devcontainer

# Rebuild container
docker-compose build --no-cache devcontainer
docker-compose up -d
```

### PostgreSQL connection issues

```bash
# Check if postgres is healthy
docker-compose ps postgres

# View postgres logs
docker-compose logs postgres

# Test connection from devcontainer
docker-compose exec devcontainer psql -h postgres -U shardis -d shardis
```

### Port conflicts

If ports are already in use, either:
1. Stop conflicting services
2. Use `docker-compose.override.yml` to remap ports

### Observability services not accessible

Ensure you started with the correct profile:

```bash
docker-compose --profile observability up -d
```

## Files

| File                              | Purpose                                          |
|-----------------------------------|--------------------------------------------------|
| `devcontainer.json`               | VS Code devcontainer configuration               |
| `docker-compose.yml`              | Main service definitions                         |
| `docker-compose.override.yml`     | Local customizations (git-ignored)               |
| `prometheus.yml`                  | Prometheus scrape configuration                  |
| `grafana-datasources.yml`         | Grafana datasource provisioning                  |
| `otel-collector-config.yaml`      | OpenTelemetry Collector pipelines                |
| `loki-config.yaml`                | Loki storage and retention settings              |
| `scripts/post_create`             | Post-container-creation initialization script    |

## Contributing

When adding new services:

1. Add to `docker-compose.yml` with appropriate profile if optional
2. Update port forwarding in `devcontainer.json`
3. Document in this README
4. Add any configuration files
5. Update `.gitignore` if needed
