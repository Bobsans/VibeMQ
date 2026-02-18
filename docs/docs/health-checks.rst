=============
Health Checks
=============

This guide describes using health checks for monitoring and orchestration.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

VibeMQ provides HTTP endpoints for server health checks. These are used by orchestrators (Kubernetes, Docker Swarm) to determine service state.

Enabling Health Checks
======================

.. code-block:: csharp

   using VibeMQ.Health;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Configuration Parameters
-----------------------

+------------------------+------------------+----------------------------------+
| Parameter              | Default          | Description                       |
+========================+==================+==================================+
| ``Enabled``            | true             | Enable health check server        |
+------------------------+------------------+----------------------------------+
| ``Port``               | 8081             | HTTP port for health checks       |
+------------------------+------------------+----------------------------------+

Endpoints
=========

GET /health/
============

Server health check.

**Request:**

.. code-block:: bash

   curl http://localhost:8081/health/

**Response (healthy):**

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 15,
     "queue_count": 5,
     "memory_usage_mb": 256
   }

**Status code:** ``200 OK``

**Response (unhealthy):**

.. code-block:: json

   {
     "status": "unhealthy",
     "active_connections": 0,
     "queue_count": 0,
     "memory_usage_mb": 512
   }

**Status code:** ``503 Service Unavailable``

**Health criteria:**

- ``healthy`` — memory usage < 90%
- ``unhealthy`` — memory usage >= 90%

GET /metrics/
=============

Get detailed metrics.

**Request:**

.. code-block:: bash

   curl http://localhost:8081/metrics/

**Response:**

.. code-block:: json

   {
     "total_messages_published": 125000,
     "total_messages_delivered": 124850,
     "total_messages_acknowledged": 124800,
     "total_retries": 150,
     "total_dead_lettered": 50,
     "total_errors": 5,
     "total_connections_accepted": 500,
     "total_connections_rejected": 10,
     "active_connections": 15,
     "active_queues": 5,
     "in_flight_messages": 42,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5,
     "timestamp": "2026-02-18T10:30:00Z"
   }

Using with Orchestrators
==========================

Kubernetes
----------

**Deployment with health checks:**

.. code-block:: yaml

   apiVersion: apps/v1
   kind: Deployment
   metadata:
     name: vibemq
   spec:
     replicas: 3
     selector:
       matchLabels:
         app: vibemq
     template:
       metadata:
         labels:
           app: vibemq
       spec:
         containers:
         - name: vibemq
           image: vibemq-server:latest
           ports:
           - containerPort: 8080
             name: tcp
           - containerPort: 8081
             name: http
           livenessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 10
             periodSeconds: 10
             timeoutSeconds: 5
             failureThreshold: 3
           readinessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 5
             periodSeconds: 5
             timeoutSeconds: 3
             failureThreshold: 3
           resources:
             requests:
               memory: "512Mi"
               cpu: "250m"
             limits:
               memory: "1Gi"
               cpu: "500m"

**Probe descriptions:**

- **livenessProbe** — determines if container should be restarted
- **readinessProbe** — determines if container is ready to accept traffic

**Parameters:**

- ``initialDelaySeconds`` — delay before first check
- ``periodSeconds`` — interval between checks
- ``timeoutSeconds`` — check timeout
- ``failureThreshold`` — number of failed attempts

Docker Compose
--------------

.. code-block:: yaml

   version: '3.8'

   services:
     vibemq:
       image: vibemq-server:latest
       ports:
         - "8080:8080"
         - "8081:8081"
       environment:
         - VIBEMQ__PORT=8080
         - VIBEMQ__AUTHTOKEN=my-secret-token
       healthcheck:
         test: ["CMD", "curl", "-f", "http://localhost:8081/health/"]
         interval: 10s
         timeout: 5s
         retries: 3
         start_period: 10s
       restart: unless-stopped

Azure Container Instances
-------------------------

.. code-block:: yaml

   apiVersion: 2019-12-01
   kind: ContainerGroup
   metadata:
     name: vibemq
   spec:
     containers:
     - name: vibemq
       image: vibemq-server:latest
       ports:
       - port: 8080
         protocol: TCP
       - port: 8081
         protocol: TCP
       livenessProbe:
         httpGet:
           path: /health/
           port: 8081
         initialDelaySeconds: 10
         periodSeconds: 10
       readinessProbe:
         httpGet:
           path: /health/
           port: 8081
         initialDelaySeconds: 5
         periodSeconds: 5

AWS ECS
-------

**Task definition:**

.. code-block:: json

   {
     "family": "vibemq",
     "containerDefinitions": [
       {
         "name": "vibemq",
         "image": "vibemq-server:latest",
         "portMappings": [
           {
             "containerPort": 8080,
             "hostPort": 8080,
             "protocol": "tcp"
           },
           {
             "containerPort": 8081,
             "hostPort": 8081,
             "protocol": "tcp"
           }
         ],
         "healthCheck": {
           "command": [
             "CMD-SHELL",
             "curl -f http://localhost:8081/health/ || exit 1"
           ],
           "interval": 10,
           "timeout": 5,
           "retries": 3,
           "startPeriod": 10
         }
       }
     ]
   }

Monitoring Health Checks
========================

Manual Check
----------------

