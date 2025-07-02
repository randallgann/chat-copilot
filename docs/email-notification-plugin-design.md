# Email Notification Plugin Design

This document outlines the design and implementation plan for creating an email notification plugin for Chat Copilot. The plugin will analyze log data and send email notifications based on specified criteria.

## Overview

The Email Notification Plugin will:

1. Scan log files for specific patterns or error conditions
2. Format detected issues into a readable summary
3. Send email notifications to specified recipients
4. Be configurable for different log sources, patterns, and notification settings

## Architecture

The plugin will be implemented as a native plugin for Semantic Kernel, similar to the existing plugins in the codebase. It will follow the OpenAPI plugin pattern used by other Chat Copilot plugins.

```
+---------------------+        +---------------------+        +---------------------+
|                     |        |                     |        |                     |
|   Semantic Kernel   |------->|  EmailPlugin Class  |------->|  Email SMTP Client  |
|                     |        |                     |        |                     |
+---------------------+        +---------------------+        +---------------------+
                                       |
                                       |
                                       v
                               +---------------------+
                               |                     |
                               |   Log File Parser   |
                               |                     |
                               +---------------------+
```

## Implementation Steps

### Step 1: Create the Plugin Project Structure

1. Create a new plugin folder in the `plugins` directory:
   ```
   plugins/email-notifier/
   ```

2. Create the necessary project files:
   ```
   - EmailNotifier.csproj
   - Program.cs
   - Models/
     - PluginConfig.cs
     - EmailConfig.cs
     - LogAnalysisResult.cs
   - Icons/
     - email.png
   - PluginEndpoint.cs
   - README.md
   ```

### Step 2: Implement Plugin Configuration

1. Create the plugin configuration class in `Models/PluginConfig.cs`:
   ```csharp
   namespace Plugins.EmailNotifier.Models;

   public class PluginConfig
   {
       public EmailConfig Email { get; set; } = new();
       public LogAnalysisConfig LogAnalysis { get; set; } = new();
   }

   public class EmailConfig
   {
       public string SmtpServer { get; set; } = string.Empty;
       public int SmtpPort { get; set; } = 587;
       public string SmtpUsername { get; set; } = string.Empty;
       public string SmtpPassword { get; set; } = string.Empty;
       public string FromAddress { get; set; } = string.Empty;
       public string DefaultToAddress { get; set; } = string.Empty;
       public bool UseSsl { get; set; } = true;
   }

   public class LogAnalysisConfig
   {
       public string LogDirectory { get; set; } = string.Empty;
       public string LogFilePattern { get; set; } = "*.log";
       public List<string> ErrorPatterns { get; set; } = new();
       public int MaxAgeMinutes { get; set; } = 60;
       public int AnalysisIntervalMinutes { get; set; } = 15;
   }
   ```

2. Add the configuration section to `appsettings.json` in the webapi project:
   ```json
   "EmailNotifier": {
     "Email": {
       "SmtpServer": "",
       "SmtpPort": 587,
       "SmtpUsername": "",
       "SmtpPassword": "",
       "FromAddress": "",
       "DefaultToAddress": "",
       "UseSsl": true
     },
     "LogAnalysis": {
       "LogDirectory": "./logs",
       "LogFilePattern": "*.log",
       "ErrorPatterns": [
         "ERROR",
         "CRITICAL",
         "EXCEPTION"
       ],
       "MaxAgeMinutes": 60,
       "AnalysisIntervalMinutes": 15
     }
   }
   ```

### Step 3: Implement the Log Analyzer

