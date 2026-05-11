using Microsoft.AspNetCore.Mvc;
using NextHorizon.Models;
using NextHorizon.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NextHorizon.Data;
using Microsoft.EntityFrameworkCore;
using NextHorizon.Messaging.Models;
using NextHorizon.Validation;
namespace NextHorizon.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IOrderService _orderService;
        private readonly ISellerContextService _sellerContextService;
        private readonly ISellerPerformanceService _sellerPerformanceService;
        private readonly ISellerNotificationService _sellerNotificationService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        public DashboardController(
            ISellerContextService sellerContextService,
            ISellerPerformanceService sellerPerformanceService,
            IConfiguration configuration,
            IOrderService orderService,
            ISellerNotificationService sellerNotificationService,
            AppDbContext context,
            IWebHostEnvironment environment)
            
        {
            _sellerContextService = sellerContextService;
            _sellerPerformanceService = sellerPerformanceService;
            _configuration = configuration;
            _orderService = orderService;
            _sellerNotificationService = sellerNotificationService;
            _context = context;
            _environment = environment;
        }

        private IActionResult? RedirectIfNotLoggedIn()
        {
            if (HttpContext.Session.GetString("SellerEmail") == null)
                return RedirectToAction("Login", "Account");
            return null;
        }

        private int? GetSellerIdFromSession()
        {
            // GetInt32 returns null if the key doesn't exist
            return HttpContext.Session.GetInt32("SellerId");
        }

        private string GetConnectionString()
        {
            return _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        }

        // ============== SELLER DASHBOARD ==============
        public async Task<IActionResult> SellerDashboard(CancellationToken cancellationToken)
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;
            
            return View(await BuildSellerDashboardModelAsync(cancellationToken));
        }

        // ============== ORDER MANAGEMENT ==============
        public async Task<IActionResult> OrderManagement(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
       {
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (currentSellerId == null)
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

    try
    {
        var realOrders = await _orderService.GetOrdersBySellerAsync(currentSellerId.Value, normalizedStartDate, normalizedEndDate);
        var couriers = await _orderService.GetCouriersAsync();
        var returnRequests = await GetReturnRequestsBySellerAsync(currentSellerId.Value, normalizedStartDate, normalizedEndDate);
        ViewBag.Couriers = couriers;
        ViewBag.ReturnRequests = returnRequests;
        return View(realOrders);
    }
    catch (Exception ex) when (IsDatabaseConnectionException(ex))
    {
        ViewBag.Couriers = new List<Logistics>();
        ViewBag.ReturnRequests = new List<ReturnRequest>();
        TempData["ErrorMessage"] = "Order data is temporarily unavailable because the database connection could not be established.";
        return View(new List<Order>());
    }
    }
    [HttpGet]
public async Task<IActionResult> GetOrderDetails(int orderId)
{
    // Security Check
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (currentSellerId == null) return Unauthorized();

var order = await _orderService.GetOrderByIdAsync(orderId, currentSellerId.Value);
    if (order == null) return NotFound(new { success = false, message = "Order not found" });

        // Map to a clean object to avoid JSON Circular Reference Exceptions (HTTP 500)
        // and precisely match the JavaScript frontend's expected properties.
        var safeData = new
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
            deliveryOption = order.Courier ?? "Standard",
            quantity = order.Quantity,
            subtotal = order.Subtotal,
            shippingFee = order.ShippingFee,
            totalAmount = order.Subtotal + order.ShippingFee,
            productName = order.ProductName
        };

        return Json(new { success = true, data = safeData });
}
public class OrderNoteRequest
{
    public int OrderId { get; set; }
    public string Note { get; set; } = string.Empty;
}

[HttpPost]
public async Task<IActionResult> SaveOrderNote([FromBody] OrderNoteRequest request)
{
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (!currentSellerId.HasValue) return Json(new { success = false, message = "Session expired." });

    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == request.OrderId);
    if (order == null || order.seller_id != currentSellerId.Value)
    {
        return Json(new { success = false, message = "Order not found or unauthorized." });
    }
    order.SellerNote = request.Note;
    try
    {
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Note saved successfully!" });
    }
    catch (Exception)
    {
        return Json(new { success = false, message = "Database error occurred." });
    }
}
[HttpPost]
public async Task<IActionResult> AcceptOrder([FromBody] AcceptOrderRequest request)
{
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (currentSellerId == null)
    {
        return Json(new { success = false, message = "Session expired. Please log in again." });
    }

    var result = await _orderService.AcceptOrderAsync(request.OrderId, currentSellerId.Value, request.Courier);
    return Json(new { success = result.Success, message = result.Message });
}
public class MarkReturnedRequest
{
    public int OrderId { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
    public string? ReturnNote { get; set; }
    public IFormFile? ReturnProof { get; set; }
}
public class MarkShippedRequest
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    // IFormFile is what C# uses to catch the uploaded image
    public IFormFile? ProofOfShipment { get; set; }
}
[HttpPost]
public async Task<IActionResult> MarkOrderShipped([FromForm] MarkShippedRequest request)
{
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (!currentSellerId.HasValue)
        return Json(new { success = false, message = "Session expired. Please log in again." });

    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == request.OrderId);
    if (order == null || order.seller_id != currentSellerId.Value)
    {
        return Json(new { success = false, message = "Order not found or unauthorized." });
    }

    // --- FILE SAVING BLOCK ---
    if (request.ProofOfShipment != null && request.ProofOfShipment.Length > 0)
    {
        try 
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
            
            if (!Directory.Exists(uploadsFolder)) 
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetExtension(request.ProofOfShipment.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await request.ProofOfShipment.CopyToAsync(fileStream);
            }

            // INSERTED HERE: Update the order object with the new path
            order.ProofOfShipmentUrl = "/uploads/receipts/" + uniqueFileName;
        }
        catch (Exception ex)
        {
            Console.WriteLine("File upload failed: " + ex.Message);
            // Optionally: return Json(new { success = false, message = "File upload failed." });
        }
    }

    // Dual-Tracking Architecture
    order.Status = "Shipped";              
    order.FulfillmentStatus = "Shipped";   
    order.TrackingNumber = request.TrackingNumber; 
    order.DateShipped = DateTime.Now;      

    try
    {
        // This saves BOTH the status changes AND the ProofOfShipmentUrl
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Order marked as shipped successfully!" });
    }
    catch (Exception)
    {
        return Json(new { success = false, message = "Database error occurred." });
    }
}
[HttpPost]
public async Task<IActionResult> MarkOrderReturned([FromForm] MarkReturnedRequest request)
{
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (!currentSellerId.HasValue)
        return Json(new { success = false, message = "Session expired. Please log in again." });

    if (request.OrderId <= 0)
        return Json(new { success = false, message = "Invalid order." });

    var returnReason = request.ReturnReason?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(returnReason))
        return Json(new { success = false, message = "Return reason is required." });

    if (request.ReturnProof is not null)
    {
        var isValidProof = request.ReturnProof.Length > 0
            && request.ReturnProof.Length <= UploadValidationRules.MaxProofSizeBytes
            && UploadValidationRules.HaveAllowedExtension(request.ReturnProof)
            && UploadValidationRules.HaveAllowedContentType(request.ReturnProof)
            && UploadValidationRules.HaveMatchingExtensionAndContentType(request.ReturnProof)
            && UploadValidationRules.HaveMatchingSignature(request.ReturnProof)
            && UploadValidationRules.HaveValidImageStructure(request.ReturnProof);

        if (!isValidProof)
            return Json(new { success = false, message = "Proof image must be a valid JPG, JPEG, PNG, or WEBP file and 5MB or smaller." });
    }

    var order = await _context.Orders
        .Include(o => o.OrderItems)
        .FirstOrDefaultAsync(o => o.OrderID == request.OrderId && o.seller_id == currentSellerId.Value);

    if (order == null)
        return Json(new { success = false, message = "Order not found or unauthorized." });

    if (!string.Equals(order.Status, "Shipped", StringComparison.OrdinalIgnoreCase))
        return Json(new { success = false, message = "Only shipped orders can be marked as returned." });

    await using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        foreach (var item in order.OrderItems)
        {
            DbProductVariant? variant = null;

            if (item.VariantId.HasValue)
            {
                variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);
            }

            variant ??= await _context.ProductVariants.FirstOrDefaultAsync(v =>
                v.ProductId == item.ProductID &&
                (item.Size == null || v.Size == item.Size) &&
                (item.Color == null || v.Style == item.Color));

            if (variant != null)
            {
                variant.Quantity += item.Quantity;
                variant.Availability = variant.Quantity > 0 ? "In Stock" : "Out of Stock";
            }
        }

        if (request.ReturnProof is not null)
        {
            await using var memory = new MemoryStream();
            await request.ReturnProof.CopyToAsync(memory);
            order.ReturnProofImageData = memory.ToArray();
            order.ReturnProofImageMimeType = string.IsNullOrWhiteSpace(request.ReturnProof.ContentType)
                ? "application/octet-stream"
                : request.ReturnProof.ContentType;
        }

        order.Status = "Failed Delivery";
        order.FulfillmentStatus = "Failed Delivery";
        order.ReturnReason = returnReason;
        order.ReturnNote = string.IsNullOrWhiteSpace(request.ReturnNote) ? null : request.ReturnNote.Trim();
        order.ReturnProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE seller_wallet
            SET Pending_Balance = CASE
                    WHEN Pending_Balance >= {order.TotalAmount} THEN Pending_Balance - {order.TotalAmount}
                    ELSE 0
                END
            WHERE Seller_Id = {order.seller_id}");

        await transaction.CommitAsync();
        return Json(new { success = true, message = "Order marked as Delivery Failed, stock restored, and pending funds cancelled." });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Json(new { success = false, message = ex.Message });
    }
}

