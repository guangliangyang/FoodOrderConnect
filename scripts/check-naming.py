#!/usr/bin/env python3
"""
Naming convention validation script for BidOne project
Validates that resources follow the project naming standards
"""

import re
import sys
import json
from pathlib import Path
import argparse


def validate_terraform_naming(file_path):
    """Validate Terraform resource naming conventions"""
    issues = []
    
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Find resource definitions
    resource_pattern = r'resource\s+"([^"]+)"\s+"([^"]+)"\s*{'
    resources = re.findall(resource_pattern, content, re.MULTILINE)
    
    for resource_type, resource_name in resources:
        # Check resource name follows snake_case
        if not re.match(r'^[a-z0-9_]+$', resource_name):
            issues.append(f"Resource '{resource_name}' should use snake_case naming")
        
        # Check for meaningful names (not just 'main', 'this', etc.)
        if resource_name in ['main', 'this', 'example', 'test']:
            issues.append(f"Resource '{resource_name}' should have a more descriptive name")
    
    # Find locals with resource names
    locals_pattern = r'(\w+_name)\s*=\s*"([^"]+)"'
    locals_matches = re.findall(locals_pattern, content)
    
    for var_name, name_value in locals_matches:
        # Check if it follows BidOne naming convention for actual Azure resources
        if 'name' in var_name and not name_value.startswith('${'):
            # Should follow pattern: project-service-environment-suffix
            if not re.match(r'^[a-zA-Z0-9]+-[a-zA-Z0-9]+-[a-zA-Z0-9]+-[a-zA-Z0-9]+$', name_value):
                # Exception for storage accounts (no hyphens allowed)
                if 'storage' not in var_name and not re.match(r'^[a-z0-9]{3,24}$', name_value):
                    issues.append(f"Resource name '{name_value}' should follow pattern: project-service-environment-suffix")
    
    return issues


def validate_bicep_naming(file_path):
    """Validate Bicep resource naming conventions"""
    issues = []
    
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Find resource definitions in Bicep
    resource_pattern = r'resource\s+(\w+)\s+\'([^\']+)\'\s*='
    resources = re.findall(resource_pattern, content, re.MULTILINE)
    
    for resource_name, resource_type in resources:
        # Check resource name follows camelCase in Bicep
        if not re.match(r'^[a-z][a-zA-Z0-9]*$', resource_name):
            issues.append(f"Bicep resource '{resource_name}' should use camelCase naming")
        
        # Check for meaningful names
        if resource_name in ['main', 'this', 'example', 'test']:
            issues.append(f"Bicep resource '{resource_name}' should have a more descriptive name")
    
    return issues


def main():
    parser = argparse.ArgumentParser(description='Validate resource naming conventions')
    parser.add_argument('files', nargs='*', help='Files to validate')
    args = parser.parse_args()
    
    all_issues = []
    
    # If no files specified, find all relevant files
    if not args.files:
        tf_files = list(Path('infra/terraform').rglob('*.tf'))
        bicep_files = list(Path('infra/bicep').rglob('*.bicep'))
        files_to_check = tf_files + bicep_files
    else:
        files_to_check = [Path(f) for f in args.files]
    
    for file_path in files_to_check:
        if not file_path.exists():
            continue
            
        print(f"Checking naming conventions in: {file_path}")
        
        if file_path.suffix == '.tf':
            issues = validate_terraform_naming(file_path)
        elif file_path.suffix == '.bicep':
            issues = validate_bicep_naming(file_path)
        else:
            continue
        
        if issues:
            print(f"  Issues found in {file_path}:")
            for issue in issues:
                print(f"    - {issue}")
            all_issues.extend(issues)
        else:
            print(f"  ✓ No naming issues found")
    
    if all_issues:
        print(f"\nTotal naming convention issues: {len(all_issues)}")
        print("\nNaming Convention Guidelines:")
        print("- Terraform resources: use snake_case for resource names")
        print("- Bicep resources: use camelCase for resource names")
        print("- Azure resource names: use pattern project-service-environment-suffix")
        print("- Storage accounts: use lowercase alphanumeric only (no hyphens)")
        print("- Use descriptive names, avoid generic names like 'main', 'this'")
        sys.exit(1)
    else:
        print("\n✓ All naming conventions are correct!")
        sys.exit(0)


if __name__ == '__main__':
    main()