1. Create a LogAnalyzer class to parse and analyze log files:
   ```csharp
   namespace Plugins.EmailNotifier;

   public class LogAnalyzer
   {
       private readonly LogAnalysisConfig _config;
       private readonly ILogger _logger;

       public LogAnalyzer(LogAnalysisConfig config, ILogger logger)
       {
           _config = config;
           _logger = logger;
       }

       public async Task<List<LogAnalysisResult>> AnalyzeLogsAsync(CancellationToken cancellationToken = default)
       {
           var results = new List<LogAnalysisResult>();
           var logFiles = Directory.GetFiles(_config.LogDirectory, _config.LogFilePattern);
           
           foreach (var logFile in logFiles)
           {
               // Filter files by age
               var fileInfo = new FileInfo(logFile);
               if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromMinutes(_config.MaxAgeMinutes))
               {
                   continue;
               }
               
               // Process each log file
               using var reader = new StreamReader(logFile);
               string? line;
               int lineNumber = 0;
               
               while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
               {
                   lineNumber++;
                   foreach (var pattern in _config.ErrorPatterns)
                   {
                       if (line.Contains(pattern))
                       {
                           results.Add(new LogAnalysisResult
                           {
                               FilePath = logFile,
                               LineNumber = lineNumber,
                               Content = line,
                               Pattern = pattern,
                               Timestamp = DateTime.Now
                           });
                           break;
                       }
                   }
               }
           }
           
           return results;
       }
   }
   ```

2. Create the LogAnalysisResult model:
   ```csharp
   namespace Plugins.EmailNotifier.Models;

   public class LogAnalysisResult
   {
       public string FilePath { get; set; } = string.Empty;
       public int LineNumber { get; set; }
       public string Content { get; set; } = string.Empty;
       public string Pattern { get; set; } = string.Empty;
       public DateTime Timestamp { get; set; }
   }
   ```

### Step 4: Implement the Email Service

1. Create an EmailService class to handle sending emails:
   ```csharp
   using System.Net;
   using System.Net.Mail;
   using Microsoft.Extensions.Logging;
   using Plugins.EmailNotifier.Models;

   namespace Plugins.EmailNotifier;

   public class EmailService
   {
       private readonly EmailConfig _config;
       private readonly ILogger _logger;

       public EmailService(EmailConfig config, ILogger logger)
       {
           _config = config;
           _logger = logger;
       }

       public async Task SendEmailAsync(
           string subject,
           string body,
           string toAddress = "",
           CancellationToken cancellationToken = default)
       {
           // Use default address if none provided
           if (string.IsNullOrEmpty(toAddress))
           {
               toAddress = _config.DefaultToAddress;
           }

           using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort)
           {
               UseDefaultCredentials = false,
               Credentials = new NetworkCredential(_config.SmtpUsername, _config.SmtpPassword),
               EnableSsl = _config.UseSsl
           };

           using var message = new MailMessage
           {
               From = new MailAddress(_config.FromAddress),
               Subject = subject,
               Body = body,
               IsBodyHtml = true
           };

           message.To.Add(toAddress);

           try
           {
               await client.SendMailAsync(message, cancellationToken);
               _logger.LogInformation("Email sent to {Recipient} with subject: {Subject}", toAddress, subject);
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Failed to send email to {Recipient}", toAddress);
               throw;
           }
       }
   }
   ```

### Step 5: Implement the Plugin Endpoint

