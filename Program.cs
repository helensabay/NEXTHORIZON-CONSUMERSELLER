using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using MyAspNetApp.Data;
using MyAspNetApp.Data.Messaging;
using MyAspNetApp.Models;
using MyAspNetApp.Security;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, ".aspnet", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Add MVC services
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".NextHorizon.Products.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
// Register profile-related services
builder.Services.AddScoped<MyAspNetApp.Services.OrderService>();
builder.Services.AddScoped<MyAspNetApp.Services.LeaderboardService>();
builder.Services.AddScoped<MyAspNetApp.Services.MediaPathService>();
builder.Services.AddScoped<MyAspNetApp.Services.ChallengeNotificationService>();
builder.Services.AddScoped<IMessagingRepository, MessagingStoredProcedureRepository>();
builder.Services.AddScoped<IAuthenticatedUserContextService, AuthenticatedUserContextService>();

// Add database context
var rawConnectionString = DbConnectionStringResolver.ResolveRequiredConnectionString(
    builder.Configuration,
    builder.Environment);

var connectionStringBuilder = new SqlConnectionStringBuilder(rawConnectionString)
{
    Encrypt = true,
    TrustServerCertificate = true,
    Pooling = true,
    MinPoolSize = 0
};

if (string.IsNullOrWhiteSpace(connectionStringBuilder.ConnectionString))
{
    throw new InvalidOperationException("Resolved DefaultConnection is empty.");
}

if (connectionStringBuilder.ConnectTimeout <= 0 || connectionStringBuilder.ConnectTimeout > 5)
{
    connectionStringBuilder.ConnectTimeout = 5;
}

if (connectionStringBuilder.MaxPoolSize < 200)
{
    connectionStringBuilder.MaxPoolSize = 200;
}

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionStringBuilder.ConnectionString;

var startupInitializationConnectionStringBuilder = new SqlConnectionStringBuilder(connectionStringBuilder.ConnectionString)
{
    Pooling = false,
    MinPoolSize = 0
};

startupInitializationConnectionStringBuilder.ConnectTimeout =
    Math.Min(connectionStringBuilder.ConnectTimeout, 5);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionStringBuilder.ConnectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 1,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorNumbersToAdd: new[] { -2, 4060, 40197, 40501, 40613, 49918, 49919, 49920 });
            sqlOptions.CommandTimeout(30);
        }));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await EnsureDatabaseSchemasAfterStartupAsync(
                app.Logger,
                startupInitializationConnectionStringBuilder.ConnectionString);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Database schema initialization failed after application startup. The app will continue to run, but database-backed features may be unavailable.");
        }
    });
});

// Configure the HTTP request pipeline.
// For debugging, always show the developer exception page so we can see the real error.
app.UseDeveloperExceptionPage();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;
        if (exception != null)
        {
            app.Logger.LogError(exception, "Unhandled exception caught by exception handler.");
        }

        var isDbTimeout =
            exception is TimeoutException ||
            exception is SqlException ||
            (exception is InvalidOperationException invalidOperationException &&
             invalidOperationException.Message.Contains("connection string", StringComparison.OrdinalIgnoreCase)) ||
            (exception is InvalidOperationException ioe && ioe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase)) ||
            exception?.InnerException is TimeoutException ||
            exception?.InnerException is SqlException ||
            (exception?.InnerException is InvalidOperationException innerInvalidOperationException &&
             innerInvalidOperationException.Message.Contains("connection string", StringComparison.OrdinalIgnoreCase)) ||
            (exception?.InnerException is InvalidOperationException innerIoe && innerIoe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase));

        if (isDbTimeout)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    message = "Database is temporarily unavailable. Please try again."
                }));
                return;
            }

            context.Response.Redirect("/Home/Error");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = "Unexpected server error."
            }));
        }
        else
        {
            context.Response.Redirect("/Home/Error");
        }
    });
});

