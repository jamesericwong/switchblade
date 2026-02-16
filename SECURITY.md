# Security Policy

# Security Policy

## Supported Versions

Currently internal development is focused on the latest release.

| Version | Supported          |
| ------- | ------------------ |
| 1.9.x   | :white_check_mark: |
| < 1.9   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability within SwitchBlade, please do not report it through public issues. Instead, please use GitHub's **Private Vulnerability Reporting** feature:

1.  Navigate to the repository on GitHub.
2.  Click on the **Security** tab.
3.  Click on **Advisories** in the left sidebar.
4.  Click the **Report a vulnerability** button to open a private report.

We will review your report and coordinate a disclosure timeline as soon as possible.

## Security Practices

- **Zero Secret Exposure**: Never commit API keys, tokens, or passwords to the repository.
- **Dependency Updates**: We strive to keep dependencies up to date to minimize known vulnerabilities.
- **Out-of-Process UIA**: UI Automation scans are performed in a separate process (`SwitchBlade.UiaWorker.exe`) to minimize the impact of any potential memory or security issues in the UIA framework.
- **Input Sanitization**: All user-provided patterns (Regex/Fuzzy) are sanitized and validated to prevent ReDoS (Regular Expression Denial of Service).
