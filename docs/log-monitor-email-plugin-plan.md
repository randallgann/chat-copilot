# Log Monitoring and Email Notification Plugin Implementation Plan

## Overview

This document outlines the plan to implement a plugin for Chat Copilot that will monitor logs for specific patterns and send email notifications when certain conditions are met. The plugin will be integrated with the existing Semantic Kernel framework and will be accessible as a tool for the LLM to use during conversations.

## Key Components

1. **LogMonitorPlugin**: A plugin class that will monitor log files for specific patterns
2. **EmailNotifierPlugin**: A plugin class that will send email notifications
3. **Configuration Options**: Settings for the plugins in appsettings.json
4. **Plugin Registration**: Code to register the new plugins with the Semantic Kernel

## Implementation Steps

### Step 1: Create Plugin Configuration Options

1. Create a new class to store configuration options for log monitoring and email notification in `/webapi/Options/`:

```csharp
// LogMonitorOptions.cs
namespace CopilotChat.WebApi.Options;

/// <summary>
/// Configuration options for log monitoring.
/// </summary>
public class LogMonitorOptions
{
    /// <summary>
    /// Path to the log files to monitor.
    /// </summary>
    public string LogDirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// File pattern for log files (e.g., "*.log").
    /// </summary>
    public string LogFilePattern { get; set; } = "*.log";

    /// <summary>
    /// How often to check for new logs (in seconds).
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of log entries to process per scan.
    /// </summary>
    public int MaxLogEntriesToProcess { get; set; } = 1000;

    /// <summary>
    /// Patterns to look for in logs (regex patterns).
    /// </summary>
    public List<AlertPattern> AlertPatterns { get; set; } = new List<AlertPattern>();
}

/// <summary>
/// Represents a pattern that should trigger an alert.
/// </summary>
public class AlertPattern
{
    /// <summary>
    /// Name of the alert pattern.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Regular expression pattern to match in the logs.
    /// </summary>
    public string RegexPattern { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the alert.
    /// </summary>
    public LogSeverity Severity { get; set; } = LogSeverity.Warning;

    /// <summary>
    /// Cooldown period (in minutes) before the same alert can be sent again.
    /// </summary>
    public int CooldownMinutes { get; set; } = 30;
}

/// <summary>
/// Severity level of the log alert.
/// </summary>
public enum LogSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
```

```csharp
// EmailNotifierOptions.cs
namespace CopilotChat.WebApi.Options;

/// <summary>
/// Configuration options for email notifications.
/// </summary>
public class EmailNotifierOptions
{
    /// <summary>
    /// SMTP server address.
    /// </summary>
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL for SMTP.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Email account username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email account password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Sender email address.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name.
    /// </summary>
    public string FromName { get; set; } = "Chat Copilot";

    /// <summary>
    /// Default recipient email addresses.
    /// </summary>
    public List<string> DefaultRecipients { get; set; } = new List<string>();
}
```

2. Update `appsettings.json` to include the new configuration sections:

```json
"LogMonitor": {
  "LogDirectoryPath": "./logs",
  "LogFilePattern": "*.log",
  "PollingIntervalSeconds": 60,
  "MaxLogEntriesToProcess": 1000,
  "AlertPatterns": [
    {
      "Name": "Critical Error",
      "RegexPattern": "\\bCRITICAL\\b|\\bFATAL\\b",
      "Severity": "Critical",
      "CooldownMinutes": 30
    },
    {
      "Name": "Error",
      "RegexPattern": "\\bERROR\\b|\\bEXCEPTION\\b",
      "Severity": "Error",
      "CooldownMinutes": 60
    }
  ]
},
"EmailNotifier": {
  "SmtpServer": "",
  "SmtpPort": 587,
  "UseSsl": true,
  "Username": "",
  "Password": "",
  "FromEmail": "",
  "FromName": "Chat Copilot",
  "DefaultRecipients": []
}
```

### Step 2: Create the Log Monitor Plugin