1. Create the PluginEndpoint class to expose the OpenAPI functions:
   ```csharp
   using System.Net;
   using Microsoft.Azure.Functions.Worker;
   using Microsoft.Azure.Functions.Worker.Http;
   using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
   using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
   using Microsoft.Extensions.Logging;
   using Microsoft.OpenApi.Models;
   using Plugins.EmailNotifier.Models;
   using Plugins.PluginShared;

   namespace Plugins.EmailNotifier;

   public class PluginEndpoint
   {
       private readonly PluginConfig _config;
       private readonly ILogger _logger;
       private readonly EmailService _emailService;
       private readonly LogAnalyzer _logAnalyzer;

       public PluginEndpoint(
           PluginConfig config,
           ILogger<PluginEndpoint> logger)
       {
           _config = config;
           _logger = logger;
           _emailService = new EmailService(config.Email, logger);
           _logAnalyzer = new LogAnalyzer(config.LogAnalysis, logger);
       }

       [Function("WellKnownAIPluginManifest")]
       public async Task<HttpResponseData> WellKnownAIPluginManifestAsync(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/ai-plugin.json")]
           HttpRequestData req)
       {
           var pluginManifest = new PluginManifest()
           {
               NameForModel = "EmailNotifier",
               NameForHuman = "Email Notification Plugin",
               DescriptionForModel = "Plugin for analyzing logs and sending email notifications based on patterns. Use this to monitor log files and receive alerts when specific error patterns are found.",
               DescriptionForHuman = "Monitors logs and sends email notifications for detected issues",
               Auth = new PluginAuth()
               {
                   Type = "user_http"
               },
               Api = new PluginApi()
               {
                   Type = "openapi",
                   Url = $"{req.Url.Scheme}://{req.Url.Host}:{req.Url.Port}/swagger.json"
               },
               LogoUrl = $"{req.Url.Scheme}://{req.Url.Host}:{req.Url.Port}/.well-known/icon",
           };

           var response = req.CreateResponse(HttpStatusCode.OK);
           await response.WriteAsJsonAsync(pluginManifest);
           return response;
       }

       [Function("Icon")]
       public async Task<HttpResponseData> IconAsync(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/icon")]
           HttpRequestData req)
       {
           if (!File.Exists("./Icons/email.png"))
           {
               return req.CreateResponse(HttpStatusCode.NotFound);
           }

           using (var stream = new FileStream("./Icons/email.png", FileMode.Open))
           {
               var response = req.CreateResponse(HttpStatusCode.OK);
               response.Headers.Add("Content-Type", "image/png");
               await stream.CopyToAsync(response.Body);
               return response;
           }
       }

       [OpenApiOperation(operationId: "AnalyzeLogs", tags: ["LogMonitoring"], Description = "Analyzes log files for specific patterns and returns the results.")]
       [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
       [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<LogAnalysisResult>), Description = "Returns a list of log entries matching the specified patterns.")]
       [Function("AnalyzeLogs")]
       public async Task<HttpResponseData> AnalyzeLogsAsync(
           [HttpTrigger(AuthorizationLevel.Function, "get", Route = "logs/analyze")]
           HttpRequestData req,
           CancellationToken cancellationToken)
       {
           try
           {
               var results = await _logAnalyzer.AnalyzeLogsAsync(cancellationToken);
               var response = req.CreateResponse(HttpStatusCode.OK);
               await response.WriteAsJsonAsync(results, cancellationToken);
               return response;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error analyzing logs");
               var response = req.CreateResponse(HttpStatusCode.InternalServerError);
               await response.WriteStringAsync($"Error analyzing logs: {ex.Message}");
               return response;
           }
       }

       [OpenApiOperation(operationId: "SendEmail", tags: ["Email"], Description = "Sends an email with the specified content.")]
       [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
       [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Email recipient address")]
       [OpenApiParameter(name: "subject", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Email subject")]
       [OpenApiParameter(name: "body", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Email body content")]
       [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Email sent successfully")]
       [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid parameters")]
       [Function("SendEmail")]
       public async Task<HttpResponseData> SendEmailAsync(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "email/send")]
           HttpRequestData req,
           CancellationToken cancellationToken)
       {
           string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
           var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

           if (data == null || !data.TryGetValue("subject", out var subject) || !data.TryGetValue("body", out var body))
           {
               var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
               await badResponse.WriteStringAsync("Missing required parameters: subject and body are required");
               return badResponse;
           }

           data.TryGetValue("to", out var to);

           try
           {
               await _emailService.SendEmailAsync(subject, body, to, cancellationToken);
               var response = req.CreateResponse(HttpStatusCode.OK);
               await response.WriteStringAsync("Email sent successfully");
               return response;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error sending email");
               var response = req.CreateResponse(HttpStatusCode.InternalServerError);
               await response.WriteStringAsync($"Error sending email: {ex.Message}");
               return response;
           }
       }

       [OpenApiOperation(operationId: "NotifyLogIssues", tags: ["LogMonitoring"], Description = "Analyzes logs and sends an email notification if issues are found.")]
       [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
       [OpenApiParameter(name: "to", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Optional email recipient (uses default if not specified)")]
       [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Result of the notification operation")]
       [Function("NotifyLogIssues")]
       public async Task<HttpResponseData> NotifyLogIssuesAsync(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "logs/notify")]
           HttpRequestData req,
           CancellationToken cancellationToken)
       {
           string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
           var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
           string? recipient = null;
           
           if (data != null && data.TryGetValue("to", out var to))
           {
               recipient = to;
           }

           try
           {
               var results = await _logAnalyzer.AnalyzeLogsAsync(cancellationToken);
               if (results.Count == 0)
               {
                   var response = req.CreateResponse(HttpStatusCode.OK);
                   await response.WriteStringAsync("No issues found in logs");
                   return response;
               }

               // Generate email content
               var subject = $"Log Analysis Alert: {results.Count} issues detected";
               var body = GenerateEmailBody(results);

               // Send the email
               await _emailService.SendEmailAsync(subject, body, recipient ?? "", cancellationToken);

               var successResponse = req.CreateResponse(HttpStatusCode.OK);
               await successResponse.WriteStringAsync($"Email notification sent for {results.Count} log issues");
               return successResponse;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error in log notification process");
               var response = req.CreateResponse(HttpStatusCode.InternalServerError);
               await response.WriteStringAsync($"Error in log notification process: {ex.Message}");
               return response;
           }
       }

       private string GenerateEmailBody(List<LogAnalysisResult> results)
       {
           var sb = new System.Text.StringBuilder();
           sb.AppendLine("<html><body>");
           sb.AppendLine("<h1>Log Analysis Alert</h1>");
           sb.AppendLine($"<p>The following {results.Count} issues were detected in the logs:</p>");
           sb.AppendLine("<table border='1' cellpadding='3' cellspacing='0'>");
           sb.AppendLine("<tr><th>File</th><th>Line</th><th>Pattern</th><th>Timestamp</th><th>Content</th></tr>");

           foreach (var result in results)
           {
               sb.AppendLine("<tr>");
               sb.AppendLine($"<td>{Path.GetFileName(result.FilePath)}</td>");
               sb.AppendLine($"<td>{result.LineNumber}</td>");
               sb.AppendLine($"<td>{result.Pattern}</td>");
               sb.AppendLine($"<td>{result.Timestamp}</td>");
               sb.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(result.Content)}</td>");
               sb.AppendLine("</tr>");
           }

           sb.AppendLine("</table>");
           sb.AppendLine("<p>This is an automated message from the Chat Copilot Email Notification Plugin.</p>");
           sb.AppendLine("</body></html>");

           return sb.ToString();
       }
   }
   ```