[HttpGet]
public async Task<IActionResult> ReturnProofImage(int orderId)
{
    int? currentSellerId = HttpContext.Session.GetInt32("SellerId");
    if (!currentSellerId.HasValue)
        return Unauthorized();

    var order = await _context.Orders
        .AsNoTracking()
        .FirstOrDefaultAsync(o => o.OrderID == orderId && o.seller_id == currentSellerId.Value);

    if (order == null || order.ReturnProofImageData == null || order.ReturnProofImageData.Length == 0)
        return NotFound();

    return File(order.ReturnProofImageData, order.ReturnProofImageMimeType ?? "application/octet-stream");
}
public class ShipmentUpdateModel
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
}
// ==========================================
// ORDER DETAILS
// ==========================================
[HttpGet]
public async Task<IActionResult> OrderDetails(string id, CancellationToken cancellationToken)
{
    var redirect = RedirectIfNotLoggedIn();
    if (redirect != null) return redirect;

    if (!int.TryParse(id.Replace("ORD-", ""), out int orderId))
    {
        return BadRequest("Invalid Order ID");
    }

    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (!sellerId.HasValue)
    {
        return RedirectToAction("Login", "Account");
    }

    var order = await _orderService.GetOrderByIdAsync(orderId, sellerId.Value);
    if (order == null)
    {
        return NotFound();
    }

    if (order.logistics_id.HasValue && order.logistics_id.Value > 0)
    {
        var courierName = await _context.Logistics
            .Where(l => l.logistics_id == order.logistics_id.Value)
            .Select(l => l.courier_name)
            .FirstOrDefaultAsync(cancellationToken);

        order.Courier = string.IsNullOrEmpty(courierName) ? "NextHorizon Partner" : courierName;
    }
    else
    {
        order.Courier = "NextHorizon Partner";
    }

    return View(order);
}
    

        private static OrderDetailsViewModel BuildOrderDetailsModel(Order order, string sellerName)
{
    // Combine the new address columns we added to the database
    var fullAddress = $"{order.StreetAddress}, {order.City} {order.PostalCode}".Trim().Trim(',');
    if (string.IsNullOrWhiteSpace(fullAddress)) 
    {
        fullAddress = "No address provided";
    }

    // Calculate unit price safely
    var unitPrice = order.Quantity > 0 ? decimal.Round(order.Subtotal / order.Quantity, 2) : order.Subtotal;

    return new OrderDetailsViewModel
    {
        SellerName = sellerName,
        OrderId = order.OrderID.ToString(),                
        BuyerName = order.FullName ?? string.Empty,
        OrderDateTime = order.OrderDate,
        PaymentStatus = order.PaymentMethod ?? string.Empty,
        FulfillmentStatus = order.Status ?? string.Empty,
        
        
        Courier = order.Courier ?? "Not Selected",
        TrackingNumber = order.TrackingNumber ?? "Pending Tracking",
        ShippingAddress = fullAddress,
        ContactNumber = order.PhoneNumber ?? "No phone number",
        Notes = "No special instructions.", 
        
        Subtotal = order.Subtotal,
        ShippingFee = order.ShippingFee,
        Discount = 0.00m, 
        Items = new List<OrderLineItemViewModel>
        {
            new OrderLineItemViewModel
            {
                ProductName = order.ProductName ?? string.Empty,
                Quantity = order.Quantity,
                UnitPrice = unitPrice
            }
        }
    };
}
public sealed class SubmitReturnRequestModel
{
    public int OrderId { get; set; }
    public int BuyerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IFormFile? ImageProof { get; set; }
}

public sealed class ReviewReturnRequestModel
{
    public int ReturnId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public string RejectionNote { get; set; } = string.Empty;
}

public sealed class ReturnStatusUpdateModel
{
    public int ReturnId { get; set; }
    public bool RestoreStock { get; set; }
}

[HttpPost]
public async Task<IActionResult> SubmitReturnRequest([FromForm] SubmitReturnRequestModel request)
{
    if (request.OrderId <= 0)
        return Json(new { success = false, message = "Invalid order." });

    var reason = request.Reason?.Trim() ?? string.Empty;
    var message = request.Message?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(reason))
        return Json(new { success = false, message = "Reason is required." });

    var order = await _context.Orders
        .AsNoTracking()
        .FirstOrDefaultAsync(o => o.OrderID == request.OrderId);

    if (order == null)
        return Json(new { success = false, message = "Order not found." });

    if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
        return Json(new { success = false, message = "Only delivered orders can be returned." });

    if (request.ImageProof is null || request.ImageProof.Length == 0)
        return Json(new { success = false, message = "Image proof is required." });

    var isValidProof = request.ImageProof.Length > 0
        && request.ImageProof.Length <= UploadValidationRules.MaxProofSizeBytes
        && UploadValidationRules.HaveAllowedExtension(request.ImageProof)
        && UploadValidationRules.HaveAllowedContentType(request.ImageProof)
        && UploadValidationRules.HaveMatchingExtensionAndContentType(request.ImageProof)
        && UploadValidationRules.HaveMatchingSignature(request.ImageProof)
        && UploadValidationRules.HaveValidImageStructure(request.ImageProof);

    if (!isValidProof)
        return Json(new { success = false, message = "Proof image must be a valid JPG, JPEG, PNG, or WEBP file and 5MB or smaller." });

    var hasExistingPendingRequest = await _context.ReturnRequests.AnyAsync(r =>
        r.OrderId == request.OrderId &&
        r.Status != "Refunded" &&
        r.Status != "Return Rejected");

    if (hasExistingPendingRequest)
        return Json(new { success = false, message = "A return request already exists for this order." });

    await using var memory = new MemoryStream();
    await request.ImageProof.CopyToAsync(memory);

    var buyerId = request.BuyerId > 0 ? request.BuyerId : 0;
    var returnRequest = new ReturnRequest
    {
        OrderId = order.OrderID,
        UserId = buyerId,
        SellerId = order.seller_id,
        Reason = reason,
        Message = string.IsNullOrWhiteSpace(message) ? null : message,
        FileName = Path.GetFileName(request.ImageProof.FileName),
        ContentType = string.IsNullOrWhiteSpace(request.ImageProof.ContentType) ? "application/octet-stream" : request.ImageProof.ContentType,
        ImageData = memory.ToArray(),
        Status = "Return Requested",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _context.ReturnRequests.Add(returnRequest);
    await _context.SaveChangesAsync();

    await _sellerNotificationService.CreateIfMissingAsync(new SellerNotification
    {
        RecipientType = "seller",
        RecipientId = order.seller_id.ToString(),
        SellerId = order.seller_id,
        OrderId = order.OrderID,
        Category = "return",
        Title = "Return Request Submitted",
        Message = $"A buyer requested a refund/return for order #{order.OrderID}.",
        IsRead = false,
        CreatedAt = returnRequest.CreatedAt
    });

    return Json(new { success = true, message = "Return request submitted successfully." });
}

[HttpPost]
public async Task<IActionResult> ReviewReturnRequest([FromBody] ReviewReturnRequestModel request)
{
    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (!sellerId.HasValue)
        return Json(new { success = false, message = "Session expired. Please log in again." });

    var returnRequest = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.ReturnId == request.ReturnId && r.SellerId == sellerId.Value);
    if (returnRequest == null)
        return Json(new { success = false, message = "Return request not found." });

    if (!string.Equals(returnRequest.Status, "Return Requested", StringComparison.OrdinalIgnoreCase))
        return Json(new { success = false, message = "Only new return requests can be reviewed." });

    var decision = request.Decision?.Trim() ?? string.Empty;
    if (string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase))
    {
        returnRequest.Status = "Return Approved";
        returnRequest.SellerDecisionReason = null;
        returnRequest.SellerDecisionNote = null;
    }
    else if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase))
    {
        var rejectionReason = request.RejectionReason?.Trim() ?? string.Empty;
        var rejectionNote = request.RejectionNote?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return Json(new { success = false, message = "A rejection reason is required." });
        }

        returnRequest.Status = "Return Rejected";
        returnRequest.SellerDecisionReason = rejectionReason;
        returnRequest.SellerDecisionNote = string.IsNullOrWhiteSpace(rejectionNote) ? null : rejectionNote;
    }
    else
    {
        return Json(new { success = false, message = "Invalid review decision." });
    }

    returnRequest.SellerId = sellerId.Value;
    returnRequest.ReviewedAt = DateTime.UtcNow;
    returnRequest.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return Json(new { success = true, message = $"Return request {returnRequest.Status.ToLowerInvariant()}." });
}

[HttpPost]
public async Task<IActionResult> MarkReturnItemReceived([FromBody] ReturnStatusUpdateModel request)
{
    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (!sellerId.HasValue)
        return Json(new { success = false, message = "Session expired. Please log in again." });

    var returnRequest = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.ReturnId == request.ReturnId && r.SellerId == sellerId.Value);
    if (returnRequest == null)
        return Json(new { success = false, message = "Return request not found." });

    if (!string.Equals(returnRequest.Status, "Return Approved", StringComparison.OrdinalIgnoreCase))
        return Json(new { success = false, message = "Only approved returns can be marked as returned." });

    returnRequest.Status = "Item Returned";
    returnRequest.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return Json(new { success = true, message = "Item marked as returned." });
}