Create a new file `LogMonitorPlugin.cs` in the `/webapi/Plugins/` directory:

```csharp
// LogMonitorPlugin.cs
using System.ComponentModel;
using System.Text.RegularExpressions;
using CopilotChat.WebApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Plugins;

/// <summary>
/// Plugin for monitoring log files and detecting patterns.
/// </summary>
public class LogMonitorPlugin
{
    private readonly LogMonitorOptions _options;
    private readonly ILogger _logger;
    private readonly Dictionary<string, DateTime> _lastAlertTimes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogMonitorPlugin"/> class.
    /// </summary>
    public LogMonitorPlugin(IOptions<LogMonitorOptions> options, ILogger<LogMonitorPlugin> logger)
    {
        this._options = options.Value;
        this._logger = logger;
    }

    /// <summary>
    /// Scan log files for alert patterns and return any matches.
    /// </summary>
    [KernelFunction, Description("Scan log files for specified patterns and return any matches.")]
    public Task<string> ScanLogsAsync(
        [Description("Optional specific pattern to look for. If not provided, all configured patterns will be checked.")]
        string? patternName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(this._options.LogDirectoryPath))
            {
                return Task.FromResult($"Log directory not found: {this._options.LogDirectoryPath}");
            }

            var logFiles = Directory.GetFiles(this._options.LogDirectoryPath, this._options.LogFilePattern)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(10); // Limit to 10 most recent log files

            var detections = new List<LogAlertDetection>();

            foreach (var logFile in logFiles)
            {
                this._logger.LogInformation("Scanning log file: {LogFile}", logFile);

                using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fileStream);

                int lineCount = 0;
                string? line;
                while ((line = reader.ReadLine()) != null && lineCount < this._options.MaxLogEntriesToProcess)
                {
                    lineCount++;

                    foreach (var alertPattern in this._options.AlertPatterns)
                    {
                        // Skip if we're looking for a specific pattern and this isn't it
                        if (!string.IsNullOrEmpty(patternName) && !alertPattern.Name.Equals(patternName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (Regex.IsMatch(line, alertPattern.RegexPattern, RegexOptions.IgnoreCase))
                        {
                            // Check cooldown period
                            var patternKey = $"{Path.GetFileName(logFile)}_{alertPattern.Name}";
                            if (_lastAlertTimes.TryGetValue(patternKey, out DateTime lastTime))
                            {
                                var cooldownMinutes = TimeSpan.FromMinutes(alertPattern.CooldownMinutes);
                                if (DateTime.UtcNow - lastTime < cooldownMinutes)
                                {
                                    // Skip if in cooldown period
                                    continue;
                                }
                            }

                            // Update last alert time
                            _lastAlertTimes[patternKey] = DateTime.UtcNow;

                            detections.Add(new LogAlertDetection
                            {
                                Timestamp = DateTime.UtcNow,
                                LogFile = Path.GetFileName(logFile),
                                PatternName = alertPattern.Name,
                                Severity = alertPattern.Severity,
                                LineNumber = lineCount,
                                LineContent = line
                            });
                        }
                    }
                }
            }

            if (detections.Count == 0)
            {
                return Task.FromResult("No alert patterns detected in log files.");
            }

            // Format results
            var result = new StringBuilder();
            result.AppendLine($"Detected {detections.Count} alert pattern matches:");

            foreach (var detection in detections)
            {
                result.AppendLine();
                result.AppendLine($"- File: {detection.LogFile}");
                result.AppendLine($"  Pattern: {detection.PatternName}");
                result.AppendLine($"  Severity: {detection.Severity}");
                result.AppendLine($"  Line: {detection.LineNumber}");
                result.AppendLine($"  Content: {detection.LineContent}");
                result.AppendLine($"  Timestamp: {detection.Timestamp}");
            }

            return Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error scanning log files");
            return Task.FromResult($"Error scanning log files: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a list of all configured alert patterns.
    /// </summary>
    [KernelFunction, Description("Get a list of all configured alert patterns for log monitoring.")]
    public Task<string> GetAlertPatternsAsync()
    {
        var result = new StringBuilder();
        result.AppendLine($"Configured alert patterns ({this._options.AlertPatterns.Count}):");

        foreach (var pattern in this._options.AlertPatterns)
        {
            result.AppendLine();
            result.AppendLine($"- Name: {pattern.Name}");
            result.AppendLine($"  Pattern: {pattern.RegexPattern}");
            result.AppendLine($"  Severity: {pattern.Severity}");
            result.AppendLine($"  Cooldown: {pattern.CooldownMinutes} minutes");
        }

        return Task.FromResult(result.ToString());
    }
}

/// <summary>
/// Represents a detected alert in a log file.
/// </summary>
public class LogAlertDetection
{
    /// <summary>
    /// When the alert was detected.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Name of the log file where the alert was detected.
    /// </summary>
    public string LogFile { get; set; } = string.Empty;

    /// <summary>
    /// Name of the pattern that triggered the alert.
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the alert.
    /// </summary>
    public LogSeverity Severity { get; set; }

    /// <summary>
    /// Line number in the log file where the pattern was detected.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Content of the line that triggered the alert.
    /// </summary>
    public string LineContent { get; set; } = string.Empty;
}
```

