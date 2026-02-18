# MasterBlaster

Windows desktop automation engine that combines Remote Desktop Protocol (RDP) control with Claude AI vision to automate legacy Windows applications. It captures screenshots of an RDP session, sends them to Claude for analysis, and executes actions based on what Claude sees on screen.

## How It Works

1. Connect to a Windows machine via RDP
2. Capture a screenshot of the current screen state
3. Send the screenshot to Claude with a natural-language description of what to do
4. Claude analyzes the screen and identifies UI elements
5. Execute the recommended action (click, type, select, etc.)
6. Repeat until the task is complete

## MBL Task Language

Tasks are defined using MBL (MasterBlaster Language), a declarative DSL with `.mbl` files:

```mbl
task "Create Invoice"
  input order_number, invoice_template

  step "Navigate to invoicing"
    expect "main menu or desktop is visible"
    click "Invoicing menu item"
    expect "invoicing screen is loaded"

  step "Generate invoice"
    type order_number into "order reference field"
    select invoice_template in "template dropdown"
    click "Create Invoice button"
    expect "invoice has been generated successfully"
    extract invoice_number from "invoice number field"
    output invoice_number

  on timeout
    screenshot
    abort "Timed out"

  on error
    screenshot
    abort "Unexpected error"
```

**Supported actions:** `expect`, `click`, `double-click`, `right-click`, `type`, `select`, `key`, `extract`, `output`, `screenshot`, `abort`, `if screen shows ... else ... end`

See `tasks/` for example task files and `masterblaster-spec.zip` for the full MBL language reference.

## Prerequisites

- .NET 9 SDK
- Windows (required for RDP and Windows Forms)
- An [Anthropic API key](https://console.anthropic.com/)
- RDP credentials for the target machine

## Setup

```bash
dotnet restore
```

Set the required environment variables:

```bash
set ANTHROPIC_API_KEY=sk-ant-...
set MB_RDP_PASSWORD=your_rdp_password
```

Edit `config.json` to configure the RDP target, Claude model settings, task timeouts, and logging paths.

## Usage

**Run a task:**

```bash
dotnet run -- run create_invoice --order_number Z6446 --invoice_template EUROLAND4
```

**Validate task syntax:**

```bash
dotnet run -- validate create_customer
```

**List available tasks:**

```bash
dotnet run -- list
```

**Start as a TCP service:**

```bash
dotnet run -- service
```

Output is JSON on stdout:

```json
{
  "status": "success",
  "task": "create_invoice",
  "outputs": {
    "invoice_number": "INV-2024-001"
  },
  "duration_ms": 12500,
  "steps_completed": 5,
  "steps_total": 5
}
```

## Project Structure

```
src/
  MasterBlaster/           # Main application
    Mbl/                   # MBL language lexer, parser, and validator
    Claude/                # Claude API client and prompt construction
    Execution/             # Task executor and action handlers
    Rdp/                   # RDP connection and screenshot capture
    Tcp/                   # TCP service for external integration
    Config/                # Configuration loading
    Logging/               # Structured logging and screenshot archival
  MasterBlaster.Tests/     # xUnit tests
tasks/                     # Example .mbl task files
config.json                # Application configuration
```

## Testing

```bash
dotnet test
```

## Tech Stack

- **C# 12 / .NET 9** (Windows Forms)
- **Anthropic.SDK** for Claude API integration
- **System.CommandLine** for CLI
- **xUnit** for testing
