using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NextHorizon.Data;
using NextHorizon.Models;

namespace NextHorizon.Services
{
    public class AutoCompleteOrderService : BackgroundService
    {
        private readonly ILogger<AutoCompleteOrderService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public AutoCompleteOrderService(ILogger<AutoCompleteOrderService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🕒 Auto-Complete Order Service is starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessGhostOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🚨 Fatal Error occurred while processing ghost orders.");
                }

                // Sleep for 24 hours before checking again
                // TO TEST THIS NOW: Change TimeSpan.FromHours(24) to TimeSpan.FromMinutes(1)
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task ProcessGhostOrdersAsync(CancellationToken stoppingToken)
        {
            // We must create a "scope" because BackgroundServices live forever, 
            // but our AppDbContext is designed to live only for a single web request.
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var sellerNotificationService = scope.ServiceProvider.GetRequiredService<ISellerNotificationService>();

                var twentyDaysAgo = DateTime.Now.AddDays(-20);

                var ghostOrders = await dbContext.Orders
                    .Where(o => o.Status == "Shipped" 
                             && o.DateShipped != null 
                             && o.DateShipped < twentyDaysAgo)
                    .ToListAsync(stoppingToken);

                if (!ghostOrders.Any())
                {
                    _logger.LogInformation("No ghost orders found today. Going back to sleep...");
                    return; 
                }

                _logger.LogInformation($"Found {ghostOrders.Count} ghost orders to auto-complete. Processing...");

                string connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is not configured.");

                foreach (var order in ghostOrders)
                {
                    // 3. Change the Order Status to your preferred "Delivered"
                    order.Status = "Delivered";
                    order.FulfillmentStatus = "Delivered";

                    // 4. THE MONEY TRANSFER (Raw SQL to match your Finance Dashboard logic)
                    using (var con = new SqlConnection(connectionString))
                    {
                        await con.OpenAsync(stoppingToken);

                        // This moves the money out of Pending and into Available for withdrawal
                        string walletQuery = @"
                            UPDATE seller_wallet
                            SET Pending_Balance = Pending_Balance - @Amount,
                                Available_Balance = Available_Balance + @Amount,
                                Total_Earned = Total_Earned + @Amount
                            WHERE Seller_Id = @SellerId";

                        using (var cmd = new SqlCommand(walletQuery, con))
                        {
                            // Assuming TotalAmount is what the seller earns. 
                            // If you have a platform fee, adjust this math!
                            cmd.Parameters.AddWithValue("@Amount", order.TotalAmount); 
                            cmd.Parameters.AddWithValue("@SellerId", order.seller_id);
                            await cmd.ExecuteNonQueryAsync(stoppingToken);
                        }
                    }

                    await sellerNotificationService.CreateIfMissingAsync(new SellerNotification
                    {
                        RecipientType = "seller",
                        RecipientId = order.seller_id.ToString(),
                        OrderId = order.OrderID,
                        Category = "order",
                        Title = "Order Delivered",
                        Message = $"Order #{order.OrderID} was automatically marked as delivered and the funds are now available in your wallet.",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    }, stoppingToken);
                }

                // 5. Save all the Order Status updates to the database in one single batch
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation($"✅ Successfully auto-delivered and released funds for {ghostOrders.Count} orders.");
            }
        }
    }
}