### Step 3: Create the Email Notifier Plugin

Create a new file `EmailNotifierPlugin.cs` in the `/webapi/Plugins/` directory:

```csharp
// EmailNotifierPlugin.cs
using System.ComponentModel;
using System.Net;
using System.Net.Mail;
using System.Text;
using CopilotChat.WebApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Plugins;

/// <summary>
/// Plugin for sending email notifications.
/// </summary>
public class EmailNotifierPlugin
{
    private readonly EmailNotifierOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotifierPlugin"/> class.
    /// </summary>
    public EmailNotifierPlugin(IOptions<EmailNotifierOptions> options, ILogger<EmailNotifierPlugin> logger)
    {
        this._options = options.Value;
        this._logger = logger;
    }

    /// <summary>
    /// Send an email notification.
    /// </summary>
    [KernelFunction, Description("Send an email notification to one or more recipients.")]
    public async Task<string> SendEmailAsync(
        [Description("Subject of the email")]
        string subject,
        [Description("Body of the email")]
        string body,
        [Description("Comma-separated list of recipient email addresses. If empty, will use default recipients from configuration.")]
        string? recipients = null,
        [Description("Whether the email body contains HTML formatting")]
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(this._options.SmtpServer) || string.IsNullOrEmpty(this._options.Username))
            {
                return "Email notification is not configured. Please set up SMTP server details in the configuration.";
            }

            // Parse recipients
            List<string> emailRecipients;
            if (string.IsNullOrEmpty(recipients))
            {
                if (this._options.DefaultRecipients.Count == 0)
                {
                    return "No recipients specified and no default recipients configured.";
                }
                emailRecipients = this._options.DefaultRecipients;
            }
            else
            {
                emailRecipients = recipients.Split(',', ';')
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                if (emailRecipients.Count == 0)
                {
                    return "No valid recipients specified.";
                }
            }

            // Create mail message
            var message = new MailMessage
            {
                From = new MailAddress(this._options.FromEmail, this._options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            // Add recipients
            foreach (var recipient in emailRecipients)
            {
                message.To.Add(recipient);
            }

            // Create SMTP client
            using var client = new SmtpClient(this._options.SmtpServer, this._options.SmtpPort)
            {
                EnableSsl = this._options.UseSsl,
                Credentials = new NetworkCredential(this._options.Username, this._options.Password)
            };

            // Send email
            await client.SendMailAsync(message, cancellationToken);

            this._logger.LogInformation("Email notification sent: {Subject} to {Recipients}", subject, string.Join(", ", emailRecipients));
            return $"Email sent successfully to {emailRecipients.Count} recipient(s).";
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error sending email notification");
            return $"Error sending email notification: {ex.Message}";
        }
    }

    /// <summary>
    /// Format log alerts into an email notification.
    /// </summary>
    [KernelFunction, Description("Format log alerts into an HTML email notification.")]
    public Task<string> FormatLogAlertsEmailAsync(
        [Description("The log alerts content from the ScanLogs function")]
        string logAlertsContent,
        [Description("Additional message to include in the email body")]
        string? additionalMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(logAlertsContent) || logAlertsContent.Contains("No alert patterns detected"))
            {
                return Task.FromResult("No alerts to format into an email.");
            }

            var emailSubject = "Chat Copilot Log Alert Notification";

            // Check if there are Critical alerts
            if (logAlertsContent.Contains("Severity: Critical"))
            {
                emailSubject = "CRITICAL: " + emailSubject;
            }
            else if (logAlertsContent.Contains("Severity: Error"))
            {
                emailSubject = "ERROR: " + emailSubject;
            }

            // Create HTML body
            var htmlBody = new StringBuilder();
            htmlBody.AppendLine("<html><body>");
            htmlBody.AppendLine("<h2>Chat Copilot Log Alert Notification</h2>");
            htmlBody.AppendLine("<p>The following alerts were detected in the application logs:</p>");

            // Add additional message if provided
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                htmlBody.AppendLine($"<p><strong>Note:</strong> {additionalMessage}</p>");
            }

            htmlBody.AppendLine("<div style='background-color: #f8f8f8; padding: 10px; border: 1px solid #ddd;'>");

            // Parse and format the log alerts
            var alertEntries = logAlertsContent.Split(new[] { "- File:" }, StringSplitOptions.RemoveEmptyEntries);

            // Skip the header if it exists
            int startIndex = 0;
            if (alertEntries.Length > 0 && !alertEntries[0].Contains("Pattern:"))
            {
                startIndex = 1;
            }

            for (int i = startIndex; i < alertEntries.Length; i++)
            {
                var entry = "- File:" + alertEntries[i];
                var lines = entry.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                htmlBody.AppendLine("<div style='margin-bottom: 15px; border-left: 4px solid #e74c3c; padding-left: 10px;'>");

                foreach (var line in lines)
                {
                    // Parse each line to add appropriate formatting
                    if (line.Contains("Severity: Critical"))
                    {
                        htmlBody.AppendLine($"<p><strong style='color: #e74c3c;'>{line.Trim()}</strong></p>");
                    }
                    else if (line.Contains("Severity: Error"))
                    {
                        htmlBody.AppendLine($"<p><strong style='color: #e67e22;'>{line.Trim()}</strong></p>");
                    }
                    else if (line.Contains("Severity: Warning"))
                    {
                        htmlBody.AppendLine($"<p><strong style='color: #f39c12;'>{line.Trim()}</strong></p>");
                    }
                    else if (line.Contains("Content:"))
                    {
                        htmlBody.AppendLine($"<pre style='background-color: #f1f1f1; padding: 5px; overflow-x: auto;'>{line.Replace("  Content: ", "").Trim()}</pre>");
                    }
                    else
                    {
                        htmlBody.AppendLine($"<p>{line.Trim()}</p>");
                    }
                }

                htmlBody.AppendLine("</div>");
            }

            htmlBody.AppendLine("</div>");
            htmlBody.AppendLine("<p>This is an automated notification from Chat Copilot.</p>");
            htmlBody.AppendLine("</body></html>");

            // Create result with subject and body for the SendEmail function
            var result = new
            {
                Subject = emailSubject,
                Body = htmlBody.ToString(),
                IsHtml = true
            };

            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error formatting log alerts email");
            return Task.FromResult($"Error formatting log alerts email: {ex.Message}");
        }
    }

    /// <summary>
    /// Send an alert email about log patterns.
    /// </summary>
    [KernelFunction, Description("Scan logs for alert patterns and send an email notification about any issues found.")]
    public async Task<string> SendLogAlertEmailAsync(
        [Description("Comma-separated list of recipient email addresses. If empty, will use default recipients from configuration.")]
        string? recipients = null,
        [Description("Optional specific pattern to look for. If not provided, all configured patterns will be checked.")]
        string? patternName = null,
        [Description("Additional message to include in the email body")]
        string? additionalMessage = null,
        [Description("Whether to force sending the email even if no alerts are found (for testing)")]
        bool forceSend = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This method will need to use the LogMonitorPlugin
            // In a real implementation, you would use dependency injection to get the plugin
            // For the plan, we'll just outline the steps that would be needed

            return "This method needs to be implemented in the actual code by calling the LogMonitorPlugin to scan logs and then using SendEmailAsync to send the email.";
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error sending log alert email");
            return $"Error sending log alert email: {ex.Message}";
        }
    }
}
```

