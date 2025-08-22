#!/bin/bash

# Bicep validation script for pre-commit hook

set -e

echo "Validating Bicep templates..."

# Check if bicep CLI is available
if ! command -v az &> /dev/null || ! az bicep version &> /dev/null; then
    echo "Error: Bicep CLI is not installed. Run 'az bicep install'"
    exit 1
fi

# Find all Bicep files
bicep_files=($(find infra/bicep -name "*.bicep" -type f))

if [[ ${#bicep_files[@]} -eq 0 ]]; then
    echo "No Bicep files found to validate"
    exit 0
fi

# Validate each Bicep file
for file in "${bicep_files[@]}"; do
    echo "Validating: $file"
    
    # Compile Bicep to test syntax
    if ! az bicep build --file "$file" --stdout > /dev/null 2>&1; then
        echo "Error: Bicep compilation failed for $file"
        az bicep build --file "$file"
        exit 1
    fi
done

echo "All Bicep templates validated successfully"