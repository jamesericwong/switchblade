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

## Plugin Trust Model

SwitchBlade discovers plugins by file-system convention and executes them as ordinary .NET assemblies — trust plugin code accordingly:

- **What gets loaded**: any `.dll` under the install directory's `Plugins\` folder (subfolders included) whose name starts with `SwitchBlade.Plugins.` (case-insensitive, e.g. `SwitchBlade.Plugins.Chrome.dll`). The host (`Core/PluginLoader`) and the UIA worker share one discovery implementation (`SwitchBlade.Contracts.PluginDiscovery`, since v1.9.x batch-2 fixes), so both sides can never diverge on which assemblies count as plugins; per-assembly and per-provider load failures are isolated, so one bad plugin cannot block or crash the rest.
- **No signature or allowlist check**: this is inherent to any plugin model — anyone who can write into `Plugins\` can execute arbitrary code with your user privileges on next launch. The trust boundary is therefore file-system access to the install directory: standard MSI installs land in admin-only Program Files, which is the primary control; if you copy SwitchBlade somewhere world-writable, restrict that folder's ACLs accordingly.
- **Containment**: UIA plugins run out-of-process in `SwitchBlade.UiaWorker.exe` (memory/COM fault isolation — see "Out-of-Process UIA" below), all scans are timeout-bounded, and provider activation of a dead/stale window is guarded so plugin misbehavior degrades gracefully instead of crashing the host.

## Security Practices

- **Zero Secret Exposure**: Never commit API keys, tokens, or passwords to the repository.
- **Dependency Updates**: We strive to keep dependencies up to date to minimize known vulnerabilities.
- **Out-of-Process UIA**: UI Automation scans are performed in a separate process (`SwitchBlade.UiaWorker.exe`) to minimize the impact of any potential memory or security issues in the UIA framework.
- **Input Sanitization**: All user-provided patterns (Regex/Fuzzy) are sanitized and validated to prevent ReDoS (Regular Expression Denial of Service).
