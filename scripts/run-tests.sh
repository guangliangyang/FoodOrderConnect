#!/bin/bash

# Test execution script for BidOne project
# Runs all tests with coverage reporting and result analysis

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
TEST_RESULTS_DIR="TestResults"
COVERAGE_THRESHOLD=80
VERBOSE=false
GENERATE_REPORT=true
RUN_INTEGRATION_TESTS=true

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -v, --verbose               Enable verbose output"
    echo "  -t, --threshold PERCENT     Coverage threshold (default: 80)"
    echo "  --no-report                 Skip generating HTML coverage report"
    echo "  --unit-only                 Run only unit tests (skip integration tests)"
    echo "  --integration-only          Run only integration tests"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                          Run all tests with default settings"
    echo "  $0 -v -t 85                Run tests with 85% coverage threshold"
    echo "  $0 --unit-only              Run only unit tests"
}

# Function to clean previous test results
clean_test_results() {
    print_info "Cleaning previous test results..."
    if [ -d "$TEST_RESULTS_DIR" ]; then
        rm -rf "$TEST_RESULTS_DIR"
    fi
    mkdir -p "$TEST_RESULTS_DIR"
}

# Function to run unit tests
run_unit_tests() {
    print_info "Running unit tests..."
    
    local test_projects=(
        "tests/ExternalOrderApi.Tests/ExternalOrderApi.Tests.csproj"
        "tests/InternalSystemApi.Tests/InternalSystemApi.Tests.csproj"
        "tests/OrderIntegrationFunction.Tests/OrderIntegrationFunction.Tests.csproj"
        "tests/Shared.Tests/Shared.Tests.csproj"
    )
    
    for project in "${test_projects[@]}"; do
        if [ -f "$project" ]; then
            print_info "Running tests for $project"
            
            local project_name=$(basename "$project" .csproj)
            local results_file="$TEST_RESULTS_DIR/${project_name}_results.trx"
            local coverage_file="$TEST_RESULTS_DIR/${project_name}_coverage.xml"
            
            local dotnet_args=(
                "test" "$project"
                "--logger" "trx;LogFileName=$results_file"
                "--collect:XPlat Code Coverage"
                "--results-directory" "$TEST_RESULTS_DIR"
                "--configuration" "Release"
            )
            
            if [ "$VERBOSE" = true ]; then
                dotnet_args+=("--verbosity" "normal")
            else
                dotnet_args+=("--verbosity" "minimal")
            fi
            
            if ! dotnet "${dotnet_args[@]}"; then
                print_error "Unit tests failed for $project"
                return 1
            fi
            
            print_success "Unit tests passed for $project"
        else
            print_warning "Test project not found: $project"
        fi
    done
    
    print_success "All unit tests completed"
}

# Function to run integration tests
run_integration_tests() {
    print_info "Running integration tests..."
    
    # Set up test environment
    export ASPNETCORE_ENVIRONMENT=Testing
    export ConnectionStrings__DefaultConnection="Data Source=:memory:"
    
    local integration_test_args=(
        "test"
        "--filter" "Category=Integration"
        "--logger" "trx;LogFileName=$TEST_RESULTS_DIR/integration_results.trx"
        "--collect:XPlat Code Coverage"
        "--results-directory" "$TEST_RESULTS_DIR"
        "--configuration" "Release"
    )
    
    if [ "$VERBOSE" = true ]; then
        integration_test_args+=("--verbosity" "normal")
    else
        integration_test_args+=("--verbosity" "minimal")
    fi
    
    if ! dotnet "${integration_test_args[@]}"; then
        print_error "Integration tests failed"
        return 1
    fi
    
    print_success "Integration tests completed"
}

