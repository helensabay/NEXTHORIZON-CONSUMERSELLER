using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using NextHorizon.Models;
using System.Data;

namespace MyAspNetApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(AppDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> SellerDashboard(CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new SellerDashboardViewModel
            {
                SellerName = seller.BusinessName ?? "Seller",
                CurrentDate = DateTime.Now
            };

            await PopulateDashboardCountsAsync(model, seller.SellerId, cancellationToken);
            model.RecentOrders = await LoadRecentOrdersAsync(seller.SellerId, cancellationToken);

            ViewData["SellerName"] = model.SellerName;
            return View(model);
        }

        public async Task<IActionResult> OrderManagement(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var normalizedStartDate = startDate?.Date;
            var normalizedEndDate = endDate?.Date;
            if (normalizedStartDate.HasValue && normalizedEndDate.HasValue && normalizedStartDate > normalizedEndDate)
            {
                (normalizedStartDate, normalizedEndDate) = (normalizedEndDate, normalizedStartDate);
            }

            ViewBag.StartDate = normalizedStartDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = normalizedEndDate?.ToString("yyyy-MM-dd");
            ViewBag.Couriers = await LoadCouriersAsync(cancellationToken);
            ViewBag.ReturnRequests = await LoadReturnRequestsAsync(seller.SellerId, normalizedStartDate, normalizedEndDate, cancellationToken);
            ViewData["SellerName"] = seller.BusinessName ?? "Seller";

            return View(await LoadOrdersAsync(seller.SellerId, normalizedStartDate, normalizedEndDate, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return Unauthorized();
            }

            var order = (await LoadOrdersAsync(seller.SellerId, null, null, cancellationToken))
                .FirstOrDefault(item => item.OrderID == orderId);

            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found." });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    orderId = order.OrderID,
                    orderDate = order.OrderDate,
                    paymentMethod = order.PaymentMethod,
                    fullName = order.FullName,
                    streetAddress = order.StreetAddress,
                    city = order.City,
                    postalCode = order.PostalCode,
                    phoneNumber = order.PhoneNumber,
                    email = order.Email,
                    deliveryOption = order.Courier,
                    quantity = order.Quantity,
                    subtotal = order.Subtotal,
                    shippingFee = order.ShippingFee,
                    totalAmount = order.CalculatedTotal,
                    productName = order.ProductName
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveOrderNote([FromBody] OrderNoteRequest request, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            return Json(new { success = true, message = "Note saved successfully!" });
        }

        [HttpPost]
        public IActionResult AcceptOrder([FromBody] AcceptOrderRequest request)
        {
            return Json(new { success = true, message = "Order accepted." });
        }

        [HttpPost]
        public IActionResult DeclineOrder([FromBody] DeclineRequest request)
        {
            return Json(new { success = true, message = "Order declined." });
        }

        [HttpPost]
        public IActionResult MarkOrderShipped([FromForm] MarkShippedRequest request)
        {
            return Json(new { success = true, message = "Order marked as shipped." });
        }

        [HttpPost]
        public IActionResult MarkOrderReturned([FromForm] MarkReturnedRequest request)
        {
            return Json(new { success = true, message = "Order marked as failed delivery." });
        }

        [HttpPost]
        public IActionResult ReviewReturnRequest([FromBody] ReviewReturnRequestModel request)
        {
            return Json(new { success = true, message = "Return request updated." });
        }

        [HttpPost]
        public IActionResult MarkReturnItemReceived([FromBody] ReturnRequestAction request)
        {
            return Json(new { success = true, message = "Return item marked as received." });
        }

        [HttpPost]
        public IActionResult ConfirmReturnRefund([FromBody] ReturnRequestAction request)
        {
            return Json(new { success = true, message = "Refund confirmed." });
        }

        public async Task<IActionResult> Finance(CancellationToken cancellationToken)
        {
            var sellerId = HttpContext.Session.GetInt32("SellerId");
            var sellerName = HttpContext.Session.GetString("SellerName") ?? "Seller";

            if (!sellerId.HasValue)
            {
                var seller = await ResolveCurrentSellerAsync(cancellationToken);
                if (seller == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                sellerId = seller.SellerId;
                sellerName = seller.BusinessName ?? "Seller";
            }

            var model = await LoadFinanceDashboardDataAsync(sellerId.Value, sellerName, cancellationToken);
            ViewData["SellerName"] = model.SellerName;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionDetailsSP(string referenceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(referenceId))
            {
                return Json(new { success = false, message = "Reference id is required." });
            }

            var sellerId = HttpContext.Session.GetInt32("SellerId");
            if (!sellerId.HasValue)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await using var command = new SqlCommand("sp_GetTransactionDetailsSP", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 8
                };
                command.Parameters.AddWithValue("@SellerId", sellerId.Value);
                command.Parameters.AddWithValue("@ReferenceId", referenceId);

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return Json(new { success = false, message = "Transaction not found." });
                }

                var additionalDetails = new Dictionary<string, string?>();
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var name = reader.GetName(index);
                    if (name is "ReferenceId" or "TransactionDate" or "Type" or "Method" or "Amount" or "Status" or "Source")
                    {
                        continue;
                    }

                    additionalDetails[name] = reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
                }

                return Json(new
                {
                    success = true,
                    transaction = new TransactionDetailDto
                    {
                        ReferenceId = ReadString(reader, "ReferenceId", referenceId),
                        TransactionDate = ReadDate(reader, "TransactionDate", DateTime.Now),
                        Type = ReadString(reader, "Type"),
                        Method = ReadString(reader, "Method"),
                        Amount = ReadDecimal(reader, "Amount"),
                        Status = ReadString(reader, "Status"),
                        Source = ReadString(reader, "Source"),
                        AdditionalDetails = additionalDetails
                    }
                });
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to load transaction details for {ReferenceId}.", referenceId);
                return Json(new { success = false, message = "Transaction details are temporarily unavailable." });
            }
        }

        public async Task<IActionResult> Analytics(CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await LoadAnalyticsDashboardDataAsync(seller, cancellationToken);
            ViewData["SellerName"] = model.SellerName;
            return View(model);
        }

        public IActionResult HelpCenter()
        {
            if (!HttpContext.Session.GetInt32("SellerId").HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewData["DashboardHeading"] = "Help Center";
            return View("~/Views/Dashboard/HelpCenter.cshtml");
        }

        public IActionResult HelpCenterDrawer()
        {
            if (!HttpContext.Session.GetInt32("SellerId").HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            return View("~/Views/Dashboard/HelpCenterDrawer.cshtml");
        }

        public async Task<IActionResult> AccountSettings(CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewData["DashboardHeading"] = "Settings";
            ViewData["SellerName"] = string.IsNullOrWhiteSpace(seller.BusinessName) ? "Seller" : seller.BusinessName;
            return View("~/Views/Dashboard/AccountSettings.cshtml");
        }

        private async Task<FinanceViewModel> LoadFinanceDashboardDataAsync(
            int sellerId,
            string sellerName,
            CancellationToken cancellationToken)
        {
            var model = new FinanceViewModel
            {
                SellerName = string.IsNullOrWhiteSpace(sellerName) ? "Seller" : sellerName,
                CurrentDate = DateTime.Now
            };

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await using var command = new SqlCommand("sp_GetSellerFinanceDashboard", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 8
                };
                command.Parameters.AddWithValue("@SellerId", sellerId);

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    model.SellerName = ReadString(reader, "SellerName", model.SellerName);
                    model.CurrentDate = ReadDate(reader, "CurrentDate", DateTime.Now);
                }

                if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                {
                    model.AvailableBalance = ReadDecimal(reader, "Available_Balance");
                    model.PendingBalance = ReadDecimal(reader, "Pending_Balance");
                    model.TotalEarned = ReadDecimal(reader, "Total_Earned");
                    model.TotalWithdrawn = ReadDecimal(reader, "Total_Withdrawn");
                    model.PendingPayoutCount = ReadInt(reader, "PendingPayoutCount");
                    model.TotalPendingWithdrawal = ReadDecimal(reader, "TotalPendingWithdrawal");
                }

                if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                {
                    model.TodayRevenue = ReadDecimal(reader, "TodayRevenue");
                }

                if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                {
                    model.ThisMonthRevenue = ReadDecimal(reader, "ThisMonthRevenue");
                }

                if (await reader.NextResultAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        model.Transactions.Add(new FinanceTransactionViewModel
                        {
                            ReferenceId = ReadString(reader, "ReferenceId"),
                            TransactionDate = ReadDate(reader, "TransactionDate", DateTime.Now),
                            Type = ReadString(reader, "Type"),
                            Method = ReadString(reader, "Method"),
                            Amount = ReadDecimal(reader, "Amount"),
                            Status = ReadString(reader, "Status")
                        });
                    }
                }
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to load seller finance dashboard.");
                TempData["ErrorMessage"] = "Finance data is temporarily unavailable.";
            }

            return model;
        }

        private async Task<SellerDashboardViewModel> LoadAnalyticsDashboardDataAsync(
            Models.SellerProfile seller,
            CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var orders = await LoadOrdersAsync(seller.SellerId, null, null, cancellationToken);
            var nonCancelledOrders = orders
                .Where(order => !IsOrderStatus(order, "Cancelled", "Canceled"))
                .ToList();
            var recognizedOrders = nonCancelledOrders
                .Where(order => IsOrderStatus(order, "Delivered", "Completed", "Complete"))
                .ToList();
            var openOrders = nonCancelledOrders
                .Where(order => !IsOrderStatus(order, "Delivered", "Completed", "Complete", "Refunded", "Returned"))
                .ToList();
            var todayOrders = nonCancelledOrders
                .Where(order => order.OrderDate.Date == now.Date)
                .ToList();
            var yesterdayOrders = nonCancelledOrders
                .Where(order => order.OrderDate.Date == now.Date.AddDays(-1))
                .ToList();
            var shippedToday = nonCancelledOrders.Count(order =>
                order.OrderDate.Date == now.Date && IsOrderStatus(order, "Shipped", "To Ship"));
            var refundedOrders = orders
                .Where(order => IsOrderStatus(order, "Refunded", "Returned", "Return", "Item Returned"))
                .ToList();
            var cancelledOrders = orders.Count(order => IsOrderStatus(order, "Cancelled", "Canceled"));
            var revenueYears = recognizedOrders
                .Select(order => order.OrderDate.Year)
                .Append(now.Year)
                .Distinct()
                .OrderByDescending(year => year)
                .ToArray();

            var monthlyRevenueByYear = revenueYears.ToDictionary(
                year => year,
                year => Enumerable.Range(1, 12)
                    .Select(month => recognizedOrders
                        .Where(order => order.OrderDate.Year == year && order.OrderDate.Month == month)
                        .Sum(GetOrderAnalyticsAmount))
                    .ToList());
            var monthlyOrdersByYear = revenueYears.ToDictionary(
                year => year,
                year => Enumerable.Range(1, 12)
                    .Select(month => recognizedOrders
                        .Count(order => order.OrderDate.Year == year && order.OrderDate.Month == month))
                    .ToList());
            var monthlyUnitsByYear = revenueYears.ToDictionary(
                year => year,
                year => Enumerable.Range(1, 12)
                    .Select(month => recognizedOrders
                        .Where(order => order.OrderDate.Year == year && order.OrderDate.Month == month)
                        .Sum(order => Math.Max(0, order.Quantity)))
                    .ToList());
            var topProducts = BuildTopProducts(recognizedOrders, null, 10);

            return new SellerDashboardViewModel
            {
                SellerName = seller.BusinessName ?? "Seller",
                CurrentDate = now,
                TodayOrderValue = todayOrders.Sum(GetOrderAnalyticsAmount),
                YesterdayOrderValue = yesterdayOrders.Sum(GetOrderAnalyticsAmount),
                TodaySales = recognizedOrders
                    .Where(order => order.OrderDate.Date == now.Date)
                    .Sum(GetOrderAnalyticsAmount),
                TodayOrderCount = todayOrders.Count,
                TodayUnitsSold = todayOrders.Sum(order => Math.Max(0, order.Quantity)),
                ShippedTodayCount = shippedToday,
                InFulfillmentCount = openOrders.Count(order => IsOrderStatus(order, "Pending", "To Ship", "Processing", "Shipped")),
                OpenOrderCount = openOrders.Count,
                TotalUnitsSold = nonCancelledOrders.Sum(order => Math.Max(0, order.Quantity)),
                TotalOrders = nonCancelledOrders.Count,
                SalesGrowth = CalculateGrowth(
                    recognizedOrders.Where(order => order.OrderDate.Date == now.Date).Sum(GetOrderAnalyticsAmount),
                    recognizedOrders.Where(order => order.OrderDate.Date == now.Date.AddDays(-1)).Sum(GetOrderAnalyticsAmount)),
                TotalRevenue = recognizedOrders.Sum(GetOrderAnalyticsAmount),
                RecognizedRevenueToday = recognizedOrders
                    .Where(order => order.OrderDate.Date == now.Date)
                    .Sum(GetOrderAnalyticsAmount),
                YesterdayRecognizedRevenue = recognizedOrders
                    .Where(order => order.OrderDate.Date == now.Date.AddDays(-1))
                    .Sum(GetOrderAnalyticsAmount),
                AverageOrderValue = nonCancelledOrders.Count > 0
                    ? decimal.Round(nonCancelledOrders.Sum(GetOrderAnalyticsAmount) / nonCancelledOrders.Count, 2)
                    : 0m,
                RefundedRevenue = refundedOrders.Sum(GetOrderAnalyticsAmount),
                NetRevenue = recognizedOrders.Sum(GetOrderAnalyticsAmount) - refundedOrders.Sum(GetOrderAnalyticsAmount),
                PipelineRevenue = openOrders.Sum(GetOrderAnalyticsAmount),
                CancelledOrders = cancelledOrders,
                RefundedOrders = refundedOrders.Count,
                ActiveReturnRequests = orders.Count(order => IsOrderStatus(order, "Return", "Return Requested", "Return Approved")),
                TotalVisits = 0,
                MonthlyRevenueByYear = monthlyRevenueByYear,
                MonthlyOrdersByYear = monthlyOrdersByYear,
                MonthlyUnitsByYear = monthlyUnitsByYear,
                TopProducts = topProducts,
                TopCategories = BuildTopCategories(recognizedOrders, 5),
                TopProductsByRange = new Dictionary<string, List<TopSellingProduct>>
                {
                    ["TODAY"] = BuildTopProducts(recognizedOrders, now.Date, 10),
                    ["7D"] = BuildTopProducts(recognizedOrders.Where(order => order.OrderDate >= now.AddDays(-7)).ToList(), null, 10),
                    ["30D"] = BuildTopProducts(recognizedOrders.Where(order => order.OrderDate >= now.AddDays(-30)).ToList(), null, 10),
                    ["ALL"] = topProducts
                },
                RecentOrders = orders.Take(5).ToList()
            };
        }

        private static bool IsOrderStatus(Order order, params string[] statuses)
        {
            var status = (order.EffectiveStatus ?? order.Status ?? string.Empty).Trim();
            return statuses.Any(item => string.Equals(status, item, StringComparison.OrdinalIgnoreCase));
        }

        private static decimal GetOrderAnalyticsAmount(Order order)
        {
            var total = order.CalculatedTotal > 0 ? order.CalculatedTotal : order.TotalAmount;
            return total > 0 ? total : order.Amount;
        }

        private static decimal CalculateGrowth(decimal current, decimal previous)
        {
            if (previous == 0)
            {
                return current > 0 ? 100m : 0m;
            }

            return decimal.Round(((current - previous) / previous) * 100m, 2);
        }

        private static List<TopSellingProduct> BuildTopProducts(IEnumerable<Order> orders, DateTime? date, int topCount)
        {
            var filteredOrders = date.HasValue
                ? orders.Where(order => order.OrderDate.Date == date.Value.Date)
                : orders;

            return filteredOrders
                .GroupBy(order => string.IsNullOrWhiteSpace(order.ProductName) ? "Product unavailable" : order.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new TopSellingProduct
                {
                    ProductName = group.Key,
                    ImageUrl = group.Select(order => order.ProductImage).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty,
                    Sku = group.Select(order => order.Sku).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                    Category = group.SelectMany(order => order.OrderItems ?? new List<OrderItem>())
                        .Select(item => item.Product?.Category)
                        .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category)) ?? "Uncategorized",
                    UnitsSold = group.Sum(order => Math.Max(0, order.Quantity)),
                    RevenueGenerated = group.Sum(GetOrderAnalyticsAmount)
                })
                .OrderByDescending(product => product.RevenueGenerated)
                .ThenByDescending(product => product.UnitsSold)
                .Take(topCount)
                .Select((product, index) =>
                {
                    product.Rank = index + 1;
                    return product;
                })
                .ToList();
        }

        private static List<CategoryPerformanceViewModel> BuildTopCategories(IEnumerable<Order> orders, int topCount)
        {
            var categoryRows = orders
                .Select(order => new
                {
                    Category = (order.OrderItems ?? new List<OrderItem>())
                        .Select(item => item.Product?.Category)
                        .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category)) ?? "Uncategorized",
                    Units = Math.Max(0, order.Quantity),
                    Revenue = GetOrderAnalyticsAmount(order)
                })
                .GroupBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CategoryPerformanceViewModel
                {
                    Category = group.Key,
                    UnitsSold = group.Sum(row => row.Units),
                    RevenueGenerated = group.Sum(row => row.Revenue)
                })
                .OrderByDescending(category => category.RevenueGenerated)
                .Take(topCount)
                .ToList();

            var totalRevenue = categoryRows.Sum(category => category.RevenueGenerated);
            foreach (var category in categoryRows)
            {
                category.RevenueShare = totalRevenue > 0
                    ? decimal.Round((category.RevenueGenerated / totalRevenue) * 100m, 2)
                    : 0m;
            }

            return categoryRows;
        }

        private async Task<Models.SellerProfile?> ResolveCurrentSellerAsync(CancellationToken cancellationToken)
        {
            var sellerId = HttpContext.Session.GetInt32("SellerId");
            if (sellerId.HasValue)
            {
                var sellerById = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.SellerId == sellerId.Value, cancellationToken);

                if (sellerById != null)
                {
                    return sellerById;
                }
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var sellerByUser = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == userId.Value, cancellationToken);

                if (sellerByUser != null)
                {
                    HttpContext.Session.SetInt32("SellerId", sellerByUser.SellerId);
                    HttpContext.Session.SetString("SellerEmail", sellerByUser.BusinessEmail ?? string.Empty);
                    HttpContext.Session.SetString("SellerName", sellerByUser.BusinessName ?? "Seller");
                    return sellerByUser;
                }
            }

            return null;
        }

        private async Task PopulateDashboardCountsAsync(
            SellerDashboardViewModel model,
            int sellerId,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var orderColumns = await GetColumnsAsync(connection, "Orders", cancellationToken);
                if (orderColumns.Count > 0)
                {
                    var sellerColumn = FindColumn(orderColumns, "seller_id", "SellerId", "SellerID");
                    var statusColumn = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");

                    if (sellerColumn != null && statusColumn != null)
                    {
                        model.PendingOrders = await ExecuteScalarIntAsync(
                            connection,
                            $"SELECT COUNT(*) FROM {Quote("Orders")} WHERE {Quote(sellerColumn)} = @SellerId AND UPPER(COALESCE({Quote(statusColumn)}, '')) IN ('PENDING', 'TO SHIP', 'PROCESSING', 'ORDER PLACED')",
                            sellerId,
                            cancellationToken);
                    }
                }

                model.ReturnRequests = await CountReturnRequestsAsync(connection, sellerId, cancellationToken);
                model.LowStockAlerts = await CountLowStockProductsAsync(connection, sellerId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load seller dashboard counts.");
            }
        }

        private async Task<List<Order>> LoadRecentOrdersAsync(int sellerId, CancellationToken cancellationToken)
        {
            var orders = new List<Order>();

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var columns = await GetColumnsAsync(connection, "Orders", cancellationToken);
                if (columns.Count == 0)
                {
                    return orders;
                }

                var sellerColumn = FindColumn(columns, "seller_id", "SellerId", "SellerID");
                var orderIdColumn = FindColumn(columns, "OrderID", "OrderId");
                var dateColumn = FindColumn(columns, "OrderDate", "CreatedAt", "created_at");
                var statusColumn = FindColumn(columns, "Status", "status", "FulfillmentStatus");
                var productColumn = FindColumn(columns, "ProductName", "Product", "product_name");
                var fullNameColumn = FindColumn(columns, "FullName", "full_name");
                var totalColumn = FindColumn(columns, "TotalAmount", "Total", "Subtotal");
                var courierColumn = FindColumn(columns, "Courier", "DeliveryOption");

                if (sellerColumn == null || orderIdColumn == null)
                {
                    return orders;
                }

                var selectParts = new[]
                {
                    $"{Quote(orderIdColumn)} AS OrderID",
                    dateColumn == null ? "GETDATE() AS OrderDate" : $"{Quote(dateColumn)} AS OrderDate",
                    statusColumn == null ? "'' AS Status" : $"{Quote(statusColumn)} AS Status",
                    productColumn == null ? "'' AS ProductName" : $"{Quote(productColumn)} AS ProductName",
                    fullNameColumn == null ? "'' AS FullName" : $"{Quote(fullNameColumn)} AS FullName",
                    totalColumn == null ? "0 AS Amount" : $"{Quote(totalColumn)} AS Amount",
                    courierColumn == null ? "'' AS Courier" : $"{Quote(courierColumn)} AS Courier"
                };

                var orderBy = dateColumn == null ? Quote(orderIdColumn) : Quote(dateColumn);
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT TOP 10 {string.Join(", ", selectParts)} FROM {Quote("Orders")} WHERE {Quote(sellerColumn)} = @SellerId ORDER BY {orderBy} DESC";
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var status = GetString(reader, "Status");
                    orders.Add(new Order
                    {
                        OrderID = GetInt(reader, "OrderID"),
                        OrderDate = GetDate(reader, "OrderDate"),
                        Status = status,
                        EffectiveStatus = status,
                        ProductName = GetString(reader, "ProductName"),
                        FullName = GetString(reader, "FullName"),
                        Amount = GetDecimal(reader, "Amount"),
                        TotalAmount = GetDecimal(reader, "Amount"),
                        Courier = GetString(reader, "Courier")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load seller recent orders.");
            }

            return orders;
        }

        private async Task<List<Order>> LoadOrdersAsync(
            int sellerId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken)
        {
            var orders = new List<Order>();

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var columns = await GetColumnsAsync(connection, "Orders", cancellationToken);
                if (columns.Count == 0)
                {
                    return orders;
                }

                var sellerColumn = FindColumn(columns, "seller_id", "SellerId", "SellerID");
                var orderIdColumn = FindColumn(columns, "OrderID", "OrderId");
                if (sellerColumn == null || orderIdColumn == null)
                {
                    return orders;
                }

                var dateColumn = FindColumn(columns, "OrderDate", "CreatedAt", "created_at");
                var statusColumn = FindColumn(columns, "Status", "status", "FulfillmentStatus");
                var productColumn = FindColumn(columns, "ProductName", "Product", "product_name");
                var fullNameColumn = FindColumn(columns, "FullName", "full_name");
                var quantityColumn = FindColumn(columns, "Quantity", "quantity");
                var subtotalColumn = FindColumn(columns, "Subtotal", "subtotal", "TotalAmount", "Total");
                var shippingColumn = FindColumn(columns, "ShippingFee", "shipping_fee");
                var courierColumn = FindColumn(columns, "Courier", "DeliveryOption");
                var consumerColumn = FindColumn(columns, "ConsumerID", "ConsumerId", "consumer_id");
                var paymentColumn = FindColumn(columns, "PaymentMethod", "payment_method");
                var emailColumn = FindColumn(columns, "Email", "email");
                var phoneColumn = FindColumn(columns, "PhoneNumber", "phone_number", "Phone");
                var addressColumn = FindColumn(columns, "StreetAddress", "Address", "address");
                var cityColumn = FindColumn(columns, "City", "city");
                var postalColumn = FindColumn(columns, "PostalCode", "postal_code");
                var trackingColumn = FindColumn(columns, "TrackingNumber", "tracking_number");
                var noteColumn = FindColumn(columns, "SellerNote", "seller_note");
                var logisticsColumn = FindColumn(columns, "logistics_id", "LogisticsId", "CourierId");

                static string SelectOrDefault(string? column, string alias, string defaultSql)
                    => column == null ? $"{defaultSql} AS {Quote(alias)}" : $"{Quote(column)} AS {Quote(alias)}";

                var selectParts = new[]
                {
                    $"{Quote(orderIdColumn)} AS OrderID",
                    SelectOrDefault(dateColumn, "OrderDate", "GETDATE()"),
                    SelectOrDefault(statusColumn, "Status", "''"),
                    SelectOrDefault(productColumn, "ProductName", "''"),
                    SelectOrDefault(fullNameColumn, "FullName", "''"),
                    SelectOrDefault(quantityColumn, "Quantity", "1"),
                    SelectOrDefault(subtotalColumn, "Subtotal", "0"),
                    SelectOrDefault(shippingColumn, "ShippingFee", "0"),
                    SelectOrDefault(courierColumn, "Courier", "''"),
                    SelectOrDefault(consumerColumn, "ConsumerID", "0"),
                    SelectOrDefault(paymentColumn, "PaymentMethod", "''"),
                    SelectOrDefault(emailColumn, "Email", "''"),
                    SelectOrDefault(phoneColumn, "PhoneNumber", "''"),
                    SelectOrDefault(addressColumn, "StreetAddress", "''"),
                    SelectOrDefault(cityColumn, "City", "''"),
                    SelectOrDefault(postalColumn, "PostalCode", "''"),
                    SelectOrDefault(trackingColumn, "TrackingNumber", "''"),
                    SelectOrDefault(noteColumn, "SellerNote", "''"),
                    SelectOrDefault(logisticsColumn, "logistics_id", "NULL")
                };

                var whereParts = new List<string> { $"{Quote(sellerColumn)} = @SellerId" };
                if (dateColumn != null && startDate.HasValue)
                {
                    whereParts.Add($"{Quote(dateColumn)} >= @StartDate");
                }

                if (dateColumn != null && endDate.HasValue)
                {
                    whereParts.Add($"{Quote(dateColumn)} < @EndDateExclusive");
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT {string.Join(", ", selectParts)} FROM {Quote("Orders")} WHERE {string.Join(" AND ", whereParts)} ORDER BY {(dateColumn == null ? Quote(orderIdColumn) : Quote(dateColumn))} DESC";
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });
                if (startDate.HasValue)
                {
                    command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = startDate.Value });
                }

                if (endDate.HasValue)
                {
                    command.Parameters.Add(new SqlParameter("@EndDateExclusive", SqlDbType.DateTime2) { Value = endDate.Value.AddDays(1) });
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var status = GetString(reader, "Status");
                    var productName = GetString(reader, "ProductName");
                    var subtotal = GetDecimal(reader, "Subtotal");
                    var shipping = GetDecimal(reader, "ShippingFee");
                    var quantity = Math.Max(1, GetInt(reader, "Quantity"));

                    orders.Add(new Order
                    {
                        OrderID = GetInt(reader, "OrderID"),
                        OrderDate = GetDate(reader, "OrderDate"),
                        Status = status,
                        EffectiveStatus = status,
                        ProductName = productName,
                        FullName = GetString(reader, "FullName"),
                        Quantity = quantity,
                        Subtotal = subtotal,
                        ShippingFee = shipping,
                        Amount = subtotal + shipping,
                        TotalAmount = subtotal + shipping,
                        Courier = GetString(reader, "Courier"),
                        DeliveryOption = GetString(reader, "Courier"),
                        ConsumerID = GetNullableInt(reader, "ConsumerID"),
                        PaymentMethod = GetString(reader, "PaymentMethod"),
                        Email = GetString(reader, "Email"),
                        PhoneNumber = GetString(reader, "PhoneNumber"),
                        StreetAddress = GetString(reader, "StreetAddress"),
                        City = GetString(reader, "City"),
                        PostalCode = GetString(reader, "PostalCode"),
                        TrackingNumber = GetString(reader, "TrackingNumber"),
                        SellerNote = GetString(reader, "SellerNote"),
                        logistics_id = GetNullableInt(reader, "logistics_id"),
                        ProductImage = string.Empty,
                        OrderItems = new List<OrderItem>
                        {
                            new()
                            {
                                Quantity = quantity,
                                UnitPrice = quantity == 0 ? subtotal : subtotal / quantity,
                                Product = new ProductSummary
                                {
                                    ProductName = productName,
                                    Category = string.Empty
                                }
                            }
                        }
                    });
                }

                // Attempt to load detailed order items (including category) if an OrderItems table exists.
                if (orders.Count > 0)
                {
                    var orderIds = string.Join(",", orders.Select(o => o.OrderID));
                    var orderItemsTable = "OrderItems";
                    var orderItemColumns = await GetColumnsAsync(connection, orderItemsTable, cancellationToken);
                    if (orderItemColumns.Count == 0)
                    {
                        orderItemsTable = "Order_Items";
                        orderItemColumns = await GetColumnsAsync(connection, orderItemsTable, cancellationToken);
                    }

                    if (orderItemColumns.Count > 0)
                    {
                        var oiOrderIdCol = FindColumn(orderItemColumns, "OrderId", "order_id", "OrderID");
                        var oiProductNameCol = FindColumn(orderItemColumns, "ProductName", "product_name", "Product");
                        var oiProductIdCol = FindColumn(orderItemColumns, "ProductId", "ProductID", "product_id");
                        var oiCategoryCol = FindColumn(orderItemColumns, "Category", "category");
                        var oiQuantityCol = FindColumn(orderItemColumns, "Quantity", "quantity");
                        var oiUnitPriceCol = FindColumn(orderItemColumns, "UnitPrice", "unit_price", "Price");

                        var productColumns = await GetColumnsAsync(connection, "Products", cancellationToken);
                        var pProductIdCol = FindColumn(productColumns, "ProductId", "ProductID", "Id");
                        var pNameCol = FindColumn(productColumns, "ProductName", "Name", "product_name", "name");
                        var pCategoryCol = FindColumn(productColumns, "Category", "category", "ProductCategory");

                        // Build select list
                        var selectList = new List<string>();
                        selectList.Add(oiOrderIdCol != null ? Quote(oiOrderIdCol) + " AS OrderID" : "0 AS OrderID");
                        if (oiProductNameCol != null) selectList.Add(Quote(oiProductNameCol) + " AS ProductName");
                        if (oiCategoryCol != null) selectList.Add(Quote(oiCategoryCol) + " AS Category");
                        if (oiQuantityCol != null) selectList.Add(Quote(oiQuantityCol) + " AS Quantity");
                        if (oiUnitPriceCol != null) selectList.Add(Quote(oiUnitPriceCol) + " AS UnitPrice");

                        string sql;
                        if (oiProductIdCol != null && pProductIdCol != null && pCategoryCol != null)
                        {
                            // Join with Products to get category or name when missing on order item
                            var productNameSelect = oiProductNameCol != null ? Quote(oiProductNameCol) : (pNameCol != null ? $"p.{Quote(pNameCol)} AS ProductName" : "'' AS ProductName");
                            var categorySelect = oiCategoryCol != null ? Quote(oiCategoryCol) : $"p.{Quote(pCategoryCol)} AS Category";
                            var qtySelect = oiQuantityCol != null ? Quote(oiQuantityCol) : "1 AS Quantity";
                            var priceSelect = oiUnitPriceCol != null ? Quote(oiUnitPriceCol) : "0 AS UnitPrice";

                            sql = $"SELECT {Quote(oiOrderIdCol)} AS OrderID, COALESCE({Quote(oiProductNameCol)}, p.{Quote(pNameCol)}) AS ProductName, COALESCE({(oiCategoryCol != null ? Quote(oiCategoryCol) : "NULL")}, p.{Quote(pCategoryCol)}) AS Category, {qtySelect} AS Quantity, {priceSelect} AS UnitPrice FROM {Quote(orderItemsTable)} oi LEFT JOIN {Quote("Products")} p ON p.{Quote(pProductIdCol)} = oi.{Quote(oiProductIdCol)} WHERE oi.{Quote(oiOrderIdCol)} IN ({orderIds})";
                        }
                        else
                        {
                            // Fallback: select whatever columns exist on order items
                            sql = $"SELECT {string.Join(", ", selectList)} FROM {Quote(orderItemsTable)} WHERE {Quote(oiOrderIdCol)} IN ({orderIds})";
                        }

                        try
                        {
                            await using var oiCmd = connection.CreateCommand();
                            oiCmd.CommandText = sql;
                            await using var oiReader = await oiCmd.ExecuteReaderAsync(cancellationToken);
                            var itemsByOrder = new Dictionary<int, List<OrderItem>>();
                            while (await oiReader.ReadAsync(cancellationToken))
                            {
                                var oid = GetInt(oiReader, "OrderID");
                                var pname = GetString(oiReader, "ProductName");
                                var cat = GetString(oiReader, "Category");
                                var qty = HasColumn(oiReader, "Quantity") ? GetInt(oiReader, "Quantity") : 1;
                                var unit = HasColumn(oiReader, "UnitPrice") ? GetDecimal(oiReader, "UnitPrice") : 0m;

                                if (!itemsByOrder.TryGetValue(oid, out var list))
                                {
                                    list = new List<OrderItem>();
                                    itemsByOrder[oid] = list;
                                }

                                list.Add(new OrderItem
                                {
                                    Quantity = qty,
                                    UnitPrice = unit,
                                    Product = new ProductSummary
                                    {
                                        ProductName = pname,
                                        Category = cat ?? string.Empty
                                    }
                                });
                            }

                            // Replace placeholder OrderItems with fetched items where available
                            foreach (var order in orders)
                            {
                                if (itemsByOrder.TryGetValue(order.OrderID, out var fetched))
                                {
                                    order.OrderItems = fetched;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Unable to load order items for orders: {OrderIds}", orderIds);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load seller order management list.");
                TempData["ErrorMessage"] = "Order data is temporarily unavailable.";
            }

            return orders;
        }

        private async Task<List<Logistics>> LoadCouriersAsync(CancellationToken cancellationToken)
        {
            var couriers = new List<Logistics>();
            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                var columns = await GetColumnsAsync(connection, "Logistics", cancellationToken);
                var idColumn = FindColumn(columns, "logistics_id", "LogisticsId", "Id");
                var nameColumn = FindColumn(columns, "courier_name", "CourierName", "Name");
                if (idColumn == null || nameColumn == null)
                {
                    return couriers;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT {Quote(idColumn)} AS logistics_id, {Quote(nameColumn)} AS courier_name FROM {Quote("Logistics")} ORDER BY {Quote(nameColumn)}";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    couriers.Add(new Logistics
                    {
                        logistics_id = GetInt(reader, "logistics_id"),
                        courier_name = GetString(reader, "courier_name")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load logistics list.");
            }

            return couriers;
        }

        private async Task<List<ReturnRequest>> LoadReturnRequestsAsync(
            int sellerId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken)
        {
            var returns = new List<ReturnRequest>();
            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                var tableName = "returns";
                var columns = await GetColumnsAsync(connection, tableName, cancellationToken);
                if (columns.Count == 0)
                {
                    tableName = "Returns";
                    columns = await GetColumnsAsync(connection, tableName, cancellationToken);
                }

                var idColumn = FindColumn(columns, "ReturnId", "return_id", "Id");
                var orderColumn = FindColumn(columns, "OrderId", "order_id", "OrderID");
                var userColumn = FindColumn(columns, "UserId", "user_id");
                var sellerColumn = FindColumn(columns, "SellerId", "seller_id", "SellerID");
                var reasonColumn = FindColumn(columns, "Reason", "reason");
                var messageColumn = FindColumn(columns, "Message", "message");
                var statusColumn = FindColumn(columns, "Status", "status");
                var createdColumn = FindColumn(columns, "CreatedAt", "created_at");
                var decisionReasonColumn = FindColumn(columns, "SellerDecisionReason");
                var decisionNoteColumn = FindColumn(columns, "SellerDecisionNote");
                var reviewedAtColumn = FindColumn(columns, "ReviewedAt");

                if (idColumn == null || orderColumn == null || sellerColumn == null)
                {
                    return returns;
                }

                static string SelectOrDefault(string? column, string alias, string defaultSql)
                    => column == null ? $"{defaultSql} AS {Quote(alias)}" : $"{Quote(column)} AS {Quote(alias)}";

                var whereParts = new List<string> { $"{Quote(sellerColumn)} = @SellerId" };
                if (createdColumn != null && startDate.HasValue)
                {
                    whereParts.Add($"{Quote(createdColumn)} >= @StartDate");
                }

                if (createdColumn != null && endDate.HasValue)
                {
                    whereParts.Add($"{Quote(createdColumn)} < @EndDateExclusive");
                }

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT " + string.Join(", ", new[]
                {
                    $"{Quote(idColumn)} AS ReturnId",
                    $"{Quote(orderColumn)} AS OrderId",
                    SelectOrDefault(userColumn, "UserId", "0"),
                    $"{Quote(sellerColumn)} AS SellerId",
                    SelectOrDefault(reasonColumn, "Reason", "''"),
                    SelectOrDefault(messageColumn, "Message", "''"),
                    SelectOrDefault(statusColumn, "Status", "''"),
                    SelectOrDefault(createdColumn, "CreatedAt", "GETDATE()"),
                    SelectOrDefault(decisionReasonColumn, "SellerDecisionReason", "''"),
                    SelectOrDefault(decisionNoteColumn, "SellerDecisionNote", "''"),
                    SelectOrDefault(reviewedAtColumn, "ReviewedAt", "NULL")
                }) + $" FROM {Quote(tableName)} WHERE {string.Join(" AND ", whereParts)} ORDER BY {Quote(createdColumn ?? idColumn)} DESC";
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });
                if (startDate.HasValue)
                {
                    command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = startDate.Value });
                }

                if (endDate.HasValue)
                {
                    command.Parameters.Add(new SqlParameter("@EndDateExclusive", SqlDbType.DateTime2) { Value = endDate.Value.AddDays(1) });
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    returns.Add(new ReturnRequest
                    {
                        ReturnId = GetInt(reader, "ReturnId"),
                        OrderId = GetInt(reader, "OrderId"),
                        UserId = GetInt(reader, "UserId"),
                        SellerId = GetInt(reader, "SellerId"),
                        Reason = GetString(reader, "Reason"),
                        Message = GetString(reader, "Message"),
                        Status = GetString(reader, "Status"),
                        CreatedAt = GetDate(reader, "CreatedAt"),
                        SellerDecisionReason = GetString(reader, "SellerDecisionReason"),
                        SellerDecisionNote = GetString(reader, "SellerDecisionNote"),
                        ReviewedAt = GetNullableDate(reader, "ReviewedAt"),
                        BuyerName = "Buyer"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load seller return requests.");
            }

            return returns;
        }

        private static async Task<Dictionary<string, string>> GetColumnsAsync(
            SqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName";
            command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var column = reader.GetString(0);
                columns[column] = column;
            }

            return columns;
        }

        private static string? FindColumn(Dictionary<string, string> columns, params string[] names)
        {
            foreach (var name in names)
            {
                if (columns.TryGetValue(name, out var actualName))
                {
                    return actualName;
                }
            }

            return null;
        }

        private static async Task<int> CountReturnRequestsAsync(SqlConnection connection, int sellerId, CancellationToken cancellationToken)
        {
            var columns = await GetColumnsAsync(connection, "returns", cancellationToken);
            if (columns.Count == 0)
            {
                columns = await GetColumnsAsync(connection, "Returns", cancellationToken);
            }

            var sellerColumn = FindColumn(columns, "SellerId", "seller_id", "SellerID");
            var statusColumn = FindColumn(columns, "Status", "status");
            if (sellerColumn == null)
            {
                return 0;
            }

            var statusFilter = statusColumn == null
                ? string.Empty
                : $" AND UPPER(COALESCE({Quote(statusColumn)}, '')) NOT IN ('COMPLETED', 'RESOLVED', 'REJECTED', 'CANCELLED')";

            return await ExecuteScalarIntAsync(
                connection,
                $"SELECT COUNT(*) FROM {Quote("returns")} WHERE {Quote(sellerColumn)} = @SellerId{statusFilter}",
                sellerId,
                cancellationToken);
        }

        private static async Task<int> CountLowStockProductsAsync(SqlConnection connection, int sellerId, CancellationToken cancellationToken)
        {
            var productColumns = await GetColumnsAsync(connection, "Products", cancellationToken);
            var variantColumns = await GetColumnsAsync(connection, "ProductVariants", cancellationToken);
            var sellerColumn = FindColumn(productColumns, "seller_id", "SellerId", "SellerID");
            var productIdColumn = FindColumn(productColumns, "ProductId", "ProductID");
            var variantProductIdColumn = FindColumn(variantColumns, "ProductId", "ProductID");
            var quantityColumn = FindColumn(variantColumns, "Quantity", "Stock");

            if (sellerColumn == null || productIdColumn == null || variantProductIdColumn == null || quantityColumn == null)
            {
                return 0;
            }

            return await ExecuteScalarIntAsync(
                connection,
                $"SELECT COUNT(DISTINCT p.{Quote(productIdColumn)}) FROM {Quote("Products")} p INNER JOIN {Quote("ProductVariants")} v ON v.{Quote(variantProductIdColumn)} = p.{Quote(productIdColumn)} WHERE p.{Quote(sellerColumn)} = @SellerId AND v.{Quote(quantityColumn)} <= 5",
                sellerId,
                cancellationToken);
        }

        private static async Task<int> ExecuteScalarIntAsync(
            SqlConnection connection,
            string sql,
            int sellerId,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static string Quote(string identifier)
        {
            return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
        }

        private static bool IsSqlAvailabilityException(Exception ex)
        {
            if (ex is SqlException || ex is TimeoutException || ex is InvalidOperationException)
            {
                return true;
            }

            return ex.InnerException != null && IsSqlAvailabilityException(ex.InnerException);
        }

        private static bool HasColumn(SqlDataReader reader, string name)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadString(SqlDataReader reader, string name, string fallback = "")
        {
            if (!HasColumn(reader, name))
            {
                return fallback;
            }

            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal)) ?? fallback;
        }

        private static int ReadInt(SqlDataReader reader, string name, int fallback = 0)
        {
            if (!HasColumn(reader, name))
            {
                return fallback;
            }

            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? fallback : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static decimal ReadDecimal(SqlDataReader reader, string name, decimal fallback = 0m)
        {
            if (!HasColumn(reader, name))
            {
                return fallback;
            }

            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? fallback : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static DateTime ReadDate(SqlDataReader reader, string name, DateTime fallback)
        {
            if (!HasColumn(reader, name))
            {
                return fallback;
            }

            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? fallback : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static string GetString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static int GetInt(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static decimal GetDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static DateTime GetDate(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? DateTime.Now : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static int? GetNullableInt(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static DateTime? GetNullableDate(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        public sealed class OrderNoteRequest
        {
            public int OrderId { get; set; }
            public string Note { get; set; } = string.Empty;
        }

        public sealed class AcceptOrderRequest
        {
            public int OrderId { get; set; }
            public int Courier { get; set; }
        }

        public sealed class DeclineRequest
        {
            public int OrderId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public sealed class MarkShippedRequest
        {
            public int OrderId { get; set; }
            public string TrackingNumber { get; set; } = string.Empty;
            public IFormFile? ProofOfShipment { get; set; }
        }

        public sealed class MarkReturnedRequest
        {
            public int OrderId { get; set; }
            public string ReturnReason { get; set; } = string.Empty;
            public string? ReturnNote { get; set; }
            public IFormFile? ReturnProof { get; set; }
        }

        public sealed class ReviewReturnRequestModel
        {
            public int ReturnId { get; set; }
            public string Decision { get; set; } = string.Empty;
            public string? RejectionReason { get; set; }
            public string? RejectionNote { get; set; }
        }

        public sealed class ReturnRequestAction
        {
            public int ReturnId { get; set; }
            public bool RestoreStock { get; set; }
        }
    }
}
