# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files
COPY ["src/ExternalOrderApi/ExternalOrderApi.csproj", "src/ExternalOrderApi/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]

# Restore dependencies
RUN dotnet restore "src/ExternalOrderApi/ExternalOrderApi.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/src/ExternalOrderApi"
RUN dotnet build "ExternalOrderApi.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "ExternalOrderApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app

# Create a non-root user
RUN addgroup -g 1001 -S appuser && adduser -S appuser -G appuser -u 1001

# Copy the published application
COPY --from=publish /app/publish .

# Change ownership of the app directory to the non-root user
RUN chown -R appuser:appuser /app

# Switch to the non-root user
USER appuser

# Configure the app to bind to port 80
ENV ASPNETCORE_URLS=http://+:80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

# Set the entry point
ENTRYPOINT ["dotnet", "BidOne.ExternalOrderApi.dll"]