### Step 4: Create a Log Monitoring Service

Create a background service to monitor logs periodically:

```csharp
// LogMonitoringService.cs
using CopilotChat.WebApi.Options;
using Microsoft.Extensions.Options;

namespace CopilotChat.WebApi.Services;

/// <summary>
/// Background service for monitoring logs and sending notifications.
/// </summary>
public class LogMonitoringService : BackgroundService
{
    private readonly LogMonitorPlugin _logMonitorPlugin;
    private readonly EmailNotifierPlugin _emailNotifierPlugin;
    private readonly LogMonitorOptions _options;
    private readonly ILogger<LogMonitoringService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogMonitoringService"/> class.
    /// </summary>
    public LogMonitoringService(
        LogMonitorPlugin logMonitorPlugin,
        EmailNotifierPlugin emailNotifierPlugin,
        IOptions<LogMonitorOptions> options,
        ILogger<LogMonitoringService> logger)
    {
        this._logMonitorPlugin = logMonitorPlugin;
        this._emailNotifierPlugin = emailNotifierPlugin;
        this._options = options.Value;
        this._logger = logger;
    }

    /// <summary>
    /// Executes the background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if monitoring is enabled
        if (string.IsNullOrEmpty(this._options.LogDirectoryPath))
        {
            this._logger.LogWarning("Log monitoring is not configured (LogDirectoryPath is empty). Background service will not run.");
            return;
        }

        this._logger.LogInformation("Log monitoring service started. Polling every {Interval} seconds.", this._options.PollingIntervalSeconds);

        // Check for log alerts periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Scan logs for alert patterns
                var alertsContent = await this._logMonitorPlugin.ScanLogsAsync(null, stoppingToken);

                // If alerts were found, send an email notification
                if (!string.IsNullOrEmpty(alertsContent) && !alertsContent.Contains("No alert patterns detected"))
                {
                    // Format the email content
                    var emailFormatResult = await this._emailNotifierPlugin.FormatLogAlertsEmailAsync(alertsContent, null, stoppingToken);

                    if (!emailFormatResult.Contains("No alerts to format"))
                    {
                        try {
                            // Parse the JSON result
                            var emailData = System.Text.Json.JsonSerializer.Deserialize<EmailData>(emailFormatResult);

                            if (emailData != null)
                            {
                                // Send the email
                                await this._emailNotifierPlugin.SendEmailAsync(
                                    emailData.Subject,
                                    emailData.Body,
                                    null, // Use default recipients
                                    emailData.IsHtml,
                                    stoppingToken);
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            this._logger.LogError(ex, "Error parsing email format result");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error in log monitoring service");
            }

            // Wait for the next polling interval
            await Task.Delay(TimeSpan.FromSeconds(this._options.PollingIntervalSeconds), stoppingToken);
        }
    }

    private class EmailData
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
    }
}
```