.. code-block:: bash

   # Health check
   curl http://localhost:8081/health/

   # Detailed metrics
   curl http://localhost:8081/metrics/

   # With formatting
   curl -s http://localhost:8081/metrics/ | jq

PowerShell:

.. code-block:: powershell

   # Health check
   Invoke-RestMethod -Uri http://localhost:8081/health/

   # Check with error handling
   try {
       $response = Invoke-RestMethod -Uri http://localhost:8081/health/
       if ($response.status -eq "healthy") {
           Write-Host "✓ Server healthy" -ForegroundColor Green
       } else {
           Write-Host "✗ Server unhealthy" -ForegroundColor Red
       }
   } catch {
       Write-Host "✗ Connection error: $_" -ForegroundColor Red
   }

Automatic Monitoring
-------------------------

**Bash script:**

.. code-block:: bash

   #!/bin/bash

   HEALTH_URL="http://localhost:8081/health/"
   METRICS_URL="http://localhost:8081/metrics/"

   # Health check
   response=$(curl -s -w "\n%{http_code}" $HEALTH_URL)
   body=$(echo "$response" | head -n -1)
   status_code=$(echo "$response" | tail -n 1)

   if [ "$status_code" -eq 200 ]; then
       echo "✓ VibeMQ healthy (HTTP $status_code)"
       echo "$body" | jq .
   elif [ "$status_code" -eq 503 ]; then
       echo "✗ VibeMQ unhealthy (HTTP $status_code)"
       echo "$body" | jq .
       exit 1
   else
       echo "✗ Connection error (HTTP $status_code)"
       exit 1
   fi

   # Metrics check
   metrics=$(curl -s $METRICS_URL)
   echo "Metrics:"
   echo "$metrics" | jq .

**Python script:**

.. code-block:: python

   import requests
   import sys
   import time

   HEALTH_URL = "http://localhost:8081/health/"
   METRICS_URL = "http://localhost:8081/metrics/"

   def check_health():
       try:
           response = requests.get(HEALTH_URL, timeout=5)
           
           if response.status_code == 200:
               data = response.json()
               print(f"✓ VibeMQ healthy")
               print(f"  Status: {data['status']}")
               print(f"  Connections: {data['active_connections']}")
               print(f"  Queues: {data['queue_count']}")
               print(f"  Memory: {data['memory_usage_mb']} MB")
               return True
           elif response.status_code == 503:
               print(f"✗ VibeMQ unhealthy")
               return False
       except requests.exceptions.RequestException as e:
           print(f"✗ Connection error: {e}")
           return False
       
       return False

   def get_metrics():
       try:
           response = requests.get(METRICS_URL, timeout=5)
           if response.status_code == 200:
               data = response.json()
               print("\nMetrics:")
               print(f"  Published: {data['total_messages_published']}")
               print(f"  Delivered: {data['total_messages_delivered']}")
               print(f"  Acknowledged: {data['total_messages_acknowledged']}")
               print(f"  Errors: {data['total_errors']}")
               print(f"  Latency: {data['average_delivery_latency_ms']:.2f} ms")
       except Exception as e:
           print(f"  Error getting metrics: {e}")

   if __name__ == "__main__":
       if check_health():
           get_metrics()
           sys.exit(0)
       else:
           sys.exit(1)

Integration with Monitoring Systems
==================================

Prometheus
----------

**prometheus.yml:**

.. code-block:: yaml

   scrape_configs:
     - job_name: 'vibemq'
       static_configs:
         - targets: ['vibemq:8081']
       metrics_path: '/metrics/'
       scrape_interval: 15s
       scrape_timeout: 10s

Grafana
-------

Import dashboard for visualizing VibeMQ metrics.

**Main panels:**

- Health status (health check)
- Active connections
- Queue count
- Memory usage
- Message throughput
- Delivery latency

Datadog
-------

**Agent configuration:**

.. code-block:: yaml

   instances:
     - vibemq_url: http://vibemq:8081/metrics/
       tags:
         - "service:vibemq"
         - "env:production"

New Relic
---------

Use Prometheus endpoint for integration:

.. code-block:: yaml

   integrations:
     - name: prometheus
       metric_types:
         - vibemq_messages_published_total
         - vibemq_messages_delivered_total
         - vibemq_active_connections
       urls:
         - http://vibemq:8081/metrics/

Troubleshooting
==================

Health check not responding
------------------------

**Problem:** ``curl: (7) Failed to connect to localhost port 8081``

**Causes:**

- Health check disabled
- Wrong port
- Firewall blocking

**Solution:**

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
       options.Port = 8081;  // Check port
   })

Returns 503
----------------

**Problem:** Health check returns ``503 Service Unavailable``

**Cause:** Critical memory usage (>90%)

**Solution:**

1. Increase memory limit
2. Reduce queue sizes
3. Optimize memory usage

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.MaxQueueSize = 5000;  // Reduce size
   })

Health check timeout
--------------------

**Problem:** ``curl: (28) Operation timed out``

**Causes:**

- Server overloaded
- Wrong timeout

**Solution:** Increase timeout in orchestrator configuration:

.. code-block:: yaml

   livenessProbe:
     timeoutSeconds: 10  # Increase timeout

Next Steps
==========

- :doc:`monitoring` — monitoring and metrics
- :doc:`troubleshooting` — troubleshooting
- :doc:`configuration` — configuration