### Step 6: Implement the Program.cs file

1. Create the Program.cs file to set up the plugin:
   ```csharp
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.DependencyInjection;
   using Microsoft.Extensions.Hosting;
   using Microsoft.Extensions.Logging;
   using Plugins.EmailNotifier;
   using Plugins.EmailNotifier.Models;

   var host = new HostBuilder()
       .ConfigureFunctionsWorkerDefaults()
       .ConfigureServices((hostContext, services) =>
       {
           var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .Build();

           var pluginConfig = new PluginConfig();
           configuration.GetSection("EmailNotifier").Bind(pluginConfig);
           services.AddSingleton(pluginConfig);
       })
       .Build();

   host.Run();
   ```

### Step 7: Implement Background Service for Scheduled Monitoring

1. Create a background service to periodically analyze logs:
   ```csharp
   namespace Plugins.EmailNotifier;

   public class LogMonitoringService : BackgroundService
   {
       private readonly PluginConfig _config;
       private readonly ILogger<LogMonitoringService> _logger;
       private readonly EmailService _emailService;
       private readonly LogAnalyzer _logAnalyzer;

       public LogMonitoringService(
           PluginConfig config,
           ILogger<LogMonitoringService> logger)
       {
           _config = config;
           _logger = logger;
           _emailService = new EmailService(config.Email, logger);
           _logAnalyzer = new LogAnalyzer(config.LogAnalysis, logger);
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
           _logger.LogInformation("Log Monitoring Service started");

           while (!stoppingToken.IsCancellationRequested)
           {
               try
               {
                   _logger.LogInformation("Running scheduled log analysis");
                   var results = await _logAnalyzer.AnalyzeLogsAsync(stoppingToken);

                   if (results.Count > 0)
                   {
                       _logger.LogInformation("Found {Count} issues in logs, sending notification email", results.Count);
                       var subject = $"Log Analysis Alert: {results.Count} issues detected";
                       var body = GenerateEmailBody(results);
                       await _emailService.SendEmailAsync(subject, body, string.Empty, stoppingToken);
                   }
                   else
                   {
                       _logger.LogInformation("No issues found in log analysis");
                   }
               }
               catch (Exception ex)
               {
                   _logger.LogError(ex, "Error in scheduled log analysis");
               }

               // Wait for the configured interval
               await Task.Delay(TimeSpan.FromMinutes(_config.LogAnalysis.AnalysisIntervalMinutes), stoppingToken);
           }
       }

       // Same method from the PluginEndpoint
       private string GenerateEmailBody(List<LogAnalysisResult> results)
       {
           // Same implementation as in PluginEndpoint
       }
   }
   ```

