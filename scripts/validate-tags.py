#!/usr/bin/env python3
"""
Tag validation script for BidOne project
Ensures all resources have required tags defined
"""

import re
import sys
import json
from pathlib import Path
import argparse


# Required tags for BidOne project
REQUIRED_TAGS = [
    'Environment',
    'Project', 
    'ManagedBy'
]

# Project-specific tag values
EXPECTED_TAG_VALUES = {
    'Project': 'BidOne-Integration-Demo',
    'ManagedBy': ['Terraform', 'Bicep', 'ARM']
}


def validate_terraform_tags(file_path):
    """Validate Terraform resource tags"""
    issues = []
    
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Find resource definitions
    resource_pattern = r'resource\s+"([^"]+)"\s+"([^"]+)"\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}'
    resources = re.findall(resource_pattern, content, re.DOTALL)
    
    for resource_type, resource_name, resource_body in resources:
        # Skip resources that typically don't need tags
        skip_resources = [
            'azurerm_resource_group',  # Often tagged differently
            'random_',  # Random resources don't need tags
            'data.',    # Data sources don't have tags
        ]
        
        if any(skip in resource_type for skip in skip_resources):
            continue
        
        # Check if tags block exists
        if 'tags' not in resource_body and '${' not in resource_body:
            issues.append(f"Resource '{resource_type}.{resource_name}' is missing tags block")
            continue
        
        # Extract tags block
        tags_pattern = r'tags\s*=\s*(\{[^}]*\}|\$\{[^}]*\})'
        tags_match = re.search(tags_pattern, resource_body)
        
        if not tags_match:
            # Check if tags are set via variable reference
            if '${' in resource_body or 'var.' in resource_body or 'local.' in resource_body:
                continue  # Tags might be set via variables
            issues.append(f"Resource '{resource_type}.{resource_name}' is missing tags definition")
            continue
        
        tags_block = tags_match.group(1)
        
        # Check for required tags (basic check - doesn't handle complex interpolations)
        if not tags_block.startswith('${'):  # Only check literal tag blocks
            for required_tag in REQUIRED_TAGS:
                if f'"{required_tag}"' not in tags_block and f'{required_tag}' not in tags_block:
                    issues.append(f"Resource '{resource_type}.{resource_name}' is missing required tag: {required_tag}")
        
        # Validate specific tag values
        if 'Project' in tags_block:
            if 'BidOne-Integration-Demo' not in tags_block:
                issues.append(f"Resource '{resource_type}.{resource_name}' has incorrect Project tag value")
    
    return issues


def validate_bicep_tags(file_path):
    """Validate Bicep resource tags"""
    issues = []
    
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Find resource definitions in Bicep
    # This is a simplified pattern - Bicep syntax can be more complex
    resource_pattern = r'resource\s+(\w+)\s+\'([^\']+)\'\s*=\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}'
    resources = re.findall(resource_pattern, content, re.DOTALL)
    
    for resource_name, resource_type, resource_body in resources:
        # Skip resources that typically don't need tags
        if 'Microsoft.Resources/resourceGroups' in resource_type:
            continue
        
        # Check if tags property exists
        if 'tags:' not in resource_body and 'tags =' not in resource_body:
            issues.append(f"Bicep resource '{resource_name}' ({resource_type}) is missing tags property")
            continue
        
        # Extract tags section (simplified)
        tags_pattern = r'tags:\s*\{([^}]*)\}'
        tags_match = re.search(tags_pattern, resource_body)
        
        if not tags_match:
            # Check if tags are set via parameter or variable
            if 'param' in resource_body or 'var(' in resource_body:
                continue  # Tags might be set via parameters
            continue
        
        tags_content = tags_match.group(1)
        
        # Check for required tags
        for required_tag in REQUIRED_TAGS:
            if required_tag not in tags_content:
                issues.append(f"Bicep resource '{resource_name}' is missing required tag: {required_tag}")
    
    return issues


def validate_tag_variables(file_path):
    """Validate that tag variables have required tags defined"""
    issues = []
    
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Check for tag variable definitions
    if file_path.name == 'variables.tf':
        # Look for tags variable definition
        tags_var_pattern = r'variable\s+"tags"\s*\{([^}]*)\}'
        tags_var_match = re.search(tags_var_pattern, content, re.DOTALL)
        
        if tags_var_match:
            tags_var_content = tags_var_match.group(1)
            
            # Check if default includes required tags
            default_pattern = r'default\s*=\s*\{([^}]*)\}'
            default_match = re.search(default_pattern, tags_var_content)
            
            if default_match:
                default_content = default_match.group(1)
                for required_tag in REQUIRED_TAGS:
                    if required_tag not in default_content:
                        issues.append(f"Variable 'tags' default is missing required tag: {required_tag}")
    
    return issues


def main():
    parser = argparse.ArgumentParser(description='Validate resource tags')
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
            
        print(f"Checking tags in: {file_path}")
        
        issues = []
        
        if file_path.suffix == '.tf':
            issues.extend(validate_terraform_tags(file_path))
            issues.extend(validate_tag_variables(file_path))
        elif file_path.suffix == '.bicep':
            issues.extend(validate_bicep_tags(file_path))
        else:
            continue
        
        if issues:
            print(f"  Issues found in {file_path}:")
            for issue in issues:
                print(f"    - {issue}")
            all_issues.extend(issues)
        else:
            print(f"  ✓ No tag issues found")
    
    if all_issues:
        print(f"\nTotal tag validation issues: {len(all_issues)}")
        print(f"\nRequired tags for BidOne project: {', '.join(REQUIRED_TAGS)}")
        print("\nTag Guidelines:")
        print("- All Azure resources must have the required tags")
        print("- Project tag must be 'BidOne-Integration-Demo'")
        print("- ManagedBy tag should indicate the IaC tool used")
        print("- Environment tag should be 'dev', 'staging', or 'prod'")
        sys.exit(1)
    else:
        print("\n✓ All tag validations passed!")
        sys.exit(0)


if __name__ == '__main__':
    main()