using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using NextHorizon.Data;
using NextHorizon.Data.Messaging;
using NextHorizon.Models;
using NextHorizon.Services;
using NextHorizon.Security;

var builder = WebApplication.CreateBuilder(args);

// Get connection string with validation
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

// Add services to the container.
builder.Services.AddControllersWithViews();

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"]
    ?? builder.Configuration["urls"];
var hasConfiguredHttpsUrl = !string.IsNullOrWhiteSpace(configuredUrls)
    && configuredUrls
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

// Database contexts
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection));

// Scoped services
builder.Services.AddHostedService<NextHorizon.Services.AutoCompleteOrderService>();
builder.Services.AddScoped<ISellerContextService, SellerContextService>();
builder.Services.AddScoped<ISellerPerformanceService, SellerPerformanceService>();
builder.Services.AddScoped<ICustomerStoredProcedureService, CustomerStoredProcedureService>();
builder.Services.AddScoped<IMessagingRepository, MessagingStoredProcedureRepository>();
builder.Services.AddScoped<IOrderConversationResolver, SimulatedOrderConversationResolver>();
builder.Services.AddScoped<IAuthenticatedUserContextService, AuthenticatedUserContextService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ISellerNotificationService, SellerNotificationService>();
// Anti-forgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

// Authentication & Authorization - FIXED: Added default scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
    options.DefaultAuthenticateScheme = "Cookies";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthorization();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddFixedWindowLimiter("upload-write", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    options.AddFixedWindowLimiter("csrf-token", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    options.AddFixedWindowLimiter("conversation-create", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    options.AddFixedWindowLimiter("message-send", limiter =>
    {
        limiter.PermitLimit = 45;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    options.AddFixedWindowLimiter("message-read", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

// Apply migrations (optional - don't fail startup if DB unavailable)
/* Disabled due to schema mismatch issues
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Console.WriteLine("Testing database connection...");
        var canConnect = dbContext.Database.CanConnect();
        Console.WriteLine($"Database connection test: {canConnect}");

        if (canConnect)
        {
            Console.WriteLine("Attempting to migrate database...");
            dbContext.Database.Migrate();
            Console.WriteLine("Database migration completed successfully.");
        }
        else
        {
            Console.WriteLine("Cannot connect to database. Skipping migration. Application will continue without database.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database operation failed: {ex.Message}");
        Console.WriteLine("Application will continue without database migration.");
       
    }
}
*/

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // The default HSTS value is 30 days
}

if (hasConfiguredHttpsUrl)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// Middleware order is important
app.UseRateLimiter();
app.UseSession();
app.Use(async (context, next) =>
{
    const string sharedUserIdCookie = "NextHorizon.SharedUserId";
    const string sharedUserTypeCookie = "NextHorizon.SharedUserType";

    var isSellerSessionMissing = string.IsNullOrWhiteSpace(context.Session.GetString("SellerEmail"))
        || !context.Session.GetInt32("SellerId").HasValue;
    var isSharedSeller = string.Equals(
        context.Request.Cookies[sharedUserTypeCookie],
        "Seller",
        StringComparison.OrdinalIgnoreCase);

    if (isSellerSessionMissing
        && isSharedSeller
        && int.TryParse(context.Request.Cookies[sharedUserIdCookie], out var sharedUserId))
    {
        var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
        var seller = await dbContext.SellerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == sharedUserId);

        if (seller != null)
        {
            context.Session.SetInt32("UserId", sharedUserId);
            context.Session.SetString("SellerEmail", seller.BusinessEmail ?? string.Empty);
            context.Session.SetInt32("SellerId", seller.SellerId);
            context.Session.SetString("SellerName", seller.BusinessName ?? "Seller");
        }
    }

    await next();
});
app.UseAuthentication(); // Now this will work with the configured scheme
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