2. Register the background service in Program.cs:
   ```csharp
   services.AddHostedService<LogMonitoringService>();
   ```

### Step 8: Create Docker Configuration

1. Create a Dockerfile for the plugin:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src

   COPY ["plugins/shared/PluginShared.csproj", "plugins/shared/"]
   COPY ["plugins/email-notifier/EmailNotifier.csproj", "plugins/email-notifier/"]
   RUN dotnet restore "plugins/email-notifier/EmailNotifier.csproj"

   COPY ["plugins/shared/", "plugins/shared/"]
   COPY ["plugins/email-notifier/", "plugins/email-notifier/"]
   WORKDIR "/src/plugins/email-notifier"
   RUN dotnet build "EmailNotifier.csproj" -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish "EmailNotifier.csproj" -c Release -o /app/publish /p:UseAppHost=false

   FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "EmailNotifier.dll"]
   ```

2. Add the plugin to the docker-compose.yaml file:
   ```yaml
   email-notifier:
     build:
       context: ..
       dockerfile: plugins/email-notifier/Dockerfile
     environment:
       - ASPNETCORE_ENVIRONMENT=Development
       - EmailNotifier__Email__SmtpServer=${SMTP_SERVER}
       - EmailNotifier__Email__SmtpPort=${SMTP_PORT}
       - EmailNotifier__Email__SmtpUsername=${SMTP_USERNAME}
       - EmailNotifier__Email__SmtpPassword=${SMTP_PASSWORD}
       - EmailNotifier__Email__FromAddress=${SMTP_FROM_ADDRESS}
       - EmailNotifier__Email__DefaultToAddress=${SMTP_TO_ADDRESS}
       - EmailNotifier__LogAnalysis__LogDirectory=/logs
     volumes:
       - ../logs:/logs
     networks:
       - copilot-chat
   ```

### Step 9: Register the Plugin in the Web API

1. Add the plugin configuration to webapi/appsettings.json in the "Plugins" section:
   ```json
   "Plugins": [
     {
       "Name": "Email Notification Plugin",
       "ManifestDomain": "http://email-notifier:5000"
     }
   ]
   ```

2. Update the PluginController to handle the new plugin:
   - The existing PluginController should already be capable of handling the new plugin based on the configuration.

### Step 10: Create Integration Tests

1. Create tests to verify the plugin functionality:
   ```csharp
   using Microsoft.Extensions.Logging;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using Moq;
   using Plugins.EmailNotifier;
   using Plugins.EmailNotifier.Models;
   
   namespace EmailNotifier.Tests;
   
   [TestClass]
   public class EmailNotifierTests
   {
       private Mock<ILogger<LogAnalyzer>> _mockLogger;
       private LogAnalysisConfig _logAnalysisConfig;
       private string _testLogDir;
       
       [TestInitialize]
       public void Initialize()
       {
           _mockLogger = new Mock<ILogger<LogAnalyzer>>();
           _testLogDir = Path.Combine(Path.GetTempPath(), "EmailNotifierTests");
           Directory.CreateDirectory(_testLogDir);
           
           _logAnalysisConfig = new LogAnalysisConfig
           {
               LogDirectory = _testLogDir,
               LogFilePattern = "*.log",
               ErrorPatterns = new List<string> { "ERROR", "CRITICAL", "EXCEPTION" },
               MaxAgeMinutes = 60,
               AnalysisIntervalMinutes = 15
           };
       }
       
       [TestCleanup]
       public void Cleanup()
       {
           Directory.Delete(_testLogDir, true);
       }
       
       [TestMethod]
       public async Task AnalyzeLogs_FindsErrorPatterns()
       {
           // Arrange
           var testLogPath = Path.Combine(_testLogDir, "test.log");
           File.WriteAllLines(testLogPath, new[]
           {
               "2023-01-01 12:00:00 INFO: Application started",
               "2023-01-01 12:01:00 ERROR: Failed to connect to database",
               "2023-01-01 12:02:00 INFO: Retry connection",
               "2023-01-01 12:03:00 CRITICAL: System failure"
           });
           
           var analyzer = new LogAnalyzer(_logAnalysisConfig, _mockLogger.Object);
           
           // Act
           var results = await analyzer.AnalyzeLogsAsync();
           
           // Assert
           Assert.AreEqual(2, results.Count);
           Assert.AreEqual("ERROR", results[0].Pattern);
           Assert.AreEqual("CRITICAL", results[1].Pattern);
       }
   }
   ```

## Integration with the Chat Copilot UI

To enable users to interact with the Email Notification Plugin through the Chat Copilot UI, the following steps should be taken:

1. The plugin will be automatically discovered by Chat Copilot based on the plugin configuration in appsettings.json.
2. Users can enable the plugin for a chat session through the plugins panel in the UI.
3. Once enabled, users can interact with the plugin using natural language, such as:
   - "Check the logs for errors and send me an email if any are found."
   - "Send an email summarizing any critical errors in the logs."
   - "Configure the email notification plugin to monitor logs every 30 minutes."

## Configuration Requirements

To use the Email Notification Plugin, users will need to configure:

1. SMTP Server details:
   - Server address
   - Port
   - Username/password
   - SSL settings

2. Log monitoring settings:
   - Log directory location
   - File patterns to search
   - Error patterns to detect
   - Analysis interval
   - Email recipient address(es)

## Security Considerations

1. SMTP credentials should be stored securely, preferably using user secrets or key vault.
2. The plugin should validate input to prevent potential security issues.
3. Email content should be properly sanitized to prevent HTML injection or other vulnerabilities.
4. Access to log files should be properly managed to ensure only authorized files are read.

## Deployment Steps

1. Build the plugin:
   ```
   dotnet build plugins/email-notifier
   ```

2. Deploy the plugin using docker-compose:
   ```
   docker-compose -f docker/docker-compose.yaml up -d email-notifier
   ```

3. Configure the plugin through environment variables or settings updates.

4. Verify the plugin is properly registered in Chat Copilot.

## Future Enhancements

1. Support for multiple email recipients and distribution lists.
2. Advanced log pattern matching using regular expressions.
3. Filtering and grouping of similar log entries.
4. Customizable email templates.
5. Dashboard for viewing log analysis results and notification history.
6. Support for different notification channels (SMS, webhook, etc.).
7. Integration with external monitoring systems.