app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
// Serve files from the etc-css folder at /etc-css (if present)
var etcCssPath = Path.Combine(builder.Environment.ContentRootPath, "etc-css");
if (Directory.Exists(etcCssPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(etcCssPath),
        RequestPath = "/etc-css"
    });
}
app.UseRouting();
app.UseCors();
app.UseSession();
app.Use(async (context, next) =>
{
    const string sharedUserIdCookie = "NextHorizon.SharedUserId";
    const string sharedUserEmailCookie = "NextHorizon.SharedUserEmail";
    const string sharedUserTypeCookie = "NextHorizon.SharedUserType";
    const string sharedDisplayNameCookie = "NextHorizon.SharedDisplayName";
    const string sharedCartCookie = "NextHorizon.SharedCart";

    if (!context.Session.GetInt32("UserId").HasValue &&
        int.TryParse(context.Request.Cookies[sharedUserIdCookie], out var userId))
    {
        context.Session.SetInt32("UserId", userId);
    }

    var sharedEmail = context.Request.Cookies[sharedUserEmailCookie];
    if (!string.IsNullOrWhiteSpace(sharedEmail) && string.IsNullOrWhiteSpace(context.Session.GetString("UserEmail")))
    {
        context.Session.SetString("UserEmail", sharedEmail);
    }

    var sharedUserType = context.Request.Cookies[sharedUserTypeCookie];
    if (!string.IsNullOrWhiteSpace(sharedUserType) && string.IsNullOrWhiteSpace(context.Session.GetString("UserType")))
    {
        context.Session.SetString("UserType", sharedUserType);
    }

    var sharedDisplayName = context.Request.Cookies[sharedDisplayNameCookie];
    if (!string.IsNullOrWhiteSpace(sharedDisplayName) && string.IsNullOrWhiteSpace(context.Session.GetString("DisplayName")))
    {
        context.Session.SetString("DisplayName", sharedDisplayName);
    }

    var sharedCart = context.Request.Cookies[sharedCartCookie];
    if (!string.IsNullOrWhiteSpace(sharedCart))
    {
        try
        {
            var cartItems = JsonSerializer.Deserialize<List<CartItem>>(sharedCart) ?? new List<CartItem>();
            ProductData.ReplaceCart(cartItems);
        }
        catch (JsonException)
        {
            ProductData.ReplaceCart(Array.Empty<CartItem>());
        }
    }
    else
    {
        ProductData.ReplaceCart(Array.Empty<CartItem>());
    }

    await next();
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var userType = context.Session.GetString("UserType") ?? string.Empty;
    var isLoggedIn = context.Session.GetInt32("UserId").HasValue;

    if (IsSellerAreaPath(path))
    {
        if (!isLoggedIn)
        {
            await RedirectToLoginAsync(context);
            return;
        }

        if (!string.Equals(userType, "Seller", StringComparison.OrdinalIgnoreCase))
        {
            await RejectRoleAsync(context, "/Home/Storefront", "Seller area is only available to seller accounts.");
            return;
        }
    }

    if (IsCustomerOnlyPath(path))
    {
        if (!isLoggedIn)
        {
            await RedirectToLoginAsync(context);
            return;
        }

        if (!string.Equals(userType, "Consumer", StringComparison.OrdinalIgnoreCase))
        {
            await RejectRoleAsync(context, "/Dashboard/SellerDashboard", "Customer area is only available to customer accounts.");
            return;
        }
    }

    if (IsCustomerBrowsingPath(path) && string.Equals(userType, "Seller", StringComparison.OrdinalIgnoreCase))
    {
        await RejectRoleAsync(context, "/Dashboard/SellerDashboard", "Customer area is only available to customer accounts.");
        return;
    }

    await next();
});

app.UseAuthorization();

// MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

static bool IsSellerAreaPath(PathString path)
{
    return path.StartsWithSegments("/Dashboard", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Seller", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Promotions", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Settings", StringComparison.OrdinalIgnoreCase);
}

static bool IsCustomerOnlyPath(PathString path)
{
    if (path.StartsWithSegments("/AccountProfile", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Order", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return path.StartsWithSegments("/Home/Checkout", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/BuyNow", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/PlaceOrder", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/Cart", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/MyOrders", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/OrderDetail", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/Wishlist", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/ConsumerMessenger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/Challenges", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/JoinChallenge", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Home/UploadChallengeActivity", StringComparison.OrdinalIgnoreCase);
}

static bool IsCustomerBrowsingPath(PathString path)
{
    if (!path.StartsWithSegments("/Home", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/Products", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/Product", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/Cart", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/Wishlist", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return !path.StartsWithSegments("/Home/Error", StringComparison.OrdinalIgnoreCase);
}

static Task RedirectToLoginAsync(HttpContext context)
{
    var returnUrl = Uri.EscapeDataString(context.Request.PathBase + context.Request.Path + context.Request.QueryString);
    context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
    return Task.CompletedTask;
}

static async Task RejectRoleAsync(HttpContext context, string redirectUrl, string message)
{
    if (IsApiLikeRequest(context))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, message }));
        return;
    }

    context.Response.Redirect(redirectUrl);
}

