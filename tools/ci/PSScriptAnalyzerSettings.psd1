# SPDX-License-Identifier: MIT
# PSScriptAnalyzer Settings for CI Pipeline
# Tailored for IndustrieLite CI scripts

@{
    Severity = @('Error', 'Warning')

    # Exclude rules that don't fit CI script patterns
    ExcludeRules = @(
        'PSAvoidUsingWriteHost',  # CI scripts need Write-Host for output
        'PSUseShouldProcessForStateChangingFunctions'  # Not needed for CI utilities
    )

    # Include common best practices
    IncludeRules = @(
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingPositionalParameters',
        'PSUseApprovedVerbs',
        'PSUseSingularNouns'
    )
}
