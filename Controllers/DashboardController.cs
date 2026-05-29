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
            await SyncCompletedReplacementReturnsAsync(seller.SellerId, cancellationToken);
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

        [HttpGet]
        public async Task<IActionResult> OrderDetails(string id, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(id) ||
                !int.TryParse(id.Replace("ORD-", string.Empty, StringComparison.OrdinalIgnoreCase), out var orderId))
            {
                return BadRequest("Invalid Order ID");
            }

            var order = (await LoadOrdersAsync(seller.SellerId, null, null, cancellationToken))
                .FirstOrDefault(item => item.OrderID == orderId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.logistics_id.HasValue && order.logistics_id.Value > 0)
            {
                var courier = (await LoadCouriersAsync(cancellationToken))
                    .FirstOrDefault(item => item.logistics_id == order.logistics_id.Value);

                order.Courier = string.IsNullOrWhiteSpace(courier?.courier_name)
                    ? "NextHorizon Partner"
                    : courier.courier_name;
            }
            else if (string.IsNullOrWhiteSpace(order.Courier))
            {
                order.Courier = string.IsNullOrWhiteSpace(order.DeliveryOption)
                    ? "NextHorizon Partner"
                    : order.DeliveryOption;
            }

            ViewData["SellerName"] = seller.BusinessName ?? "Seller";
            return View(order);
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
        public async Task<IActionResult> ReviewReturnRequest([FromBody] ReviewReturnRequestModel request, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            if (request.ReturnId <= 0)
            {
                return Json(new { success = false, message = "Invalid return request." });
            }

            var decision = (request.Decision ?? string.Empty).Trim();
            var resolutionType = NormalizeResolutionType(request.ResolutionType);
            var approved = string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase);
            var rejected = string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase);
            if (!approved && !rejected)
            {
                return Json(new { success = false, message = "Choose a valid return decision." });
            }

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var returnTable = await ResolveReturnTableAsync(connection, cancellationToken);
                if (returnTable == null)
                {
                    return Json(new { success = false, message = "Returns table is unavailable." });
                }

                var columns = returnTable.Value.Columns;
                var idColumn = FindColumn(columns, "ReturnId", "return_id", "Id");
                var sellerColumn = FindColumn(columns, "SellerId", "seller_id", "SellerID");
                var statusColumn = FindColumn(columns, "Status", "status");
                if (idColumn == null || sellerColumn == null || statusColumn == null)
                {
                    return Json(new { success = false, message = "Returns table is missing required columns." });
                }

                var decisionReasonColumn = FindColumn(columns, "SellerDecisionReason");
                var decisionNoteColumn = FindColumn(columns, "SellerDecisionNote");
                var reviewedAtColumn = FindColumn(columns, "ReviewedAt");
                var resolutionTypeColumn = FindColumn(columns, "ResolutionType", "resolution_type");

                var assignments = new List<string> { $"{Quote(statusColumn)} = @Status" };
                await using var command = connection.CreateCommand();
                command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = approved ? "Return Approved" : "Return Rejected" });
                command.Parameters.Add(new SqlParameter("@ReturnId", SqlDbType.Int) { Value = request.ReturnId });
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = seller.SellerId });

                if (decisionReasonColumn != null)
                {
                    assignments.Add($"{Quote(decisionReasonColumn)} = @DecisionReason");
                    command.Parameters.Add(new SqlParameter("@DecisionReason", SqlDbType.NVarChar, 150)
                    {
                        Value = rejected ? NullIfWhiteSpace(request.RejectionReason) : DBNull.Value
                    });
                }

                if (decisionNoteColumn != null)
                {
                    assignments.Add($"{Quote(decisionNoteColumn)} = @DecisionNote");
                    command.Parameters.Add(new SqlParameter("@DecisionNote", SqlDbType.NVarChar)
                    {
                        Value = rejected ? NullIfWhiteSpace(request.RejectionNote) : DBNull.Value
                    });
                }

                if (reviewedAtColumn != null)
                {
                    assignments.Add($"{Quote(reviewedAtColumn)} = SYSUTCDATETIME()");
                }

                if (resolutionTypeColumn != null)
                {
                    assignments.Add($"{Quote(resolutionTypeColumn)} = @ResolutionType");
                    command.Parameters.Add(new SqlParameter("@ResolutionType", SqlDbType.NVarChar, 50)
                    {
                        Value = approved ? resolutionType : "Refund"
                    });
                }

                command.CommandText =
                    $"UPDATE {Quote(returnTable.Value.TableName)} SET {string.Join(", ", assignments)} WHERE {Quote(idColumn)} = @ReturnId AND {Quote(sellerColumn)} = @SellerId";

                var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                return Json(new
                {
                    success = affected > 0,
                    message = affected > 0
                        ? approved && resolutionType == "Replacement"
                            ? "Return approved for replacement."
                            : approved
                                ? "Return approved for refund."
                                : "Return request rejected."
                        : "Return request not found."
                });
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to review return request {ReturnId}.", request.ReturnId);
                return Json(new { success = false, message = "Return request could not be updated right now." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkReturnItemReceived([FromBody] ReturnRequestAction request, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            var updated = await UpdateReturnStatusAsync(request.ReturnId, seller.SellerId, "Item Returned", cancellationToken);
            return Json(new
            {
                success = updated,
                message = updated ? "Return item marked as received." : "Return request not found."
            });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmReturnRefund([FromBody] ReturnRequestAction request, CancellationToken cancellationToken)
        {
            var seller = await ResolveCurrentSellerAsync(cancellationToken);
            if (seller == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var returnRequest = await LoadReturnRequestForSellerAsync(connection, request.ReturnId, seller.SellerId, cancellationToken);
                if (returnRequest == null)
                {
                    return Json(new { success = false, message = "Return request not found." });
                }

                if (string.Equals(returnRequest.ResolutionType, "Replacement", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(returnRequest.Status, "Item Returned", StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Mark the returned item as received before creating a replacement order."
                        });
                    }

                    if (returnRequest.ReplacementOrderId.HasValue && returnRequest.ReplacementOrderId.Value > 0)
                    {
                        return Json(new
                        {
                            success = true,
                            message = $"Replacement order #{returnRequest.ReplacementOrderId.Value} is already in To Ship."
                        });
                    }

                    var replacementOrderId = await CreateReplacementOrderAsync(connection, returnRequest.OrderId, seller.SellerId, seller.UserId, request.ReturnId, cancellationToken);
                    return Json(new
                    {
                        success = true,
                        message = $"Replacement order #{replacementOrderId} was created and moved to To Ship."
                    });
                }

                var updated = await UpdateReturnStatusAsync(request.ReturnId, seller.SellerId, "Refunded", cancellationToken);
                return Json(new
                {
                    success = updated,
                    message = updated ? "Refund confirmed." : "Return request not found."
                });
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to complete return request {ReturnId}.", request.ReturnId);
                return Json(new { success = false, message = "Return request could not be completed right now." });
            }
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
            var codOrders = nonCancelledOrders
                .Where(order => IsCodPayment(order.PaymentMethod))
                .ToList();
            var totalCodOrders = codOrders.Count;
            var deliveredCodOrders = codOrders.Count(order => IsOrderStatus(order, "Delivered", "Completed", "Complete"));
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
                CodDeliveredRevenue = codOrders
                    .Where(order => IsOrderStatus(order, "Delivered", "Completed", "Complete"))
                    .Sum(GetOrderAnalyticsAmount),
                CodExposure = codOrders
                    .Where(order => IsOrderStatus(order, "To Ship", "Shipped"))
                    .Sum(GetOrderAnalyticsAmount),
                CodSuccessRate = totalCodOrders > 0
                    ? decimal.Round((deliveredCodOrders / (decimal)totalCodOrders) * 100m, 2)
                    : 0m,
                CodRtsCount = codOrders.Count(order => IsOrderStatus(order, "Failed Delivery")),
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
            var status = NormalizeOrderStatus(order.EffectiveStatus ?? order.Status);
            return statuses.Any(item => string.Equals(status, item, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCodPayment(string? paymentMethod)
        {
            var value = paymentMethod?.Trim();
            return string.Equals(value, "COD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Cash on Delivery", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeOrderStatus(string? status)
        {
            return (status ?? string.Empty).Trim() switch
            {
                "Placed" => "Pending",
                var value => value
            };
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
                            $"SELECT COUNT(*) FROM {Quote("Orders")} WHERE {Quote(sellerColumn)} = @SellerId AND UPPER(COALESCE({Quote(statusColumn)}, '')) IN ('PENDING', 'PLACED', 'TO SHIP', 'PROCESSING', 'ORDER PLACED')",
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
                    var status = NormalizeOrderStatus(GetString(reader, "Status"));
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

                {
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var status = NormalizeOrderStatus(GetString(reader, "Status"));
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
                        var oiVariantIdCol = FindColumn(orderItemColumns, "VariantId", "VariantID", "variant_id");
                        var oiCategoryCol = FindColumn(orderItemColumns, "Category", "category");
                        var oiQuantityCol = FindColumn(orderItemColumns, "Quantity", "quantity");
                        var oiUnitPriceCol = FindColumn(orderItemColumns, "UnitPrice", "unit_price", "Price");
                        var oiProductImageCol = FindColumn(orderItemColumns, "ProductImage", "ProductImagePath", "ImagePath");

                        var productColumns = await GetColumnsAsync(connection, "Products", cancellationToken);
                        var pProductIdCol = FindColumn(productColumns, "ProductId", "ProductID", "Id");
                        var pNameCol = FindColumn(productColumns, "ProductName", "Name", "product_name", "name");
                        var pCategoryCol = FindColumn(productColumns, "Category", "category", "ProductCategory");
                        var pImagePathCol = FindColumn(productColumns, "ImagePath", "ProductImage", "image_path");

                        var variantColumns = await GetColumnsAsync(connection, "ProductVariants", cancellationToken);
                        var vVariantIdCol = FindColumn(variantColumns, "VariantId", "VariantID", "Id");
                        var vProductIdCol = FindColumn(variantColumns, "ProductId", "ProductID", "product_id");
                        var vImagePathCol = FindColumn(variantColumns, "ImagePath", "ProductImage", "image_path");

                        // Build select list
                        if (oiOrderIdCol == null)
                        {
                            return orders;
                        }

                        var selectList = new List<string>
                        {
                            $"{Quote(oiOrderIdCol)} AS OrderID"
                        };
                        if (oiProductNameCol != null) selectList.Add(Quote(oiProductNameCol) + " AS ProductName");
                        if (oiCategoryCol != null) selectList.Add(Quote(oiCategoryCol) + " AS Category");
                        if (oiQuantityCol != null) selectList.Add(Quote(oiQuantityCol) + " AS Quantity");
                        if (oiUnitPriceCol != null) selectList.Add(Quote(oiUnitPriceCol) + " AS UnitPrice");
                        if (oiProductImageCol != null) selectList.Add(Quote(oiProductImageCol) + " AS ProductImage");

                        string sql;
                        if (oiProductIdCol != null && pProductIdCol != null && pCategoryCol != null)
                        {
                            var variantJoin = string.Empty;
                            if (vProductIdCol != null && vImagePathCol != null)
                            {
                                var variantJoinConditions = new List<string>();
                                if (oiVariantIdCol != null && vVariantIdCol != null)
                                {
                                    variantJoinConditions.Add($"(oi.{Quote(oiVariantIdCol)} IS NOT NULL AND pv.{Quote(vVariantIdCol)} = oi.{Quote(oiVariantIdCol)})");
                                    variantJoinConditions.Add($"(oi.{Quote(oiVariantIdCol)} IS NULL AND pv.{Quote(vProductIdCol)} = oi.{Quote(oiProductIdCol)})");
                                }
                                else
                                {
                                    variantJoinConditions.Add($"pv.{Quote(vProductIdCol)} = oi.{Quote(oiProductIdCol)}");
                                }
                                variantJoin = $" LEFT JOIN {Quote("ProductVariants")} pv ON {string.Join(" OR ", variantJoinConditions)}";
                            }

                            var productNameExpr = (oiProductNameCol, pNameCol) switch
                            {
                                ({ } oiName, { } pName) => $"COALESCE(NULLIF(oi.{Quote(oiName)}, ''), p.{Quote(pName)})",
                                ({ } oiName, null) => $"oi.{Quote(oiName)}",
                                (null, { } pName) => $"p.{Quote(pName)}",
                                _ => "''"
                            };
                            var categoryExpr = oiCategoryCol != null
                                ? $"COALESCE(NULLIF(oi.{Quote(oiCategoryCol)}, ''), p.{Quote(pCategoryCol)})"
                                : $"p.{Quote(pCategoryCol)}";
                            var qtySelect = oiQuantityCol != null ? $"oi.{Quote(oiQuantityCol)}" : "1";
                            var priceSelect = oiUnitPriceCol != null ? $"oi.{Quote(oiUnitPriceCol)}" : "0";
                            var imageCandidates = new List<string>();
                            if (oiProductImageCol != null) imageCandidates.Add($"NULLIF(oi.{Quote(oiProductImageCol)}, '')");
                            if (!string.IsNullOrWhiteSpace(variantJoin) && vImagePathCol != null) imageCandidates.Add($"NULLIF(pv.{Quote(vImagePathCol)}, '')");
                            if (pImagePathCol != null) imageCandidates.Add($"NULLIF(p.{Quote(pImagePathCol)}, '')");
                            var imageExpr = imageCandidates.Count == 0
                                ? "''"
                                : $"COALESCE({string.Join(", ", imageCandidates)}, '')";

                            sql = $"SELECT oi.{Quote(oiOrderIdCol)} AS OrderID, {productNameExpr} AS ProductName, {categoryExpr} AS Category, {qtySelect} AS Quantity, {priceSelect} AS UnitPrice, {imageExpr} AS ProductImage FROM {Quote(orderItemsTable)} oi LEFT JOIN {Quote("Products")} p ON p.{Quote(pProductIdCol)} = oi.{Quote(oiProductIdCol)}{variantJoin} WHERE oi.{Quote(oiOrderIdCol)} IN ({orderIds})";
                        }
                        else
                        {
                            // Fallback: select whatever columns exist on order items
                            if (oiProductNameCol == null) selectList.Add("'' AS ProductName");
                            if (oiCategoryCol == null) selectList.Add("'' AS Category");
                            if (oiQuantityCol == null) selectList.Add("1 AS Quantity");
                            if (oiUnitPriceCol == null) selectList.Add("0 AS UnitPrice");
                            if (oiProductImageCol == null) selectList.Add("'' AS ProductImage");
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
                                var productImage = HasColumn(oiReader, "ProductImage") ? GetString(oiReader, "ProductImage") : string.Empty;

                                if (!itemsByOrder.TryGetValue(oid, out var list))
                                {
                                    list = new List<OrderItem>();
                                    itemsByOrder[oid] = list;
                                }

                                list.Add(new OrderItem
                                {
                                    Quantity = qty,
                                    UnitPrice = unit,
                                    ProductImage = productImage,
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
                                    order.ProductImage = fetched
                                        .Select(item => item.ProductImage)
                                        .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Unable to load order items for orders: {OrderIds}", orderIds);
                        }
                    }
                }

                await EnrichOrderCategoriesByProductNameAsync(connection, sellerId, orders, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load seller order management list.");
                TempData["ErrorMessage"] = "Order data is temporarily unavailable.";
            }

            return orders;
        }

        private async Task EnrichOrderCategoriesByProductNameAsync(
            SqlConnection connection,
            int sellerId,
            List<Order> orders,
            CancellationToken cancellationToken)
        {
            if (orders.Count == 0)
            {
                return;
            }

            try
            {
                var columns = await GetColumnsAsync(connection, "Products", cancellationToken);
                var sellerColumn = FindColumn(columns, "seller_id", "SellerId", "SellerID");
                var nameColumn = FindColumn(columns, "ProductName", "Name", "product_name", "name");
                var categoryColumn = FindColumn(columns, "Category", "category", "ProductCategory");
                if (sellerColumn == null || nameColumn == null || categoryColumn == null)
                {
                    return;
                }

                var categoriesByProductName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT {Quote(nameColumn)} AS ProductName, {Quote(categoryColumn)} AS Category FROM {Quote("Products")} WHERE {Quote(sellerColumn)} = @SellerId AND NULLIF(LTRIM(RTRIM({Quote(categoryColumn)})), '') IS NOT NULL";
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var productName = GetString(reader, "ProductName")?.Trim();
                    var category = GetString(reader, "Category")?.Trim();
                    if (!string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(category))
                    {
                        categoriesByProductName[productName] = category;
                    }
                }

                foreach (var order in orders)
                {
                    foreach (var item in order.OrderItems ?? new List<OrderItem>())
                    {
                        var product = item.Product ??= new ProductSummary();
                        if (!string.IsNullOrWhiteSpace(product.Category))
                        {
                            continue;
                        }

                        var itemProductName = !string.IsNullOrWhiteSpace(product.ProductName)
                            ? product.ProductName.Trim()
                            : order.ProductName?.Trim();

                        if (!string.IsNullOrWhiteSpace(itemProductName) &&
                            categoriesByProductName.TryGetValue(itemProductName, out var category))
                        {
                            product.Category = category;
                        }
                    }
                }
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogDebug(ex, "Unable to enrich order categories for seller {SellerId}", sellerId);
            }
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

        private async Task<bool> UpdateReturnStatusAsync(
            int returnId,
            int sellerId,
            string status,
            CancellationToken cancellationToken)
        {
            if (returnId <= 0)
            {
                return false;
            }

            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var returnTable = await ResolveReturnTableAsync(connection, cancellationToken);
                if (returnTable == null)
                {
                    return false;
                }

                var columns = returnTable.Value.Columns;
                var idColumn = FindColumn(columns, "ReturnId", "return_id", "Id");
                var sellerColumn = FindColumn(columns, "SellerId", "seller_id", "SellerID");
                var statusColumn = FindColumn(columns, "Status", "status");
                if (idColumn == null || sellerColumn == null || statusColumn == null)
                {
                    return false;
                }

                var reviewedAtColumn = FindColumn(columns, "ReviewedAt");
                var assignments = new List<string> { $"{Quote(statusColumn)} = @Status" };
                if (reviewedAtColumn != null)
                {
                    assignments.Add($"{Quote(reviewedAtColumn)} = SYSUTCDATETIME()");
                }

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE {Quote(returnTable.Value.TableName)} SET {string.Join(", ", assignments)} WHERE {Quote(idColumn)} = @ReturnId AND {Quote(sellerColumn)} = @SellerId";
                command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = status });
                command.Parameters.Add(new SqlParameter("@ReturnId", SqlDbType.Int) { Value = returnId });
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to set return request {ReturnId} status to {Status}.", returnId, status);
                return false;
            }
        }

        private async Task SyncCompletedReplacementReturnsAsync(int sellerId, CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var returnTable = await ResolveReturnTableAsync(connection, cancellationToken);
                if (returnTable == null)
                {
                    return;
                }

                var returnColumns = returnTable.Value.Columns;
                var returnSellerColumn = FindColumn(returnColumns, "SellerId", "seller_id", "SellerID");
                var returnStatusColumn = FindColumn(returnColumns, "Status", "status");
                var replacementOrderColumn = FindColumn(returnColumns, "ReplacementOrderId", "replacement_order_id");
                var reviewedAtColumn = FindColumn(returnColumns, "ReviewedAt");
                if (returnSellerColumn == null || returnStatusColumn == null || replacementOrderColumn == null)
                {
                    return;
                }

                var orderColumns = await GetColumnsAsync(connection, "Orders", cancellationToken);
                var orderIdColumn = FindColumn(orderColumns, "OrderID", "OrderId", "id");
                var orderSellerColumn = FindColumn(orderColumns, "SellerId", "seller_id", "SellerID");
                var orderStatusColumn = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");
                if (orderIdColumn == null || orderSellerColumn == null || orderStatusColumn == null)
                {
                    return;
                }

                var assignments = new List<string> { $"r.{Quote(returnStatusColumn)} = @CompletedStatus" };
                if (reviewedAtColumn != null)
                {
                    assignments.Add($"r.{Quote(reviewedAtColumn)} = SYSUTCDATETIME()");
                }

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"""
                    UPDATE r
                    SET {string.Join(", ", assignments)}
                    FROM {Quote(returnTable.Value.TableName)} r
                    INNER JOIN {Quote("Orders")} o
                        ON o.{Quote(orderIdColumn)} = r.{Quote(replacementOrderColumn)}
                        AND o.{Quote(orderSellerColumn)} = r.{Quote(returnSellerColumn)}
                    WHERE r.{Quote(returnSellerColumn)} = @SellerId
                        AND UPPER(LTRIM(RTRIM(COALESCE(r.{Quote(returnStatusColumn)}, '')))) = 'REPLACEMENT CREATED'
                        AND UPPER(LTRIM(RTRIM(COALESCE(o.{Quote(orderStatusColumn)}, '')))) IN ('DELIVERED', 'COMPLETED', 'COMPLETE');
                    """;
                command.Parameters.Add(new SqlParameter("@CompletedStatus", SqlDbType.NVarChar, 50) { Value = "Replacement Completed" });
                command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex) when (IsSqlAvailabilityException(ex))
            {
                _logger.LogWarning(ex, "Unable to sync completed replacement returns for seller {SellerId}.", sellerId);
            }
        }

        private async Task<ReturnRequest?> LoadReturnRequestForSellerAsync(
            SqlConnection connection,
            int returnId,
            int sellerId,
            CancellationToken cancellationToken)
        {
            var returnTable = await ResolveReturnTableAsync(connection, cancellationToken);
            if (returnTable == null)
            {
                return null;
            }

            var columns = returnTable.Value.Columns;
            var idColumn = FindColumn(columns, "ReturnId", "return_id", "Id");
            var orderColumn = FindColumn(columns, "OrderId", "order_id", "OrderID");
            var sellerColumn = FindColumn(columns, "SellerId", "seller_id", "SellerID");
            var statusColumn = FindColumn(columns, "Status", "status");
            var resolutionTypeColumn = FindColumn(columns, "ResolutionType", "resolution_type");
            var replacementOrderColumn = FindColumn(columns, "ReplacementOrderId", "replacement_order_id");
            if (idColumn == null || orderColumn == null || sellerColumn == null)
            {
                return null;
            }

            static string SelectOrDefault(string? column, string alias, string defaultSql)
                => column == null ? $"{defaultSql} AS {Quote(alias)}" : $"{Quote(column)} AS {Quote(alias)}";

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT " + string.Join(", ", new[]
            {
                $"{Quote(idColumn)} AS ReturnId",
                $"{Quote(orderColumn)} AS OrderId",
                $"{Quote(sellerColumn)} AS SellerId",
                SelectOrDefault(statusColumn, "Status", "''"),
                SelectOrDefault(resolutionTypeColumn, "ResolutionType", "'Refund'"),
                SelectOrDefault(replacementOrderColumn, "ReplacementOrderId", "NULL")
            }) + $" FROM {Quote(returnTable.Value.TableName)} WHERE {Quote(idColumn)} = @ReturnId AND {Quote(sellerColumn)} = @SellerId";
            command.Parameters.Add(new SqlParameter("@ReturnId", SqlDbType.Int) { Value = returnId });
            command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ReturnRequest
            {
                ReturnId = GetInt(reader, "ReturnId"),
                OrderId = GetInt(reader, "OrderId"),
                SellerId = GetInt(reader, "SellerId"),
                Status = GetString(reader, "Status"),
                ResolutionType = NormalizeResolutionType(GetString(reader, "ResolutionType")),
                ReplacementOrderId = GetNullableInt(reader, "ReplacementOrderId")
            };
        }

        private async Task<int> CreateReplacementOrderAsync(
            SqlConnection connection,
            int originalOrderId,
            int sellerId,
            int sellerUserId,
            int returnId,
            CancellationToken cancellationToken)
        {
            var orderColumns = await GetColumnsAsync(connection, "Orders", cancellationToken);
            var orderItemColumns = await GetColumnsAsync(connection, "OrderItems", cancellationToken);
            var returnTable = await ResolveReturnTableAsync(connection, cancellationToken);
            if (orderColumns.Count == 0 || orderItemColumns.Count == 0 || returnTable == null)
            {
                throw new InvalidOperationException("Order tables are unavailable.");
            }

            var orderIdColumn = FindColumn(orderColumns, "OrderID", "OrderId");
            var sellerColumn = FindColumn(orderColumns, "seller_id", "SellerId", "SellerID");
            if (orderIdColumn == null || sellerColumn == null)
            {
                throw new InvalidOperationException("Orders table is missing required columns.");
            }

            var orderNumberColumn = FindColumn(orderColumns, "OrderNumber", "OrderNo");
            var userIdColumn = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerColumn = FindColumn(orderColumns, "ConsumerID", "ConsumerId", "consumer_id");
            var fullNameColumn = FindColumn(orderColumns, "FullName", "full_name");
            var emailColumn = FindColumn(orderColumns, "Email", "email");
            var phoneColumn = FindColumn(orderColumns, "PhoneNumber", "phone_number", "Phone");
            var addressColumn = FindColumn(orderColumns, "StreetAddress", "Address", "address");
            var cityColumn = FindColumn(orderColumns, "City", "city");
            var postalColumn = FindColumn(orderColumns, "PostalCode", "postal_code");
            var deliveryColumn = FindColumn(orderColumns, "DeliveryOption", "delivery_option", "Courier");
            var paymentColumn = FindColumn(orderColumns, "PaymentMethod", "payment_method");
            var statusColumn = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");
            var subtotalColumn = FindColumn(orderColumns, "Subtotal", "subtotal");
            var shippingColumn = FindColumn(orderColumns, "ShippingFee", "shipping_fee");
            var totalColumn = FindColumn(orderColumns, "TotalAmount", "total_amount", "Total");
            var dateColumn = FindColumn(orderColumns, "OrderDate", "CreatedAt", "created_at");
            var quantityColumn = FindColumn(orderColumns, "Quantity", "quantity");
            var productColumn = FindColumn(orderColumns, "ProductName", "Product", "product_name");
            var logisticsColumn = FindColumn(orderColumns, "logistics_id", "LogisticsId", "CourierId");

            static string SelectOrDefault(string? column, string alias, string defaultSql)
                => column == null ? $"{defaultSql} AS {Quote(alias)}" : $"{Quote(column)} AS {Quote(alias)}";

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using var originalCommand = connection.CreateCommand();
                originalCommand.Transaction = (SqlTransaction)transaction;
                originalCommand.CommandText = "SELECT " + string.Join(", ", new[]
                {
                    $"{Quote(orderIdColumn)} AS OrderID",
                    SelectOrDefault(orderNumberColumn, "OrderNumber", "''"),
                    SelectOrDefault(userIdColumn, "UserId", "NULL"),
                    SelectOrDefault(consumerColumn, "ConsumerID", "NULL"),
                    $"{Quote(sellerColumn)} AS SellerId",
                    SelectOrDefault(fullNameColumn, "FullName", "''"),
                    SelectOrDefault(emailColumn, "Email", "''"),
                    SelectOrDefault(phoneColumn, "PhoneNumber", "''"),
                    SelectOrDefault(addressColumn, "StreetAddress", "''"),
                    SelectOrDefault(cityColumn, "City", "''"),
                    SelectOrDefault(postalColumn, "PostalCode", "''"),
                    SelectOrDefault(deliveryColumn, "DeliveryOption", "''"),
                    SelectOrDefault(quantityColumn, "Quantity", "1"),
                    SelectOrDefault(productColumn, "ProductName", "''"),
                    SelectOrDefault(logisticsColumn, "logistics_id", "NULL")
                }) + $" FROM {Quote("Orders")} WHERE {Quote(orderIdColumn)} = @OrderId AND {Quote(sellerColumn)} = @SellerId";
                originalCommand.Parameters.Add(new SqlParameter("@OrderId", SqlDbType.Int) { Value = originalOrderId });
                originalCommand.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                int? sourceUserId;
                int? sourceConsumerId;
                int? sourceLogisticsId;
                string sourceOrderNumber;
                string fullName;
                string email;
                string phoneNumber;
                string streetAddress;
                string city;
                string postalCode;
                string deliveryOption;
                int quantity;
                string productName;

                await using (var reader = await originalCommand.ExecuteReaderAsync(cancellationToken))
                {
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        throw new InvalidOperationException("Original order was not found.");
                    }

                    sourceOrderNumber = GetString(reader, "OrderNumber");
                    sourceUserId = GetNullableInt(reader, "UserId");
                    sourceConsumerId = GetNullableInt(reader, "ConsumerID");
                    sourceLogisticsId = GetNullableInt(reader, "logistics_id");
                    fullName = GetString(reader, "FullName");
                    email = GetString(reader, "Email");
                    phoneNumber = GetString(reader, "PhoneNumber");
                    streetAddress = GetString(reader, "StreetAddress");
                    city = GetString(reader, "City");
                    postalCode = GetString(reader, "PostalCode");
                    deliveryOption = GetString(reader, "DeliveryOption");
                    quantity = Math.Max(1, GetInt(reader, "Quantity"));
                    productName = GetString(reader, "ProductName");
                }

                await using var insertOrderCommand = connection.CreateCommand();
                insertOrderCommand.Transaction = (SqlTransaction)transaction;
                var insertColumns = new List<string>();
                var insertValues = new List<string>();

                void AddOrderField(string? columnName, string parameterName, object? value, SqlDbType dbType)
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        return;
                    }

                    insertColumns.Add(Quote(columnName));
                    insertValues.Add(parameterName);
                    insertOrderCommand.Parameters.Add(new SqlParameter(parameterName, dbType) { Value = value ?? DBNull.Value });
                }

                AddOrderField(orderNumberColumn, "@OrderNumber", $"RPL-{originalOrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}", SqlDbType.NVarChar);
                AddOrderField(userIdColumn, "@UserId", sourceUserId, SqlDbType.Int);
                AddOrderField(consumerColumn, "@ConsumerId", sourceConsumerId, SqlDbType.Int);
                AddOrderField(sellerColumn, "@SellerId", sellerId, SqlDbType.Int);
                AddOrderField(fullNameColumn, "@FullName", fullName, SqlDbType.NVarChar);
                AddOrderField(emailColumn, "@Email", email, SqlDbType.NVarChar);
                AddOrderField(phoneColumn, "@PhoneNumber", phoneNumber, SqlDbType.NVarChar);
                AddOrderField(addressColumn, "@StreetAddress", streetAddress, SqlDbType.NVarChar);
                AddOrderField(cityColumn, "@City", city, SqlDbType.NVarChar);
                AddOrderField(postalColumn, "@PostalCode", postalCode, SqlDbType.NVarChar);
                AddOrderField(deliveryColumn, "@DeliveryOption", deliveryOption, SqlDbType.NVarChar);
                AddOrderField(paymentColumn, "@PaymentMethod", "Replacement", SqlDbType.NVarChar);
                AddOrderField(statusColumn, "@Status", "To Ship", SqlDbType.NVarChar);
                AddOrderField(subtotalColumn, "@Subtotal", 0m, SqlDbType.Decimal);
                AddOrderField(shippingColumn, "@ShippingFee", 0m, SqlDbType.Decimal);
                AddOrderField(totalColumn, "@TotalAmount", 0m, SqlDbType.Decimal);
                AddOrderField(dateColumn, "@OrderDate", DateTime.UtcNow, SqlDbType.DateTime2);
                AddOrderField(quantityColumn, "@Quantity", quantity, SqlDbType.Int);
                AddOrderField(productColumn, "@ProductName", productName, SqlDbType.NVarChar);
                AddOrderField(logisticsColumn, "@LogisticsId", sourceLogisticsId, SqlDbType.Int);

                insertOrderCommand.CommandText =
                    $"""
                    INSERT INTO dbo.Orders
                    (
                        {string.Join(", ", insertColumns)}
                    )
                    VALUES
                    (
                        {string.Join(", ", insertValues)}
                    );

                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;

                var replacementOrderId = Convert.ToInt32(await insertOrderCommand.ExecuteScalarAsync(cancellationToken));

                await CopyReplacementOrderItemsAsync(
                    connection,
                    (SqlTransaction)transaction,
                    orderItemColumns,
                    originalOrderId,
                    replacementOrderId,
                    sellerId,
                    cancellationToken);

                await MarkReplacementCreatedAsync(
                    connection,
                    (SqlTransaction)transaction,
                    returnTable.Value.TableName,
                    returnTable.Value.Columns,
                    returnId,
                    sellerId,
                    replacementOrderId,
                    cancellationToken);

                await CreateReplacementBuyerMessageAsync(
                    connection,
                    (SqlTransaction)transaction,
                    sourceUserId,
                    sourceConsumerId,
                    sellerId,
                    sellerUserId,
                    originalOrderId,
                    replacementOrderId,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return replacementOrderId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static async Task CopyReplacementOrderItemsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            Dictionary<string, string> orderItemColumns,
            int originalOrderId,
            int replacementOrderId,
            int sellerId,
            CancellationToken cancellationToken)
        {
            var orderIdColumn = FindColumn(orderItemColumns, "OrderId", "OrderID");
            var productIdColumn = FindColumn(orderItemColumns, "ProductId", "ProductID", "product_id");
            var sellerIdColumn = FindColumn(orderItemColumns, "SellerId", "SellerID", "seller_id");
            var sizeColumn = FindColumn(orderItemColumns, "Size", "size");
            var colorColumn = FindColumn(orderItemColumns, "Color", "color");
            var quantityColumn = FindColumn(orderItemColumns, "Quantity", "quantity");
            var unitPriceColumn = FindColumn(orderItemColumns, "UnitPrice", "unit_price", "Price");
            var productImageColumn = FindColumn(orderItemColumns, "ProductImage", "ProductImagePath", "ImagePath");
            if (orderIdColumn == null)
            {
                throw new InvalidOperationException("OrderItems table is missing its order id column.");
            }

            static string SelectOrDefault(string? column, string alias, string defaultSql)
                => column == null ? $"{defaultSql} AS {Quote(alias)}" : $"{Quote(column)} AS {Quote(alias)}";

            var sourceItems = new List<Dictionary<string, object?>>();
            await using (var selectCommand = connection.CreateCommand())
            {
                selectCommand.Transaction = transaction;
                selectCommand.CommandText = "SELECT " + string.Join(", ", new[]
                {
                    SelectOrDefault(productIdColumn, "ProductId", "0"),
                    SelectOrDefault(sellerIdColumn, "SellerId", "@SellerId"),
                    SelectOrDefault(sizeColumn, "Size", "NULL"),
                    SelectOrDefault(colorColumn, "Color", "NULL"),
                    SelectOrDefault(quantityColumn, "Quantity", "1"),
                    SelectOrDefault(productImageColumn, "ProductImage", "NULL")
                }) + $" FROM {Quote("OrderItems")} WHERE {Quote(orderIdColumn)} = @OriginalOrderId";
                selectCommand.Parameters.Add(new SqlParameter("@OriginalOrderId", SqlDbType.Int) { Value = originalOrderId });
                selectCommand.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    sourceItems.Add(new Dictionary<string, object?>
                    {
                        ["ProductId"] = ReadInt(reader, "ProductId"),
                        ["SellerId"] = ReadInt(reader, "SellerId", sellerId),
                        ["Size"] = ReadString(reader, "Size"),
                        ["Color"] = ReadString(reader, "Color"),
                        ["Quantity"] = Math.Max(1, ReadInt(reader, "Quantity", 1)),
                        ["ProductImage"] = ReadString(reader, "ProductImage")
                    });
                }
            }

            foreach (var item in sourceItems)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                var insertColumns = new List<string>();
                var insertValues = new List<string>();

                void AddItemField(string? columnName, string parameterName, object? value, SqlDbType dbType)
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        return;
                    }

                    insertColumns.Add(Quote(columnName));
                    insertValues.Add(parameterName);
                    insertCommand.Parameters.Add(new SqlParameter(parameterName, dbType) { Value = value ?? DBNull.Value });
                }

                AddItemField(orderIdColumn, "@OrderId", replacementOrderId, SqlDbType.Int);
                AddItemField(productIdColumn, "@ProductId", item["ProductId"], SqlDbType.Int);
                AddItemField(sellerIdColumn, "@SellerId", sellerId, SqlDbType.Int);
                AddItemField(sizeColumn, "@Size", item["Size"], SqlDbType.NVarChar);
                AddItemField(colorColumn, "@Color", item["Color"], SqlDbType.NVarChar);
                AddItemField(quantityColumn, "@Quantity", item["Quantity"], SqlDbType.Int);
                AddItemField(unitPriceColumn, "@UnitPrice", 0m, SqlDbType.Decimal);
                AddItemField(productImageColumn, "@ProductImage", item["ProductImage"], SqlDbType.NVarChar);

                insertCommand.CommandText =
                    $"""
                    INSERT INTO dbo.OrderItems
                    (
                        {string.Join(", ", insertColumns)}
                    )
                    VALUES
                    (
                        {string.Join(", ", insertValues)}
                    );
                    """;

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static async Task MarkReplacementCreatedAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string returnTableName,
            Dictionary<string, string> returnColumns,
            int returnId,
            int sellerId,
            int replacementOrderId,
            CancellationToken cancellationToken)
        {
            var idColumn = FindColumn(returnColumns, "ReturnId", "return_id", "Id");
            var sellerColumn = FindColumn(returnColumns, "SellerId", "seller_id", "SellerID");
            var statusColumn = FindColumn(returnColumns, "Status", "status");
            var replacementOrderColumn = FindColumn(returnColumns, "ReplacementOrderId", "replacement_order_id");
            var reviewedAtColumn = FindColumn(returnColumns, "ReviewedAt");
            if (idColumn == null || sellerColumn == null || statusColumn == null)
            {
                throw new InvalidOperationException("Returns table is missing required columns.");
            }

            var assignments = new List<string> { $"{Quote(statusColumn)} = @Status" };
            if (replacementOrderColumn != null)
            {
                assignments.Add($"{Quote(replacementOrderColumn)} = @ReplacementOrderId");
            }

            if (reviewedAtColumn != null)
            {
                assignments.Add($"{Quote(reviewedAtColumn)} = SYSUTCDATETIME()");
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"UPDATE {Quote(returnTableName)} SET {string.Join(", ", assignments)} WHERE {Quote(idColumn)} = @ReturnId AND {Quote(sellerColumn)} = @SellerId";
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = "Replacement Created" });
            command.Parameters.Add(new SqlParameter("@ReplacementOrderId", SqlDbType.Int) { Value = replacementOrderId });
            command.Parameters.Add(new SqlParameter("@ReturnId", SqlDbType.Int) { Value = returnId });
            command.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task CreateReplacementBuyerMessageAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            int? buyerUserId,
            int? consumerId,
            int sellerId,
            int sellerUserId,
            int originalOrderId,
            int replacementOrderId,
            CancellationToken cancellationToken)
        {
            var resolvedBuyerUserId = buyerUserId.GetValueOrDefault();
            var resolvedConsumerId = consumerId.GetValueOrDefault();
            if (resolvedBuyerUserId <= 0 && resolvedConsumerId > 0)
            {
                await using var buyerCommand = connection.CreateCommand();
                buyerCommand.Transaction = transaction;
                buyerCommand.CommandText =
                    """
                    SELECT TOP (1) [user_id]
                    FROM dbo.Consumers
                    WHERE [consumer_id] = @ConsumerId;
                    """;
                buyerCommand.Parameters.Add(new SqlParameter("@ConsumerId", SqlDbType.Int) { Value = resolvedConsumerId });

                var buyerValue = await buyerCommand.ExecuteScalarAsync(cancellationToken);
                resolvedBuyerUserId = buyerValue == null || buyerValue == DBNull.Value ? 0 : Convert.ToInt32(buyerValue);
            }

            var resolvedSellerUserId = sellerUserId;
            if (resolvedSellerUserId <= 0)
            {
                await using var sellerCommand = connection.CreateCommand();
                sellerCommand.Transaction = transaction;
                sellerCommand.CommandText =
                    """
                    SELECT TOP (1) [user_id]
                    FROM dbo.Sellers
                    WHERE [seller_id] = @SellerId;
                    """;
                sellerCommand.Parameters.Add(new SqlParameter("@SellerId", SqlDbType.Int) { Value = sellerId });

                var sellerValue = await sellerCommand.ExecuteScalarAsync(cancellationToken);
                resolvedSellerUserId = sellerValue == null || sellerValue == DBNull.Value ? 0 : Convert.ToInt32(sellerValue);
            }

            if (resolvedBuyerUserId <= 0 || resolvedSellerUserId <= 0)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                DECLARE @ConversationId INT;
                DECLARE @SentAt DATETIME2 = SYSDATETIME();

                SELECT TOP (1) @ConversationId = ConversationId
                FROM dbo.MessagingConversations
                WHERE BuyerUserId = @BuyerUserId
                  AND SellerUserId = @SellerUserId
                  AND ContextType = 1
                ORDER BY ConversationId;

                IF @ConversationId IS NULL
                BEGIN
                    INSERT INTO dbo.MessagingConversations
                    (
                        BuyerUserId,
                        SellerUserId,
                        ContextType,
                        OrderId,
                        LastMessageAt,
                        BuyerLastReadAt,
                        SellerLastReadAt,
                        CreatedAt,
                        UpdatedAt
                    )
                    VALUES
                    (
                        @BuyerUserId,
                        @SellerUserId,
                        1,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        @SentAt,
                        @SentAt
                    );

                    SET @ConversationId = CAST(SCOPE_IDENTITY() AS INT);
                END;

                INSERT INTO dbo.MessagingMessages
                (
                    ConversationId,
                    SenderUserId,
                    Body,
                    AttachmentUrl,
                    SentAt,
                    IsDeleted
                )
                VALUES
                (
                    @ConversationId,
                    @SellerUserId,
                    @Body,
                    NULL,
                    @SentAt,
                    0
                );

                UPDATE dbo.MessagingConversations
                SET LastMessageAt = @SentAt,
                    UpdatedAt = @SentAt
                WHERE ConversationId = @ConversationId;
                """;

            var body =
                $"Replacement order created\n\nYour replacement item for Order #{originalOrderId} has been approved and is now waiting to be shipped.\nReplacement Order: #{replacementOrderId}\nAmount Due: PHP 0.00";

            command.Parameters.Add(new SqlParameter("@BuyerUserId", SqlDbType.Int) { Value = resolvedBuyerUserId });
            command.Parameters.Add(new SqlParameter("@SellerUserId", SqlDbType.Int) { Value = resolvedSellerUserId });
            command.Parameters.Add(new SqlParameter("@Body", SqlDbType.NVarChar, 2000) { Value = body });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<(string TableName, Dictionary<string, string> Columns)?> ResolveReturnTableAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var columns = await GetColumnsAsync(connection, "returns", cancellationToken);
            if (columns.Count > 0)
            {
                return ("returns", columns);
            }

            columns = await GetColumnsAsync(connection, "Returns", cancellationToken);
            return columns.Count > 0 ? ("Returns", columns) : null;
        }

        private static string NormalizeResolutionType(string? resolutionType)
        {
            return string.Equals(resolutionType?.Trim(), "Replacement", StringComparison.OrdinalIgnoreCase)
                ? "Replacement"
                : "Refund";
        }

        private static object NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
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
                var resolutionTypeColumn = FindColumn(columns, "ResolutionType", "resolution_type");
                var replacementOrderColumn = FindColumn(columns, "ReplacementOrderId", "replacement_order_id");

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
                    SelectOrDefault(resolutionTypeColumn, "ResolutionType", "'Refund'"),
                    SelectOrDefault(replacementOrderColumn, "ReplacementOrderId", "NULL"),
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
                        ResolutionType = GetString(reader, "ResolutionType"),
                        ReplacementOrderId = GetNullableInt(reader, "ReplacementOrderId"),
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
            public string ResolutionType { get; set; } = "Refund";
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
