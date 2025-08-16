#!/bin/bash
set -euo pipefail

echo "🔨 Building all BidOne projects..."

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Build all projects
echo "🔧 Building projects..."
dotnet build --configuration Debug --no-restore

# Run tests
echo "🧪 Running tests..."
dotnet test --configuration Debug --no-build --verbosity normal

echo "✅ Build completed successfully!"