### Step 5: Register the Plugins and Services

Update `Program.cs` to register the plugins and services:

```csharp
// In Program.cs, inside ConfigureServices or similar method

// Register options
builder.Services.Configure<LogMonitorOptions>(builder.Configuration.GetSection("LogMonitor"));
builder.Services.Configure<EmailNotifierOptions>(builder.Configuration.GetSection("EmailNotifier"));

// Register plugins
builder.Services.AddSingleton<LogMonitorPlugin>();
builder.Services.AddSingleton<EmailNotifierPlugin>();

// Register background service
builder.Services.AddHostedService<LogMonitoringService>();
```

### Step 6: Add Log Monitor Plugin to Semantic Kernel

Update the plugin registration in the `SemanticKernelProvider.cs` file or where the kernel is initialized:

```csharp
// In SemanticKernelProvider.cs or similar file, inside the method where plugins are registered

// Add log monitoring and email notification plugins
kernel.Plugins.AddFromObject(serviceProvider.GetRequiredService<LogMonitorPlugin>(), "LogMonitor");
kernel.Plugins.AddFromObject(serviceProvider.GetRequiredService<EmailNotifierPlugin>(), "EmailNotifier");
```

### Step 7: Secure Email Credentials

For security, we need to make sure email credentials are secured properly:

