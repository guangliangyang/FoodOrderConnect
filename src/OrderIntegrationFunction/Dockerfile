# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY src/OrderIntegrationFunction/OrderIntegrationFunction.csproj src/OrderIntegrationFunction/
COPY src/Shared/Shared.csproj src/Shared/

# Restore dependencies
RUN dotnet restore "src/OrderIntegrationFunction/OrderIntegrationFunction.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR /src/src/OrderIntegrationFunction
RUN dotnet build "OrderIntegrationFunction.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "OrderIntegrationFunction.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-appservice AS final
WORKDIR /home/site/wwwroot

# Copy the published application
COPY --from=publish /app/publish .

# Set environment variables
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

EXPOSE 80