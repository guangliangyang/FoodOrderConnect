#!/bin/bash
set -euo pipefail

echo "🛑 Stopping BidOne local development services..."

docker-compose -f docker-compose.dev.yml down

echo "✅ All services stopped"