# Function to generate coverage reports
generate_coverage_report() {
    print_info "Generating coverage report..."
    
    # Find coverage files
    local coverage_files=()
    while IFS= read -r -d '' file; do
        coverage_files+=("$file")
    done < <(find "$TEST_RESULTS_DIR" -name "coverage.cobertura.xml" -print0)
    
    if [ ${#coverage_files[@]} -eq 0 ]; then
        print_warning "No coverage files found"
        return 0
    fi
    
    # Check if reportgenerator tool is installed
    if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
        print_info "Installing ReportGenerator tool..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    # Generate HTML report
    local report_args=(
        "-reports:${coverage_files[*]}"
        "-targetdir:$TEST_RESULTS_DIR/coverage-report"
        "-reporttypes:Html;Cobertura;JsonSummary"
        "-sourcedirs:src"
        "-historydir:$TEST_RESULTS_DIR/coverage-history"
    )
    
    if ! reportgenerator "${report_args[@]}"; then
        print_error "Failed to generate coverage report"
        return 1
    fi
    
    print_success "Coverage report generated: $TEST_RESULTS_DIR/coverage-report/index.html"
}

# Function to analyze coverage results
analyze_coverage() {
    print_info "Analyzing test coverage..."
    
    local summary_file="$TEST_RESULTS_DIR/coverage-report/Summary.json"
    if [ ! -f "$summary_file" ]; then
        print_warning "Coverage summary file not found"
        return 0
    fi
    
    # Extract coverage percentage using jq if available
    if command -v jq &> /dev/null; then
        local line_coverage=$(jq -r '.summary.linecoverage' "$summary_file")
        local branch_coverage=$(jq -r '.summary.branchcoverage' "$summary_file")
        
        print_info "Line Coverage: ${line_coverage}%"
        print_info "Branch Coverage: ${branch_coverage}%"
        
        # Check if coverage meets threshold
        local line_coverage_int=${line_coverage%.*}
        if [ "$line_coverage_int" -lt "$COVERAGE_THRESHOLD" ]; then
            print_error "Line coverage ($line_coverage%) is below threshold ($COVERAGE_THRESHOLD%)"
            return 1
        else
            print_success "Coverage threshold met: $line_coverage% >= $COVERAGE_THRESHOLD%"
        fi
    else
        print_warning "jq not available, skipping coverage analysis"
    fi
}

# Function to generate test summary
generate_test_summary() {
    print_info "Generating test summary..."
    
    local summary_file="$TEST_RESULTS_DIR/test-summary.txt"
    
    {
        echo "BidOne Project Test Summary"
        echo "==========================="
        echo "Date: $(date)"
        echo "Coverage Threshold: $COVERAGE_THRESHOLD%"
        echo ""
        
        # Count test result files
        local trx_files=()
        while IFS= read -r -d '' file; do
            trx_files+=("$file")
        done < <(find "$TEST_RESULTS_DIR" -name "*.trx" -print0)
        
        echo "Test Result Files: ${#trx_files[@]}"
        
        for file in "${trx_files[@]}"; do
            echo "  - $(basename "$file")"
        done
        
        echo ""
        echo "Coverage Report: $TEST_RESULTS_DIR/coverage-report/index.html"
        echo "Test Results Directory: $TEST_RESULTS_DIR"
        
    } > "$summary_file"
    
    print_success "Test summary saved: $summary_file"
    cat "$summary_file"
}

# Function to open coverage report
open_coverage_report() {
    local report_file="$TEST_RESULTS_DIR/coverage-report/index.html"
    if [ -f "$report_file" ]; then
        print_info "Coverage report available at: $report_file"
        
        # Try to open in browser (works on macOS and some Linux distributions)
        if command -v open &> /dev/null; then
            open "$report_file" 2>/dev/null || true
        elif command -v xdg-open &> /dev/null; then
            xdg-open "$report_file" 2>/dev/null || true
        fi
    fi
}

# Main function
main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -v|--verbose)
                VERBOSE=true
                shift
                ;;
            -t|--threshold)
                COVERAGE_THRESHOLD="$2"
                shift 2
                ;;
            --no-report)
                GENERATE_REPORT=false
                shift
                ;;
            --unit-only)
                RUN_INTEGRATION_TESTS=false
                shift
                ;;
            --integration-only)
                RUN_UNIT_TESTS=false
                RUN_INTEGRATION_TESTS=true
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            *)
                print_error "Unknown parameter: $1"
                show_usage
                exit 1
                ;;
        esac
    done

    print_info "Starting test execution for BidOne project"
    print_info "Coverage threshold: $COVERAGE_THRESHOLD%"
    print_info "Generate report: $GENERATE_REPORT"
    print_info "Run integration tests: $RUN_INTEGRATION_TESTS"
    echo ""

    # Clean previous results
    clean_test_results

    # Run tests
    if [ "$RUN_INTEGRATION_TESTS" != "false" ] || [ "${RUN_UNIT_TESTS:-true}" = "true" ]; then
        run_unit_tests || exit 1
    fi
    
    if [ "$RUN_INTEGRATION_TESTS" = "true" ]; then
        run_integration_tests || exit 1
    fi

    # Generate coverage report
    if [ "$GENERATE_REPORT" = "true" ]; then
        generate_coverage_report || exit 1
        analyze_coverage || exit 1
    fi

    # Generate summary
    generate_test_summary

    # Open report if in interactive mode
    if [ -t 0 ] && [ "$GENERATE_REPORT" = "true" ]; then
        open_coverage_report
    fi

    print_success "All tests completed successfully!"
}

# Run main function
main "$@"
