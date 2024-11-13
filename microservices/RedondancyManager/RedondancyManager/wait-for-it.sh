#!/bin/bash
host="$1"
shift
port="$1"
shift
timeout="$1"
shift

# Essayer jusqu'à ce que le port soit accessible
for i in $(seq 1 $timeout); do
    nc -z "$host" "$port" && echo "$host:$port is available" && exec "$@"
    echo "Waiting for $host:$port..."
    sleep 1
done

echo "Timeout waiting for $host:$port"
exit 1