[HttpPost]
public async Task<IActionResult> ConfirmReturnRefund([FromBody] ReturnStatusUpdateModel request)
{
    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (!sellerId.HasValue)
        return Json(new { success = false, message = "Session expired. Please log in again." });

    var returnRequest = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.ReturnId == request.ReturnId && r.SellerId == sellerId.Value);
    if (returnRequest == null)
        return Json(new { success = false, message = "Return request not found." });

    if (!string.Equals(returnRequest.Status, "Item Returned", StringComparison.OrdinalIgnoreCase))
        return Json(new { success = false, message = "Only returned items can be refunded." });

    if (request.RestoreStock)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderID == returnRequest.OrderId && o.seller_id == sellerId.Value);

        if (order != null)
        {
            await RestoreStockForOrderAsync(order);
        }
    }

    returnRequest.Status = "Refunded";
    returnRequest.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return Json(new { success = true, message = "Refund confirmed." });
}

private async Task<List<ReturnRequest>> GetReturnRequestsBySellerAsync(int sellerId, DateTime? startDate = null, DateTime? endDate = null)
{
    var query = _context.ReturnRequests
        .AsNoTracking()
        .Where(r => r.SellerId == sellerId);

    if (startDate.HasValue)
    {
        var normalizedStartDate = startDate.Value.Date;
        query = query.Where(r => r.CreatedAt >= normalizedStartDate);
    }

    if (endDate.HasValue)
    {
        var exclusiveEndDate = endDate.Value.Date.AddDays(1);
        query = query.Where(r => r.CreatedAt < exclusiveEndDate);
    }

    var returnRequests = await query
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

    var orderIds = returnRequests.Select(r => r.OrderId).Distinct().ToList();
    if (orderIds.Count == 0)
        return returnRequests;

    var orders = await _context.Orders
        .AsNoTracking()
        .Where(o => orderIds.Contains(o.OrderID))
        .Select(o => new { o.OrderID, o.FullName, o.OrderDate })
        .ToDictionaryAsync(o => o.OrderID);

    foreach (var returnRequest in returnRequests)
    {
        if (orders.TryGetValue(returnRequest.OrderId, out var order))
        {
            returnRequest.BuyerName = order.FullName ?? "Buyer";
            returnRequest.OrderDate = order.OrderDate;
        }
    }

    return returnRequests;
}


