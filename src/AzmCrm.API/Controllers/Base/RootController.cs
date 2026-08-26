using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace AzmCrm.API.Controllers.Base;

/// <summary>
/// Root controller for API landing page
/// </summary>
[ApiController]
[Route("")]
public class RootController(IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// API landing page
    /// </summary>
    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult GetLandingPage()
    {
        var version = GetApplicationVersion();
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var swaggerUrl = "/swagger";
        var healthUrl = "/health";

        // Add cache-control headers to prevent caching
        Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Expires", "0");

        var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Azm CRM API</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}

        .container {{
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            max-width: 600px;
            width: 100%;
            padding: 40px;
            animation: fadeIn 0.6s ease-in-out;
        }}

        @keyframes fadeIn {{
            from {{
                opacity: 0;
                transform: translateY(20px);
            }}
            to {{
                opacity: 1;
                transform: translateY(0);
            }}
        }}

        .header {{
            text-align: center;
            margin-bottom: 40px;
        }}

        .logo {{
            width: 80px;
            height: 80px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 20px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 20px;
            box-shadow: 0 10px 30px rgba(102, 126, 234, 0.3);
        }}

        .logo svg {{
            width: 50px;
            height: 50px;
            fill: white;
        }}

        h1 {{
            color: #2d3748;
            font-size: 32px;
            font-weight: 700;
            margin-bottom: 10px;
        }}

        .subtitle {{
            color: #718096;
            font-size: 16px;
            margin-bottom: 10px;
        }}

        .badge {{
            display: inline-block;
            background: #48bb78;
            color: white;
            padding: 6px 16px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 600;
            margin-top: 10px;
        }}

        .links {{
            display: flex;
            flex-direction: column;
            gap: 16px;
            margin-top: 30px;
        }}

        .link-card {{
            display: flex;
            align-items: center;
            padding: 20px;
            background: #f7fafc;
            border-radius: 12px;
            text-decoration: none;
            color: #2d3748;
            transition: all 0.3s ease;
            border: 2px solid transparent;
        }}

        .link-card:hover {{
            background: #edf2f7;
            border-color: #667eea;
            transform: translateX(5px);
        }}

        .link-icon {{
            width: 48px;
            height: 48px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin-right: 16px;
            flex-shrink: 0;
        }}

        .link-icon svg {{
            width: 24px;
            height: 24px;
            fill: white;
        }}

        .link-content {{
            flex: 1;
        }}

        .link-title {{
            font-size: 18px;
            font-weight: 600;
            color: #2d3748;
            margin-bottom: 4px;
        }}

        .link-description {{
            font-size: 14px;
            color: #718096;
        }}

        .link-arrow {{
            color: #a0aec0;
            font-size: 24px;
            transition: transform 0.3s ease;
        }}

        .link-card:hover .link-arrow {{
            transform: translateX(5px);
        }}

        .footer {{
            text-align: center;
            margin-top: 40px;
            padding-top: 30px;
            border-top: 1px solid #e2e8f0;
        }}

        .info-grid {{
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 16px;
            margin-bottom: 20px;
        }}

        .info-item {{
            text-align: center;
            padding: 16px;
            background: #f7fafc;
            border-radius: 8px;
        }}

        .info-label {{
            font-size: 12px;
            color: #718096;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 4px;
        }}

        .info-value {{
            font-size: 16px;
            color: #2d3748;
            font-weight: 600;
        }}

        .copyright {{
            color: #a0aec0;
            font-size: 14px;
        }}

        @media (max-width: 640px) {{
            .container {{
                padding: 30px 20px;
            }}

            h1 {{
                font-size: 24px;
            }}

            .info-grid {{
                grid-template-columns: 1fr;
            }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>
                <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
                    <path d='M12 2L2 7v10c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V7l-10-5zm0 18c-3.86-.93-7-5.43-7-10V8.3l7-3.11 7 3.11V10c0 4.57-3.14 9.07-7 10z'/>
                    <path d='M11 7h2v6h-2zm0 8h2v2h-2z'/>
                </svg>
            </div>
            <h1>Azm CRM API</h1>
            <p class='subtitle'>Customer Relationship Management Platform</p>
            <span class='badge'>✓ Online</span>
        </div>

        <div class='links'>
            <a href='{swaggerUrl}' class='link-card'>
                <div class='link-icon'>
                    <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
                        <path d='M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z'/>
                    </svg>
                </div>
                <div class='link-content'>
                    <div class='link-title'>API Documentation</div>
                    <div class='link-description'>Explore and test API endpoints</div>
                </div>
                <span class='link-arrow'>→</span>
            </a>

            <a href='{healthUrl}' class='link-card'>
                <div class='link-icon'>
                    <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
                        <path d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z'/>
                    </svg>
                </div>
                <div class='link-content'>
                    <div class='link-title'>Health Check</div>
                    <div class='link-description'>View API health status</div>
                </div>
                <span class='link-arrow'>→</span>
            </a>
        </div>

        <div class='footer'>
            <div class='info-grid'>
                <div class='info-item'>
                    <div class='info-label'>Version</div>
                    <div class='info-value'>{version}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Environment</div>
                    <div class='info-value'>{environment}</div>
                </div>
            </div>
            <p class='copyright'>© 2026 Azm CRM. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        return Content(html, "text/html");
    }

    private static string GetApplicationVersion()
    {
        // Try to get from environment variable first (set by CI/CD pipeline)
        var envVersion = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrEmpty(envVersion))
        {
            // Return short SHA (first 7 characters) for better readability
            return envVersion.Length > 7 ? envVersion.Substring(0, 7) : envVersion;
        }

        // Fallback to assembly version for local development
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}