1. Update the configuration to use secrets rather than storing credentials in appsettings.json:

```json
"EmailNotifier": {
  "SmtpServer": "", // Set in user secrets
  "SmtpPort": 587,
  "UseSsl": true,
  "Username": "", // Set in user secrets
  "Password": "", // Set in user secrets
  "FromEmail": "", // Set in user secrets
  "FromName": "Chat Copilot",
  "DefaultRecipients": [] // Set in user secrets
}
```

2. Use the `dotnet user-secrets` command to set the sensitive information:

```
dotnet user-secrets set "EmailNotifier:SmtpServer" "smtp.example.com"
dotnet user-secrets set "EmailNotifier:Username" "username"
dotnet user-secrets set "EmailNotifier:Password" "password"
dotnet user-secrets set "EmailNotifier:FromEmail" "from@example.com"
dotnet user-secrets set "EmailNotifier:DefaultRecipients:0" "recipient1@example.com"
dotnet user-secrets set "EmailNotifier:DefaultRecipients:1" "recipient2@example.com"
```

## Testing and Verification

### Step 1: Create Unit Tests

Create unit tests for the new plugins:

1. Create tests for LogMonitorPlugin
2. Create tests for EmailNotifierPlugin
3. Mock the dependencies to ensure tests are isolated

### Step 2: Manual Testing Steps

1. Configure the plugins with test settings
2. Create test log files with patterns that should trigger alerts
3. Test the ScanLogsAsync function directly
4. Test the SendEmailAsync function with test recipients
5. Test the SendLogAlertEmailAsync function to verify integration

## Code Locations That Need to Be Modified

1. `/webapi/Options/` - Add new option classes
2. `/webapi/Plugins/` - Add new plugin classes
3. `/webapi/Services/` - Add LogMonitoringService
4. `/webapi/Program.cs` - Register services and options
5. `/webapi/appsettings.json` - Add configuration sections
6. `/webapi/SemanticKernelProvider.cs` or equivalent - Register plugins with the kernel

## Dependencies

1. **System.Net.Mail**: For sending emails
2. **Microsoft.Extensions.Hosting**: For background service
3. **Microsoft.Extensions.Options**: For configuration options
4. **System.Text.RegularExpressions**: For pattern matching in logs

## Future Enhancements

1. **Web UI Integration**: Add UI components to configure monitoring patterns
2. **Alert Dashboard**: Create a dashboard showing recent alerts
3. **SMS Notifications**: Add capability to send SMS notifications for critical alerts
4. **Alert Aggregation**: Group similar alerts to reduce notification volume
5. **Custom Templates**: Allow customization of email templates
6. **Metrics and Reporting**: Track alert trends over time

## Conclusion

This implementation plan provides a complete roadmap for adding log monitoring and email notification capabilities to the Chat Copilot application. By following this approach, you'll create a plugin that:

1. Monitors log files for specified patterns
2. Sends email notifications when important log events are detected
3. Integrates with the Semantic Kernel framework as plugin functions
4. Can be used directly by the LLM during conversations

The implementation is designed to be configurable, secure, and scalable to meet the needs of your application.