[HttpGet]
public async Task<IActionResult> ReturnRequestImage(int returnId)
{
    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (!sellerId.HasValue)
        return Unauthorized();

    var returnRequest = await _context.ReturnRequests
        .AsNoTracking()
        .FirstOrDefaultAsync(r => r.ReturnId == returnId && r.SellerId == sellerId.Value);

    if (returnRequest == null || returnRequest.ImageData == null || returnRequest.ImageData.Length == 0)
        return NotFound();

    return File(returnRequest.ImageData, returnRequest.ContentType ?? "application/octet-stream");
}
private async Task RestoreStockForOrderAsync(Order order)
{
    foreach (var item in order.OrderItems)
    {
        DbProductVariant? variant = null;

        if (item.VariantId.HasValue)
        {
            variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);
        }

        variant ??= await _context.ProductVariants.FirstOrDefaultAsync(v =>
            v.ProductId == item.ProductID &&
            (item.Size == null || v.Size == item.Size) &&
            (item.Color == null || v.Style == item.Color));

        if (variant == null)
            continue;

        variant.Quantity += item.Quantity;
        variant.Availability = variant.Quantity > 0 ? "In Stock" : "Out of Stock";
    }
}
        // ============== FINANCE DASHBOARD ==============
        public async Task<IActionResult> Finance()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            var sellerId = GetSellerIdFromSession();
            if (sellerId == null) return RedirectToAction("Login", "Account");

            var model = await GetFinanceDashboardData(sellerId.Value);
            return View(model);
        }


        private async Task<FinanceViewModel> GetFinanceDashboardData(int sellerId)
        {
            var model = new FinanceViewModel();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_GetSellerFinanceDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Read Seller Info
                        if (await reader.ReadAsync())
                        {
                            model.SellerName = reader["SellerName"]?.ToString() ?? "Seller";
                            model.CurrentDate = reader["CurrentDate"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["CurrentDate"]) 
                                : DateTime.Now;
                        }

                        // Next result set - Wallet Info
                        if (await reader.NextResultAsync() && await reader.ReadAsync())
                        {
                            model.AvailableBalance = reader["Available_Balance"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Available_Balance"]) : 0;
                            model.PendingBalance = reader["Pending_Balance"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Pending_Balance"]) : 0;
                            model.TotalEarned = reader["Total_Earned"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Total_Earned"]) : 0;
                            model.TotalWithdrawn = reader["Total_Withdrawn"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Total_Withdrawn"]) : 0;
                            
                            // Get pending payout count and amount from withdrawal_requests
                            model.PendingPayoutCount = reader["PendingPayoutCount"] != DBNull.Value 
                                ? Convert.ToInt32(reader["PendingPayoutCount"]) : 0;
                            model.TotalPendingWithdrawal = reader["TotalPendingWithdrawal"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["TotalPendingWithdrawal"]) : 0;
                        }

                        // Next result set - Today's Revenue
                        if (await reader.NextResultAsync() && await reader.ReadAsync())
                        {
                            model.TodayRevenue = reader["TodayRevenue"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["TodayRevenue"]) : 0;
                        }

                        // Next result set - This Month's Revenue
                        if (await reader.NextResultAsync() && await reader.ReadAsync())
                        {
                            model.ThisMonthRevenue = reader["ThisMonthRevenue"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["ThisMonthRevenue"]) : 0;
                        }

                        // Next result set - Recent Transactions (Combined)
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.Transactions.Add(new FinanceTransactionViewModel
                                {
                                    ReferenceId = reader["ReferenceId"]?.ToString() ?? "",
                                    TransactionDate = reader["TransactionDate"] != DBNull.Value 
                                        ? Convert.ToDateTime(reader["TransactionDate"]) : DateTime.Now,
                                    Type = reader["Type"]?.ToString() ?? "",
                                    Method = reader["Method"]?.ToString() ?? "",
                                    Amount = reader["Amount"] != DBNull.Value 
                                        ? Convert.ToDecimal(reader["Amount"]) : 0,
                                    Status = reader["Status"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }

            return model;
        }
        
        // ============== TRANSACTION HISTORY ==============
        public async Task<IActionResult> TransactionHistory(
            int page = 1, 
            string? type = null, 
            string? status = null, 
            DateTime? fromDate = null, 
            DateTime? toDate = null)
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            var sellerId = GetSellerIdFromSession();
            if (sellerId == null) return RedirectToAction("Login", "Account");

            var model = await GetTransactionHistory(sellerId.Value, page, 10, type, status, fromDate, toDate);
            return View(model);
        }

        private async Task<TransactionHistoryViewModel> GetTransactionHistory(
            int sellerId, 
            int pageNumber, 
            int pageSize,
            string? type = null,
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var model = new TransactionHistoryViewModel
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                FilterType = type,
                FilterStatus = status,
                FilterFromDate = fromDate,
                FilterToDate = toDate
            };

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_GetSellerTransactionHistory", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId);
                    command.Parameters.AddWithValue("@PageNumber", pageNumber);
                    command.Parameters.AddWithValue("@PageSize", pageSize);
                    
                    if (!string.IsNullOrEmpty(type))
                        command.Parameters.AddWithValue("@TransactionType", type);
                    if (!string.IsNullOrEmpty(status))
                        command.Parameters.AddWithValue("@Status", status);
                    if (fromDate.HasValue)
                        command.Parameters.AddWithValue("@FromDate", fromDate.Value);
                    if (toDate.HasValue)
                        command.Parameters.AddWithValue("@ToDate", toDate.Value.AddDays(1).AddSeconds(-1));

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Read Total Count
                        if (await reader.ReadAsync())
                        {
                            model.TotalCount = reader["TotalCount"] != DBNull.Value 
                                ? Convert.ToInt32(reader["TotalCount"]) : 0;
                        }

                        // Read Transactions
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.Transactions.Add(new FinanceTransactionViewModel
                                {
                                    ReferenceId = reader["ReferenceId"]?.ToString() ?? "",
                                    TransactionDate = reader["TransactionDate"] != DBNull.Value 
                                        ? Convert.ToDateTime(reader["TransactionDate"]) : DateTime.Now,
                                    Type = reader["Type"]?.ToString() ?? "",
                                    Method = reader["Method"]?.ToString() ?? "",
                                    Amount = reader["Amount"] != DBNull.Value 
                                        ? Convert.ToDecimal(reader["Amount"]) : 0,
                                    Status = reader["Status"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }



            // Get seller name separately
            model.SellerName = await GetSellerName(sellerId);
            model.CurrentDate = DateTime.Now;

            return model;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionDetailsSP(string referenceId)
        {
            try
            {
                Console.WriteLine($"GetTransactionDetailsSP called with referenceId: {referenceId}");
                
                var sellerId = GetSellerIdFromSession();
                if (sellerId == null)
                {
                    return Json(new { success = false, message = "Seller not authenticated" });
                }

                Console.WriteLine($"SellerId: {sellerId}");

                var transaction = await GetTransactionDetailsFromSP(sellerId.Value, referenceId);

                if (transaction == null)
                {
                    Console.WriteLine($"No transaction found for referenceId: {referenceId}");
                    return Json(new { success = false, message = $"Transaction not found with reference: {referenceId}" });
                }

                return Json(new { success = true, transaction });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<object?> GetTransactionDetailsFromSP(int sellerId, string referenceId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_GetTransactionDetails", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId);
                    command.Parameters.AddWithValue("@ReferenceId", referenceId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Check if we actually got data (ReferenceId not null)
                            if (reader["ReferenceId"] != DBNull.Value)
                            {
                                string source = reader["Source"]?.ToString() ?? "";
                                
                                // Handle wallet transaction (no Method column)
                                if (source == "wallet_transaction")
                                {
                                    return new
                                    {
                                        referenceId = reader["ReferenceId"].ToString(),
                                        transactionDate = Convert.ToDateTime(reader["TransactionDate"]),
                                        type = reader["Type"].ToString(),
                                        method = "N/A", // Wallet transactions don't have method
                                        amount = Convert.ToDecimal(reader["Amount"]),
                                        status = reader["Status"].ToString(),
                                        source = source,
                                        additionalDetails = new { }
                                    };
                                }
                                // Handle withdrawal request (has Method column)
                                else
                                {
                                    // Parse additional details if they exist
                                    object additionalDetails = new Dictionary<string, string>();
                                    if (reader["AdditionalDetails"] != DBNull.Value)
                                    {
                                        string details = reader["AdditionalDetails"]?.ToString() ?? string.Empty;
                                        var detailsDict = new Dictionary<string, string>();
                                        
                                        // Parse the CONCAT string format: "Requested: ... | Processed: ... | Account: ..."
                                        var parts = details.Split('|');
                                        foreach (var part in parts)
                                        {
                                            var keyValue = part.Split(':');
                                            if (keyValue.Length >= 2)
                                            {
                                                string key = keyValue[0].Trim();
                                                string value = string.Join(":", keyValue.Skip(1)).Trim();
                                                detailsDict[key] = value;
                                            }
                                        }
                                        additionalDetails = detailsDict;
                                    }

                                    return new
                                    {
                                        referenceId = reader["ReferenceId"].ToString(),
                                        transactionDate = Convert.ToDateTime(reader["TransactionDate"]),
                                        type = reader["Type"].ToString(),
                                        method = reader["Method"]?.ToString() ?? "N/A",
                                        amount = Convert.ToDecimal(reader["Amount"]),
                                        status = reader["Status"].ToString(),
                                        source = source,
                                        additionalDetails = additionalDetails
                                    };
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
            
        private static bool IsDatabaseConnectionException(Exception ex)
        {
            return ex is SqlException
                || ex is TimeoutException
                || ex is Win32Exception
                || (ex.InnerException != null && IsDatabaseConnectionException(ex.InnerException));
        }
        // ============== MY BALANCE ==============
        public async Task<IActionResult> MyBalance()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            var sellerId = GetSellerIdFromSession();
            if (sellerId == null) return RedirectToAction("Login", "Account");

            var model = await GetBalanceDetails(sellerId.Value);
            return View(model);
        }

        private async Task<BalanceDetailsViewModel> GetBalanceDetails(int sellerId)
        {
            var model = new BalanceDetailsViewModel();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_GetSellerBalanceDetails", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model.SellerName = reader["SellerName"]?.ToString() ?? "Seller";
                            model.CurrentDate = reader["CurrentDate"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["CurrentDate"]) : DateTime.Now;
                            model.AvailableBalance = reader["Available_Balance"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Available_Balance"]) : 0;
                            model.PendingBalance = reader["Pending_Balance"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Pending_Balance"]) : 0;
                            model.TotalEarned = reader["Total_Earned"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Total_Earned"]) : 0;
                            model.TotalWithdrawn = reader["Total_Withdrawn"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["Total_Withdrawn"]) : 0;
                            model.PendingPayoutCount = reader["PendingPayoutCount"] != DBNull.Value 
                                ? Convert.ToInt32(reader["PendingPayoutCount"]) : 0;
                            model.TotalPendingWithdrawal = reader["TotalPendingWithdrawal"] != DBNull.Value 
                                ? Convert.ToDecimal(reader["TotalPendingWithdrawal"]) : 0;
                        }

                        // Read recent transactions if available
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.RecentTransactions.Add(new FinanceTransactionViewModel
                                {
                                    ReferenceId = reader["ReferenceId"]?.ToString() ?? "",
                                    TransactionDate = reader["TransactionDate"] != DBNull.Value 
                                        ? Convert.ToDateTime(reader["TransactionDate"]) : DateTime.Now,
                                    Type = reader["Type"]?.ToString() ?? "",
                                    Method = reader["Method"]?.ToString() ?? "",
                                    Amount = reader["Amount"] != DBNull.Value 
                                        ? Convert.ToDecimal(reader["Amount"]) : 0,
                                    Status = reader["Status"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }

            return model;
        }

        // ============== PAYOUT ACCOUNTS ==============
        public async Task<IActionResult> PayoutAccounts()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            var sellerId = GetSellerIdFromSession();
            if (sellerId == null) return RedirectToAction("Login", "Account");

            var accounts = await GetPayoutAccounts(sellerId.Value);
            ViewBag.SellerName = await GetSellerName(sellerId.Value);
            
            // Pass the model to the view
            return View(accounts);
        }

        private async Task<List<PayoutAccountViewModel>> GetPayoutAccounts(int sellerId)
        {
            var accounts = new List<PayoutAccountViewModel>();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_GetSellerPayoutAccounts", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            accounts.Add(new PayoutAccountViewModel
                            {
                                AccountId = reader["AccountId"] != DBNull.Value ? Convert.ToInt32(reader["AccountId"]) : 0,
                                AccountType = reader["AccountType"]?.ToString() ?? "",
                                AccountName = reader["AccountName"]?.ToString() ?? "",
                                AccountNumber = reader["AccountNumber"]?.ToString() ?? "",
                                BankName = reader["BankName"]?.ToString(),
                                CardNumber = reader["CardNumber"]?.ToString(),
                                LastUsed = reader["LastUsed"] != DBNull.Value ? Convert.ToDateTime(reader["LastUsed"]) : (DateTime?)null,
                                IsDefault = reader["IsDefault"] != DBNull.Value && Convert.ToBoolean(reader["IsDefault"])
                            });
                        }
                    }
                }
            }

            return accounts;
        }

        // Add these methods to your DashboardController.cs

// ============== SET DEFAULT PAYOUT ACCOUNT ==============
[HttpPost]
[HttpPost]
        public async Task<IActionResult> SetDefaultPayoutAccount([FromBody] SetDefaultAccountRequest request)
{
    var redirect = RedirectIfNotLoggedIn();
    if (redirect != null) return Unauthorized();

    var sellerId = GetSellerIdFromSession();
    if (sellerId == null) return Unauthorized();

    try
    {
        using (var connection = new SqlConnection(GetConnectionString()))
        {
            using (var command = new SqlCommand("sp_SetDefaultPayoutAccount", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@AccountId", request.AccountId);
                command.Parameters.AddWithValue("@SellerId", sellerId.Value);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        await _sellerNotificationService.CreateAsync(new SellerNotification
        {
            RecipientType = "seller",
            RecipientId = sellerId.Value.ToString(),
            SellerId = sellerId.Value,
            Category = "system",
            Type = "system.payout_account_default_changed",
            Title = "Default Payout Account Updated",
            Message = "Your default payout account has been updated.",
            Priority = "medium",
            DeliveryMode = "in_app",
            LinkType = "payout",
            LinkTarget = "/Dashboard/PayoutAccounts",
            ActionRequired = false,
            DeduplicationKey = $"system.payout_account_default_changed:{sellerId.Value}:{request.AccountId}"
        });

        return Ok();
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message);
    }
}


// ============== REMOVE PAYOUT ACCOUNT ==============

[HttpPost]
public async Task<IActionResult> RemovePayoutAccount([FromBody] RemoveAccountRequest request)
{
    var redirect = RedirectIfNotLoggedIn();
    if (redirect != null) return Unauthorized();

    var sellerId = GetSellerIdFromSession();
    if (sellerId == null) return Unauthorized();

    try
    {
        using (var connection = new SqlConnection(GetConnectionString()))
        {
            using (var command = new SqlCommand("sp_RemovePayoutAccount", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@AccountId", request.AccountId);
                command.Parameters.AddWithValue("@SellerId", sellerId.Value);

                await connection.OpenAsync();
                
                // Execute and read the returned value
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        int rowsAffected = reader.GetInt32(0);

                        if (rowsAffected > 0)
                        {
                            await _sellerNotificationService.CreateAsync(new SellerNotification
                            {
                                RecipientType = "seller",
                                RecipientId = sellerId.Value.ToString(),
                                SellerId = sellerId.Value,
                                Category = "system",
                                Type = "system.payout_account_removed",
                                Title = "Payout Account Removed",
                                Message = "A payout account was removed from your seller profile.",
                                Priority = "medium",
                                DeliveryMode = "in_app",
                                LinkType = "payout",
                                LinkTarget = "/Dashboard/PayoutAccounts",
                                ActionRequired = false,
                                DeduplicationKey = $"system.payout_account_removed:{sellerId.Value}:{request.AccountId}"
                            });
                        }
                        
                        // Always return 200 OK with success flag
                        return Ok(new { 
                            success = rowsAffected > 0, 
                            message = rowsAffected > 0 ? "Account removed successfully" : "Account not found or already removed" 
                        });
                    }
                }
                
                // If no result returned
                return Ok(new { success = false, message = "No response from database" });
            }
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}


// Update the POST AddPayoutAccount method
// ============== ADD PAYOUT ACCOUNT (GET) ==============
[HttpGet]
public IActionResult AddPayoutAccount()
{
    var redirect = RedirectIfNotLoggedIn();
    if (redirect != null) return redirect;

    return View();
}

// ============== ADD PAYOUT ACCOUNT (POST) ==============
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddPayoutAccount(AddPayoutAccountViewModel model)
{
    var redirect = RedirectIfNotLoggedIn();
    if (redirect != null) return redirect;

    var sellerId = GetSellerIdFromSession();
    if (sellerId == null) return RedirectToAction("Login", "Account");

    try
    {
        using (var connection = new SqlConnection(GetConnectionString()))
        {
            using (var command = new SqlCommand("sp_AddPayoutAccount", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                
                command.Parameters.AddWithValue("@SellerId", sellerId.Value);
                command.Parameters.AddWithValue("@AccountType", model.AccountType ?? "");
                
                // Set AccountName and AccountNumber based on account type
                if (model.AccountType == "ewallet")
                {
                    command.Parameters.AddWithValue("@AccountName", model.EwalletAccountName ?? "");
                    command.Parameters.AddWithValue("@AccountNumber", model.EwalletAccountNumber ?? "");
                    command.Parameters.AddWithValue("@BankName", model.EWalletType ?? "E-Wallet");
                }
                else if (model.AccountType == "bank")
                {
                    command.Parameters.AddWithValue("@AccountName", model.BankAccountName ?? "");
                    command.Parameters.AddWithValue("@AccountNumber", model.BankAccountNumber ?? "");
                    command.Parameters.AddWithValue("@BankName", model.BankName ?? "");
                }
                else if (model.AccountType == "card")
                {
                    command.Parameters.AddWithValue("@AccountName", "Card Holder");
                    command.Parameters.AddWithValue("@AccountNumber", model.CardNumber ?? "");
                    command.Parameters.AddWithValue("@BankName", "Credit Card");
                }
                else
                {
                    command.Parameters.AddWithValue("@AccountName", model.AccountName ?? "");
                    command.Parameters.AddWithValue("@AccountNumber", model.AccountNumber ?? "");
                    command.Parameters.AddWithValue("@BankName", DBNull.Value);
                }
                
                // Handle optional fields
                command.Parameters.AddWithValue("@CardNumber", string.IsNullOrEmpty(model.CardNumber) ? DBNull.Value : (object)model.CardNumber);
                command.Parameters.AddWithValue("@CvvCode", string.IsNullOrEmpty(model.CVV) ? DBNull.Value : (object)model.CVV);
                
                // Handle expiry date
                if (!string.IsNullOrEmpty(model.ExpiryDate) && model.ExpiryDate.Contains('/'))
                {
                    var expiryParts = model.ExpiryDate.Split('/');
                    command.Parameters.AddWithValue("@ExpirationMonth", expiryParts[0]);
                    command.Parameters.AddWithValue("@ExpirationYear", expiryParts.Length > 1 ? expiryParts[1] : DBNull.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@ExpirationMonth", DBNull.Value);
                    command.Parameters.AddWithValue("@ExpirationYear", DBNull.Value);
                }
                
                // Address fields
                command.Parameters.AddWithValue("@PostalCode", string.IsNullOrEmpty(model.PostalCode) ? DBNull.Value : (object)Convert.ToInt32(model.PostalCode));
                command.Parameters.AddWithValue("@Region", string.IsNullOrEmpty(model.Region) ? DBNull.Value : (object)model.Region);
                command.Parameters.AddWithValue("@Province", string.IsNullOrEmpty(model.Province) ? DBNull.Value : (object)model.Province);
                command.Parameters.AddWithValue("@City", string.IsNullOrEmpty(model.City) ? DBNull.Value : (object)model.City);
                command.Parameters.AddWithValue("@Barangay", string.IsNullOrEmpty(model.Barangay) ? DBNull.Value : (object)model.Barangay);
                command.Parameters.AddWithValue("@StreetName", string.IsNullOrEmpty(model.StreetName) ? DBNull.Value : (object)model.StreetName);
                command.Parameters.AddWithValue("@Building", string.IsNullOrEmpty(model.Building) ? DBNull.Value : (object)model.Building);
                command.Parameters.AddWithValue("@HouseNo", string.IsNullOrEmpty(model.HouseNo) ? DBNull.Value : (object)model.HouseNo);
                command.Parameters.AddWithValue("@IsDefault", model.IsDefault);

                await connection.OpenAsync();
                var newAccountId = await command.ExecuteScalarAsync();
            }
        }

        TempData["SuccessMessage"] = "Payout account added successfully";

        await _sellerNotificationService.CreateAsync(new SellerNotification
        {
            RecipientType = "seller",
            RecipientId = sellerId.Value.ToString(),
            SellerId = sellerId.Value,
            Category = "system",
            Type = "system.payout_account_added",
            Title = "Payout Account Added",
            Message = "A new payout account was added to your seller profile.",
            Priority = "medium",
            DeliveryMode = "in_app",
            LinkType = "payout",
            LinkTarget = "/Dashboard/PayoutAccounts",
            ActionRequired = false,
            DeduplicationKey = $"system.payout_account_added:{sellerId.Value}:{model.AccountType}:{model.AccountNumber ?? model.CardNumber ?? model.EwalletAccountNumber ?? "unknown"}"
        });

        return RedirectToAction("PayoutAccounts");
    }
    catch (Exception ex)
    {
        ViewBag.ErrorMessage = $"Error adding account: {ex.Message}";
        return View(model);
    }
}
    // ============== WITHDRAW (GET) ==============
    public async Task<IActionResult> Withdraw()
    {
        var redirect = RedirectIfNotLoggedIn();
        if (redirect != null) return redirect;

        var sellerId = GetSellerIdFromSession();
        if (sellerId == null) return RedirectToAction("Login", "Account");

        var model = await GetWithdrawalDetails(sellerId.Value);
        return View(model);
    }

    private async Task<WithdrawalDetailsViewModel> GetWithdrawalDetails(int sellerId)
    {
        var model = new WithdrawalDetailsViewModel();

        using (var connection = new SqlConnection(GetConnectionString()))
        {
            using (var command = new SqlCommand("sp_GetWithdrawalDetails", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@SellerId", sellerId);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Read seller info and wallet balance
                    if (await reader.ReadAsync())
                    {
                        model.SellerName = reader["SellerName"]?.ToString() ?? "Seller";
                        model.AvailableBalance = reader["AvailableBalance"] != DBNull.Value 
                            ? Convert.ToDecimal(reader["AvailableBalance"]) : 0;
                        model.PendingBalance = reader["PendingBalance"] != DBNull.Value 
                            ? Convert.ToDecimal(reader["PendingBalance"]) : 0;
                        model.TotalEarned = reader["TotalEarned"] != DBNull.Value 
                            ? Convert.ToDecimal(reader["TotalEarned"]) : 0;
                        model.TotalWithdrawn = reader["TotalWithdrawn"] != DBNull.Value 
                            ? Convert.ToDecimal(reader["TotalWithdrawn"]) : 0;
                    }

                    // Read payout accounts
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            model.PayoutAccounts.Add(new PayoutAccountViewModel
                            {
                                AccountId = reader["AccountId"] != DBNull.Value ? Convert.ToInt32(reader["AccountId"]) : 0,
                                AccountType = reader["AccountType"]?.ToString() ?? "",
                                AccountName = reader["AccountName"]?.ToString() ?? "",
                                AccountNumber = reader["AccountNumber"]?.ToString() ?? "",
                                BankName = reader["BankName"]?.ToString(),
                                IsDefault = reader["IsDefault"] != DBNull.Value && Convert.ToBoolean(reader["IsDefault"])
                            });
                        }
                    }

                    // Read recent withdrawals
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            model.RecentWithdrawals.Add(new RecentWithdrawalViewModel
                            {
                                WithdrawalId = reader["WithdrawalId"] != DBNull.Value ? Convert.ToInt64(reader["WithdrawalId"]) : 0,
                                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                                Status = reader["Status"]?.ToString() ?? "",
                                RequestedAt = reader["RequestedAt"] != DBNull.Value ? Convert.ToDateTime(reader["RequestedAt"]) : DateTime.Now
                            });
                        }
                    }
                }
            }
        }

        return model;
    }

    // ============== PROCESS WITHDRAWAL (POST) ==============
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessWithdrawal(WithdrawalRequestModel model)
    {
        var redirect = RedirectIfNotLoggedIn();
        if (redirect != null) return redirect;

        var sellerId = GetSellerIdFromSession();
        if (sellerId == null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
        {
            var details = await GetWithdrawalDetails(sellerId.Value);
            return View("Withdraw", details);
        }

        try
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var command = new SqlCommand("sp_ProcessWithdrawal", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@SellerId", sellerId.Value);
                    command.Parameters.AddWithValue("@Amount", model.Amount);
                    command.Parameters.AddWithValue("@PayoutAccountId", model.PayoutAccountId);

                    var refNoParam = new SqlParameter("@ReferenceNo", SqlDbType.NVarChar, 50)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(refNoParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    string referenceNo = refNoParam.Value?.ToString() ?? "";
                    TempData["SuccessMessage"] = $"Withdrawal request submitted successfully. Reference: {referenceNo}";

                    await _sellerNotificationService.CreateAsync(new SellerNotification
                    {
                        RecipientType = "seller",
                        RecipientId = sellerId.Value.ToString(),
                        SellerId = sellerId.Value,
                        Category = "payout",
                        Type = "payout.withdrawal_requested",
                        Title = "Withdrawal Requested",
                        Message = $"Your withdrawal request for PHP {model.Amount:N2} has been submitted for processing.",
                        Priority = "high",
                        DeliveryMode = "realtime,email,in_app",
                        LinkType = "payout",
                        LinkTarget = "/Dashboard/Withdraw",
                        ActionRequired = false,
                        DeduplicationKey = string.IsNullOrWhiteSpace(referenceNo) ? null : $"payout.withdrawal_requested:{referenceNo}"
                    });
                    
                    return RedirectToAction("Finance");
                }
            }
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error processing withdrawal: {ex.Message}";
        }

        var withdrawalDetails = await GetWithdrawalDetails(sellerId.Value);
        return View("Withdraw", withdrawalDetails);
    }

    // ============== WITHDRAWAL HISTORY ==============
    public async Task<IActionResult> WithdrawalHistory(int page = 1, string? status = null)
    {
        var redirect = RedirectIfNotLoggedIn();
        if (redirect != null) return redirect;

        var sellerId = GetSellerIdFromSession();
        if (sellerId == null) return RedirectToAction("Login", "Account");

        var model = await GetWithdrawalHistory(sellerId.Value, page, 10, status);
        return View(model);
    }

    private async Task<WithdrawalHistoryViewModel> GetWithdrawalHistory(int sellerId, int pageNumber, int pageSize, string? status = null)
    {
        var model = new WithdrawalHistoryViewModel
        {
            CurrentPage = pageNumber,
            PageSize = pageSize,
            FilterStatus = status
        };

        using (var connection = new SqlConnection(GetConnectionString()))
        {
            using (var command = new SqlCommand("sp_GetWithdrawalHistory", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@SellerId", sellerId);
                command.Parameters.AddWithValue("@PageNumber", pageNumber);
                command.Parameters.AddWithValue("@PageSize", pageSize);
                
                if (!string.IsNullOrEmpty(status))
                    command.Parameters.AddWithValue("@Status", status);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Read total count
                    if (await reader.ReadAsync())
                    {
                        model.TotalCount = reader["TotalCount"] != DBNull.Value 
                            ? Convert.ToInt32(reader["TotalCount"]) : 0;
                    }

                    // Read withdrawals
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            model.Withdrawals.Add(new WithdrawalHistoryItem
                            {
                                WithdrawalId = reader["WithdrawalId"] != DBNull.Value ? Convert.ToInt64(reader["WithdrawalId"]) : 0,
                                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                                Status = reader["Status"]?.ToString() ?? "",
                                RequestedAt = reader["RequestedAt"] != DBNull.Value ? Convert.ToDateTime(reader["RequestedAt"]) : DateTime.Now,
                                ProcessedAt = reader["ProcessedAt"] != DBNull.Value ? Convert.ToDateTime(reader["ProcessedAt"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }
        }

        return model;
    }
        // ============== ANALYTICS ==============
        public async Task<IActionResult> Analytics(CancellationToken cancellationToken)
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;
            
            return View(await BuildSellerDashboardModelAsync(cancellationToken));
        }

        // ============== HELP CENTER ==============
        public IActionResult HelpCenter()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            ViewData["DashboardHeading"] = "Help Center";
            return View("~/Views/Dashboard/HelpCenter.cshtml");
        }

        public IActionResult HelpCenterDrawer()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;

            return View("~/Views/Dashboard/HelpCenterDrawer.cshtml");
        }

        // ============== ACCOUNT SETTINGS ==============
        public IActionResult AccountSettings()
        {
            var redirect = RedirectIfNotLoggedIn();
            if (redirect != null) return redirect;
            
            return View();
        }

        // ============== HELPER METHODS ==============
        private async Task<int> GetOrderCountByStatusAsync(int sellerId, string status, CancellationToken cancellationToken)
        {
            return await _orderService.CountOrdersBySellerFacingStatusAsync(sellerId, status, cancellationToken);
        }


        private async Task<int> GetReturnRequestCountAsync(int sellerId, CancellationToken cancellationToken)
        {
            return await _context.ReturnRequests
                .AsNoTracking()
                .CountAsync(r => r.SellerId == sellerId && r.Status == "Return Requested", cancellationToken);
        }
        private async Task<int> GetLowStockCountAsync(int sellerId, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(GetConnectionString());
            var query = @"SELECT COUNT(*) FROM dbo.ProductVariants v
                          JOIN dbo.Products p ON p.ProductId = v.ProductId
                          WHERE p.seller_id = @SellerId AND v.Quantity <= 5 AND v.Quantity > 0";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@SellerId", sellerId);
            await connection.OpenAsync(cancellationToken);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        private async Task<decimal> GetAvailableBalance(int sellerId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var query = "SELECT Available_Balance FROM seller_wallet WHERE Seller_Id = @SellerId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SellerId", sellerId);
                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                }
            }
        }

        private async Task<string> GetSellerName(int sellerId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var query = "SELECT business_name FROM sellers WHERE seller_id = @SellerId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SellerId", sellerId);
                    await connection.OpenAsync();
                    return (await command.ExecuteScalarAsync())?.ToString() ?? "Seller";
                }
            }
        }

        private async Task<int?> GetSellerIdByEmail(string email)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                // Use 'sellers' table (plural)
                var query = "SELECT seller_id FROM sellers WHERE business_email = @Email";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }
            return null;
        }
        // A small class to catch the data from your JavaScript fetch request
public class DeclineRequest
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
[HttpPost]
public async Task<IActionResult> DeclineOrder([FromBody] DeclineRequest request)
{
    // 1. Get the Seller ID from the session
    var sellerId = HttpContext.Session.GetInt32("SellerId");
    if (sellerId == null) return Unauthorized(new { message = "Please log in." });

    // 2. Find the order using your OrderService
    var order = await _orderService.GetOrderByIdAsync(request.OrderId, sellerId.Value);

    if (order == null)
    {
        return NotFound(new { message = "Order not found." });
    }

    // --- NEW: START CLEANUP LOGIC ---
    // If there is an existing proof of shipment, delete the file from the server
    if (!string.IsNullOrEmpty(order.ProofOfShipmentUrl))
    {
        try
        {
            // Convert the relative URL (/uploads/receipts/file.jpg) back to a physical path
            string relativePath = order.ProofOfShipmentUrl.TrimStart('/');
            string fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            // Check if the file actually exists on the server before trying to delete it
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            // Log the error (optional) but allow the cancellation to continue
            Console.WriteLine($"Failed to delete file: {ex.Message}");
        }
    }
    // --- END CLEANUP LOGIC ---

    // 3. Update the status, the reason, and clear the image URL
    order.Status = "Cancelled";
    order.CancellationReason = request.Reason; 
    order.ProofOfShipmentUrl = null; // Ensure the DB link is removed even if file deletion fails

    // 4. Save to database using your service
    await _orderService.UpdateOrderAsync(order);

    return Ok(new { message = "Order declined successfully and files cleaned up." });
}

        private async Task<List<Order>> GetRecentOrdersAsync(int sellerId, int top, CancellationToken cancellationToken)
        {
            var orders = new List<Order>();
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand("sp_GetSellerRecentOrders", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@SellerId", sellerId);
            command.Parameters.AddWithValue("@Top", top);
            await connection.OpenAsync(cancellationToken);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(new Order
                {
                    OrderID      = reader["OrderId"] != DBNull.Value ? Convert.ToInt32(reader["OrderId"]) : 0,
                    FullName     = reader["Customer"].ToString() ?? string.Empty,
                    ProductName  = reader["ProductName"].ToString() ?? string.Empty,
                    ProductImage = reader["ProductImage"].ToString() ?? string.Empty,
                    Size         = reader["Size"].ToString() ?? string.Empty,
                    Sku          = reader["Sku"].ToString() ?? string.Empty,
                    OrderDate    = reader["DateTime"] != DBNull.Value ? Convert.ToDateTime(reader["DateTime"]) : DateTime.Now,
                    Courier      = reader["Courier"].ToString() ?? string.Empty,
                    Status       = reader["Status"].ToString() ?? string.Empty,
                    Amount       = reader["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmount"]) : 0
                });
            }

            await _orderService.ApplySellerFacingStatusesAsync(sellerId, orders, cancellationToken);
            return orders;
        }
        private async Task<SellerDashboardViewModel> BuildSellerDashboardModelAsync(CancellationToken cancellationToken = default)
        {
            var sellerEmail = HttpContext.Session.GetString("SellerEmail");
            var sellerIdFromSession = HttpContext.Session.GetInt32("SellerId");
            var sellerNameFromSession = HttpContext.Session.GetString("SellerName");

            SellerContextInfo sellerContext;
            if (sellerIdFromSession is > 0)
            {
                sellerContext = await _sellerContextService.ResolveSellerByIdAsync(sellerIdFromSession.Value, cancellationToken);

                if (sellerContext.SellerId <= 0)
                {
                    sellerContext = new SellerContextInfo
                    {
                        SellerId = sellerIdFromSession.Value,
                        SellerName = string.IsNullOrWhiteSpace(sellerNameFromSession) ? "Seller" : sellerNameFromSession
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(sellerEmail))
            {
                sellerContext = await _sellerContextService.ResolveSellerAsync(sellerEmail, cancellationToken);
            }
            else
            {
                sellerContext = await _sellerContextService.ResolveSellerAsync(null, cancellationToken);
            }

            if (sellerContext.SellerId > 0)
            {
                HttpContext.Session.SetInt32("SellerId", sellerContext.SellerId);
                HttpContext.Session.SetString("SellerName", string.IsNullOrWhiteSpace(sellerContext.SellerName) ? "Seller" : sellerContext.SellerName);
            }

            var now = DateTime.Now;
            var performance = await _sellerPerformanceService.GetSellerPerformanceAsync(
                sellerContext.SellerId,
                now,
                cancellationToken);
            var operationsSummary = await _sellerPerformanceService.GetOperationsSummaryAsync(
                sellerContext.SellerId,
                now,
                cancellationToken);
            var analyticsSummary = await _sellerPerformanceService.GetAnalyticsSummaryAsync(
                sellerContext.SellerId,
                now,
                cancellationToken);
            var revenueYears = await _sellerPerformanceService.GetAvailableRevenueYearsAsync(
                sellerContext.SellerId,
                cancellationToken);
            var monthlyRevenue = await _sellerPerformanceService.GetMonthlyRevenueByYearAsync(
                sellerContext.SellerId,
                revenueYears.Length > 0 ? revenueYears : new[] { now.Year },
                cancellationToken);
            var monthlyOrders = await _sellerPerformanceService.GetMonthlyOrdersByYearAsync(
                sellerContext.SellerId,
                revenueYears.Length > 0 ? revenueYears : new[] { now.Year },
                cancellationToken);
            var monthlyUnits = await _sellerPerformanceService.GetMonthlyUnitsByYearAsync(
                sellerContext.SellerId,
                revenueYears.Length > 0 ? revenueYears : new[] { now.Year },
                cancellationToken);
            var topProducts = await _sellerPerformanceService.GetTopPerformingProductsAsync(
                sellerContext.SellerId,
                topCount: 10,
                cancellationToken: cancellationToken);
            var topCategories = await _sellerPerformanceService.GetTopCategoriesAsync(
                sellerContext.SellerId,
                topCount: 5,
                cancellationToken: cancellationToken);

            var todayRange = _sellerPerformanceService.GetTopPerformingProductsAsync(sellerContext.SellerId, topCount: 10, from: now.Date, cancellationToken: cancellationToken);
            var range7D = _sellerPerformanceService.GetTopPerformingProductsAsync(sellerContext.SellerId, topCount: 10, from: now.AddDays(-7), cancellationToken: cancellationToken);
            var range30D = _sellerPerformanceService.GetTopPerformingProductsAsync(sellerContext.SellerId, topCount: 10, from: now.AddDays(-30), cancellationToken: cancellationToken);
            await Task.WhenAll(todayRange, range7D, range30D);
            var recentOrders = sellerContext.SellerId > 0
                ? await GetRecentOrdersAsync(sellerContext.SellerId, 5, cancellationToken)
                : new List<Order>();
            var notifications = await BuildDashboardNotificationsAsync(sellerContext.SellerId, cancellationToken);
            return new SellerDashboardViewModel
            {
                SellerName = sellerContext.SellerName,
                CurrentDate = DateTime.Now,

                ReturnRequests = await GetReturnRequestCountAsync(sellerContext.SellerId, cancellationToken),
                PendingOrders = await GetOrderCountByStatusAsync(sellerContext.SellerId, "Pending", cancellationToken),
                LowStockAlerts = await GetLowStockCountAsync(sellerContext.SellerId, cancellationToken),
                WithdrawAmount = 15400.00m,
                WithdrawStatus = "Processing",

                TodayOrderValue = operationsSummary.TodayOrderValue,
                YesterdayOrderValue = operationsSummary.YesterdayOrderValue,
                TodaySales = performance.RecognizedRevenueToday,
                TodayOrderCount = operationsSummary.TodayOrderCount,
                TodayUnitsSold = operationsSummary.TodayUnitsSold,
                ShippedTodayCount = operationsSummary.ShippedTodayCount,
                InFulfillmentCount = operationsSummary.InFulfillmentCount,
                OpenOrderCount = operationsSummary.OpenOrderCount,
                TotalUnitsSold = operationsSummary.TotalUnitsSold,
                TotalOrders = operationsSummary.TotalOrderCount,
                SalesGrowth = performance.SalesGrowth,
                TotalRevenue = performance.TotalRevenue,
                RecognizedRevenueToday = performance.RecognizedRevenueToday,
                YesterdayRecognizedRevenue = performance.YesterdayRecognizedRevenue,
                AverageOrderValue = operationsSummary.TotalOrderCount > 0
                    ? decimal.Round(operationsSummary.TotalOrderValue / operationsSummary.TotalOrderCount, 2)
                    : 0m,
                RefundedRevenue = analyticsSummary.RefundedRevenue,
                NetRevenue = performance.TotalRevenue - analyticsSummary.RefundedRevenue,
                PipelineRevenue = operationsSummary.PipelineRevenue,
                CancelledOrders = analyticsSummary.CancelledOrders,
                RefundedOrders = analyticsSummary.RefundedOrders,
                ActiveReturnRequests = analyticsSummary.ActiveReturnRequests,
                TotalVisits = 423,
                MonthlyRevenueByYear = monthlyRevenue,
                MonthlyOrdersByYear = monthlyOrders,
                MonthlyUnitsByYear = monthlyUnits,
                RecentOrders = recentOrders,
                Notifications = notifications,
                TopProducts = topProducts,
                TopCategories = topCategories,
                TopProductsByRange = new Dictionary<string, List<TopSellingProduct>>
                {
                    ["TODAY"] = todayRange.Result,
                    ["7D"] = range7D.Result,
                    ["30D"] = range30D.Result,
                    ["ALL"] = topProducts,
                }
            };
        }

        private async Task<List<SellerDashboardNotificationViewModel>> BuildSellerNotificationsAsync(int sellerId, CancellationToken cancellationToken)
        {
            var notifications = new List<SellerDashboardNotificationViewModel>();
            if (sellerId <= 0)
            {
                return notifications;
            }

            var recipientId = sellerId.ToString();
            var now = DateTime.UtcNow;
            var dedupeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddNotification(
                string category,
                string type,
                string title,
                string message,
                DateTime createdAt,
                string priority,
                string deliveryMode,
                bool actionRequired,
                int? orderId,
                string? linkType,
                string? linkTarget)
            {
                var dedupeKey = $"{type}:{orderId?.ToString() ?? "none"}:{createdAt:O}";
                if (!dedupeKeys.Add(dedupeKey))
                {
                    return;
                }

                notifications.Add(new SellerDashboardNotificationViewModel
                {
                    RecipientType = "seller",
                    RecipientId = recipientId,
                    OrderId = orderId,
                    Message = message,
                    IsRead = false,
                    CreatedAt = createdAt,
                    Category = category,
                    Type = type,
                    Title = title,
                    Priority = priority,
                    ActionRequired = actionRequired,
                    DeliveryMode = deliveryMode,
                    LinkType = linkType,
                    LinkTarget = linkTarget
                });
            }

            var recentOrders = await _context.Orders
                .AsNoTracking()
                .Where(order => order.seller_id == sellerId)
                .OrderByDescending(order => order.OrderDate)
                .Take(12)
                .ToListAsync(cancellationToken);

            await _orderService.ApplySellerFacingStatusesAsync(sellerId, recentOrders, cancellationToken);

            foreach (var order in recentOrders)
            {
                var effectiveStatus = string.IsNullOrWhiteSpace(order.EffectiveStatus)
                    ? order.Status ?? string.Empty
                    : order.EffectiveStatus;
                var orderLink = Url.Action("OrderManagement", "Dashboard", new { orderId = order.OrderID }) ?? "/Dashboard/OrderManagement";

                if (order.OrderDate >= now.AddDays(-1))
                {
                    AddNotification(
                        "order",
                        "order.new_received",
                        "New Order Received",
                        $"You received a new order #{order.OrderID}. Review and confirm fulfillment details.",
                        order.OrderDate,
                        "high",
                        "realtime,email,in_app",
                        true,
                        order.OrderID,
                        "order",
                        orderLink);
                }

                if (effectiveStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "order",
                        "order.pending_seller_action",
                        "Order Needs Your Action",
                        $"Order #{order.OrderID} is waiting for your confirmation or fulfillment update.",
                        order.OrderDate,
                        "high",
                        "realtime,email,in_app",
                        true,
                        order.OrderID,
                        "order",
                        orderLink);
                    continue;
                }

                if (effectiveStatus.Equals("To Ship", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "order",
                        "order.ready_to_ship",
                        "Order Ready to Ship",
                        $"Order #{order.OrderID} is ready for shipment. Print the label and dispatch it.",
                        order.OrderDate,
                        "high",
                        "realtime,email,in_app",
                        true,
                        order.OrderID,
                        "order",
                        orderLink);
                    continue;
                }

                if (effectiveStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var cancelledByBuyer = !string.IsNullOrWhiteSpace(order.CancellationReason);
                    AddNotification(
                        "cancellation",
                        cancelledByBuyer ? "cancellation.by_buyer" : "cancellation.by_system_admin",
                        cancelledByBuyer ? "Order Canceled by Buyer" : "Order Canceled by Platform",
                        cancelledByBuyer
                            ? $"Buyer canceled order #{order.OrderID}."
                            : $"Order #{order.OrderID} was canceled by the platform or an administrator.",
                        order.OrderDate,
                        "high",
                        "realtime,email,in_app",
                        false,
                        order.OrderID,
                        "order",
                        orderLink);
                    continue;
                }

                if (effectiveStatus.Equals("Failed Delivery", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "order",
                        "order.issue_failed_processing",
                        "Order Processing Issue",
                        $"Order #{order.OrderID} could not be completed because delivery failed. Review the issue and take action.",
                        order.OrderDate,
                        "critical",
                        "realtime,email,in_app",
                        true,
                        order.OrderID,
                        "order",
                        orderLink);
                    continue;
                }

                if (effectiveStatus.Equals("Shipped", StringComparison.OrdinalIgnoreCase)
                    || effectiveStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "order",
                        "order.accepted_rejected",
                        "Order Status Updated",
                        $"Order #{order.OrderID} moved to {effectiveStatus}.",
                        order.DateShipped ?? order.OrderDate,
                        "medium",
                        "in_app",
                        false,
                        order.OrderID,
                        "order",
                        orderLink);
                }
            }

            var returnRequests = await _context.ReturnRequests
                .AsNoTracking()
                .Where(request => request.SellerId == sellerId)
                .OrderByDescending(request => request.UpdatedAt)
                .Take(10)
                .ToListAsync(cancellationToken);

            foreach (var request in returnRequests)
            {
                var activityAt = request.UpdatedAt == default ? request.CreatedAt : request.UpdatedAt;
                var orderLink = Url.Action("OrderManagement", "Dashboard", new { status = "Return" }) ?? "/Dashboard/OrderManagement?status=Return";

                if (request.Status.Equals("Return Requested", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "return",
                        "return.request_submitted",
                        "Return Request Submitted",
                        $"A return request was submitted for order #{request.OrderId}.",
                        activityAt,
                        "high",
                        "realtime,email,in_app",
                        true,
                        request.OrderId,
                        "order",
                        orderLink);
                    continue;
                }

                if (request.Status.Equals("Return Approved", StringComparison.OrdinalIgnoreCase)
                    || request.Status.Equals("Return Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "return",
                        "return.approved_rejected",
                        "Return Decision Updated",
                        $"Return for order #{request.OrderId} has been {request.Status.Replace("Return ", string.Empty).ToLowerInvariant()}.",
                        activityAt,
                        "high",
                        "realtime,in_app",
                        false,
                        request.OrderId,
                        "order",
                        orderLink);
                    continue;
                }

                if (request.Status.Equals("Item Returned", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "return",
                        "return.completed",
                        "Return Completed",
                        $"Return workflow for order #{request.OrderId} has been completed.",
                        activityAt,
                        "medium",
                        "in_app",
                        false,
                        request.OrderId,
                        "order",
                        orderLink);
                    continue;
                }

                if (request.Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase))
                {
                    AddNotification(
                        "refund",
                        "refund.completed",
                        "Refund Completed",
                        $"Refund for order #{request.OrderId} has been completed successfully.",
                        activityAt,
                        "medium",
                        "in_app",
                        false,
                        request.OrderId,
                        "order",
                        orderLink);
                }
            }

            var lowStockProducts = await _context.ProductVariants
                .AsNoTracking()
                .Where(variant => variant.Product != null && variant.Product.SellerId == sellerId)
                .GroupBy(variant => new
                {
                    variant.ProductId,
                    ProductName = variant.Product != null ? variant.Product.ProductName : string.Empty
                })
                .Select(group => new
                {
                    group.Key.ProductId,
                    group.Key.ProductName,
                    Stock = group.Sum(item => item.Quantity)
                })
                .Where(item => item.Stock <= 5)
                .OrderBy(item => item.Stock)
                .Take(5)
                .ToListAsync(cancellationToken);

            foreach (var product in lowStockProducts)
            {
                var isOutOfStock = product.Stock <= 0;
                var linkTarget = Url.Action("ViewProduct", "Seller", new { id = product.ProductId, state = "active" }) ?? $"/Seller/ViewProduct?id={product.ProductId}&state=active";

                AddNotification(
                    "inventory",
                    isOutOfStock ? "inventory.out_of_stock" : "inventory.low_stock",
                    isOutOfStock ? "Out of Stock" : "Low Stock Alert",
                    isOutOfStock
                        ? $"Product {product.ProductName} is now out of stock."
                        : $"Product {product.ProductName} is running low on stock with {product.Stock} units left.",
                    DateTime.UtcNow,
                    "high",
                    "in_app,email",
                    true,
                    null,
                    "product",
                    linkTarget);
            }

            var conversations = await _context.MessageConversations
                .AsNoTracking()
                .Where(conversation => conversation.SellerUserId == sellerId)
                .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
                .Take(10)
                .Select(conversation => new
                {
                    conversation.ConversationId,
                    conversation.OrderId,
                    conversation.SellerLastReadAt
                })
                .ToListAsync(cancellationToken);

            var conversationIds = conversations.Select(conversation => conversation.ConversationId).ToArray();
            var latestMessages = new Dictionary<int, ConversationMessage>();
            if (conversationIds.Length > 0)
            {
                var messages = await _context.ConversationMessages
                    .AsNoTracking()
                    .Where(message => conversationIds.Contains(message.ConversationId) && !message.IsDeleted)
                    .OrderByDescending(message => message.SentAt)
                    .ToListAsync(cancellationToken);

                latestMessages = messages
                    .GroupBy(message => message.ConversationId)
                    .ToDictionary(group => group.Key, group => group.First());
            }

            foreach (var conversation in conversations)
            {
                if (!latestMessages.TryGetValue(conversation.ConversationId, out var latestMessage))
                {
                    continue;
                }

                var isUnread = !conversation.SellerLastReadAt.HasValue || latestMessage.SentAt > conversation.SellerLastReadAt.Value;
                if (!isUnread || latestMessage.SenderUserId == sellerId)
                {
                    continue;
                }

                var messageLink = Url.Action(
                    "SellerMessenger",
                    "Seller",
                    new
                    {
                        conversationId = conversation.ConversationId,
                        orderId = conversation.OrderId
                    }) ?? $"/seller/messenger?conversationId={conversation.ConversationId}";

                AddNotification(
                    "message",
                    "message.new_customer_message",
                    "New Customer Message",
                    conversation.OrderId.HasValue
                        ? $"You received a new message from a customer regarding order #{conversation.OrderId.Value}."
                        : "You received a new message from a customer.",
                    latestMessage.SentAt,
                    "high",
                    "realtime,email,in_app",
                    true,
                    conversation.OrderId,
                    "message_thread",
                    messageLink);

                if (!string.IsNullOrWhiteSpace(latestMessage.AttachmentUrl)
                    || latestMessage.AttachmentData is { Length: > 0 }
                    || !string.IsNullOrWhiteSpace(latestMessage.AttachmentFileName))
                {
                    AddNotification(
                        "message",
                        "message.follow_up_attachment_alert",
                        "Message Follow-up Needed",
                        "A customer sent additional information or an attachment that needs review.",
                        latestMessage.SentAt,
                        "medium",
                        "realtime,in_app",
                        true,
                        conversation.OrderId,
                        "message_thread",
                        messageLink);
                }

                if (latestMessage.SentAt <= now.AddHours(-12))
                {
                    AddNotification(
                        "message",
                        "message.unread_conversation_alert",
                        "Unread Conversation Reminder",
                        "You have unread customer messages awaiting response.",
                        latestMessage.SentAt,
                        "medium",
                        "email,in_app",
                        true,
                        conversation.OrderId,
                        "message_thread",
                        messageLink);
                }
            }

            return notifications
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(12)
                .ToList();
        }

        private async Task<List<SellerDashboardNotificationViewModel>> BuildDashboardNotificationsAsync(int sellerId, CancellationToken cancellationToken)
        {
            var persistedNotifications = sellerId > 0
                ? await _sellerNotificationService.ListAsync(sellerId, 12, false, cancellationToken)
                : Array.Empty<SellerNotification>();

            var mappedPersisted = persistedNotifications
                .Select(notification => new SellerDashboardNotificationViewModel
                {
                    RecipientType = notification.RecipientType,
                    RecipientId = notification.RecipientId,
                    OrderId = notification.OrderId,
                    Message = notification.Message,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt,
                    Category = notification.Category,
                    Type = notification.Type,
                    Title = notification.Title,
                    Priority = notification.Priority,
                    ActionRequired = notification.ActionRequired,
                    DeliveryMode = notification.DeliveryMode,
                    LinkType = notification.LinkType,
                    LinkTarget = notification.LinkTarget
                })
                .ToList();

            var computed = await BuildSellerNotificationsAsync(sellerId, cancellationToken);
            var merged = mappedPersisted
                .Concat(computed)
                .GroupBy(notification => string.Join("|",
                    notification.Type,
                    notification.OrderId?.ToString() ?? "none",
                    notification.Title,
                    notification.CreatedAt.ToString("O")))
                .Select(group => group
                    .OrderBy(item => item.IsRead)
                    .First())
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(12)
                .ToList();

            return merged;
        }
        
    }
}