static bool IsApiLikeRequest(HttpContext context)
{
    return context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(context.Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
}

// Ensure the configured HTTP ports are available; if not, pick a nearby free port
int TryFindFreePort(int startPort, int maxAttempts = 50)
{
    for (int p = startPort; p < startPort + maxAttempts; p++)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, p);
            listener.Start();
            listener.Stop();
            return p;
        }
        catch
        {
            // port in use, try next
        }
    }

    return -1;
}

try
{
    // Inspect existing URLs (if any) and ensure they bindable. If a URL's port is in use, replace it with a free one.
    var currentUrls = app.Urls.ToList();
    if (currentUrls.Count > 0)
    {
        var replaced = false;
        var newUrls = new List<string>();
        foreach (var url in currentUrls)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.IsLoopback && uri.Port > 0)
                {
                    try
                    {
                        var testListener = new TcpListener(IPAddress.Loopback, uri.Port);
                        testListener.Start();
                        testListener.Stop();
                        newUrls.Add(url);
                    }
                    catch
                    {
                        var freePort = TryFindFreePort(uri.Port + 1);
                        if (freePort > 0)
                        {
                            replaced = true;
                            var newUrl = $"{uri.Scheme}://{uri.Host}:{freePort}";
                            newUrls.Add(newUrl);
                            app.Logger.LogWarning("Port {OldPort} was in use; switching URL to {NewUrl}", uri.Port, newUrl);
                        }
                        else
                        {
                            newUrls.Add(url); // give up, keep original
                        }
                    }
                }
                else
                {
                    newUrls.Add(url);
                }
            }
            catch
            {
                newUrls.Add(url);
            }
        }

        if (replaced)
        {
            app.Urls.Clear();
            foreach (var u in newUrls) app.Urls.Add(u);
        }
    }

    app.Run();
}
catch (IOException ex) when (ex.InnerException is AddressInUseException || ex.Message?.Contains("address already in use", StringComparison.OrdinalIgnoreCase) == true)
{
    app.Logger.LogError(ex, "Failed to start web host because configured address was already in use. Ensure no other process is binding the same HTTP port (dotnet processes or other servers).");
    throw;
}

static async Task EnsureDatabaseSchemasAfterStartupAsync(
    ILogger logger,
    string startupConnectionString,
    CancellationToken cancellationToken = default)
{
    using var startupInitializationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    startupInitializationCts.CancelAfter(TimeSpan.FromSeconds(20));

    static DbContextOptions<AppDbContext> CreateStartupDbOptions(string connectionString)
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 1,
                        maxRetryDelay: TimeSpan.FromSeconds(2),
                        errorNumbersToAdd: new[] { -2, 4060, 40197, 40501, 40613, 49918, 49919, 49920 });
                    sqlOptions.CommandTimeout(15);
                })
            .Options;

    try
    {
        await using var connectivityDbContext = new AppDbContext(CreateStartupDbOptions(startupConnectionString));
        var canConnect = await connectivityDbContext.Database.CanConnectAsync(startupInitializationCts.Token);
        if (!canConnect)
        {
            logger.LogWarning("Skipping database schema initialization after startup because the SQL Server connection check failed.");
            return;
        }

        await using (var messagingDbContext = new AppDbContext(CreateStartupDbOptions(startupConnectionString)))
        {
            await DatabaseMessagingInitializer.EnsureSchemaAsync(messagingDbContext, startupInitializationCts.Token);
        }

        await using (var commerceDbContext = new AppDbContext(CreateStartupDbOptions(startupConnectionString)))
        {
            await DatabaseSchemaInitializer.EnsureCommerceSchemaAsync(commerceDbContext, logger, startupInitializationCts.Token);
        }
    }
    catch (OperationCanceledException) when (startupInitializationCts.IsCancellationRequested)
    {
        logger.LogWarning("Skipping database schema initialization after startup because the SQL Server connection check timed out.");
    }
    catch (SqlException ex) when (ex.Number is -2 or 53 or 258)
    {
        logger.LogWarning(ex, "Skipping database schema initialization after startup because the SQL Server instance is unreachable.");
    }
    catch (SqlException ex) when (ex.Number == 3980)
    {
        logger.LogWarning(ex, "Skipping database schema initialization after startup because the SQL session was cancelled while initialization was running.");
    }
}
