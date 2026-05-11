using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Data.Messaging;
using MyAspNetApp.Models;
using MyAspNetApp.Models.Messaging;
using MyAspNetApp.Security;
using Microsoft.Net.Http.Headers;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.FileProviders;

namespace MyAspNetApp.Controllers.Messaging;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultConversationPageSize = 20;
    private const int DefaultMessagePageSize = 50;
    private const int MaxPageSize = 100;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private readonly IMessagingRepository _messagingRepository;
    private readonly IAuthenticatedUserContextService _authenticatedUserContextService;
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public MessagesController(
        IMessagingRepository messagingRepository,
        IAuthenticatedUserContextService authenticatedUserContextService,
        AppDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _messagingRepository = messagingRepository;
        _authenticatedUserContextService = authenticatedUserContextService;
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpPost("conversations")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<ConversationDto>> CreateOrGetConversation([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var contextType = ParseContextType(request.ContextType);
        if (!contextType.HasValue)
        {
            return BadRequest("ContextType must be either 'general' or 'order'.");
        }

        if (contextType.Value != ConversationContextType.General)
        {
            return BadRequest("Only general product conversations are supported here.");
        }

        var sellerUserId = request.SellerUserId?.Trim();
        if (!int.TryParse(sellerUserId, out var sellerUserInt) || sellerUserInt <= 0)
        {
            return BadRequest("SellerUserId must be a positive integer when ContextType is general.");
        }

        if (sellerUserInt == currentUser.UserId)
        {
            return BadRequest("Buyer and seller must be different accounts.");
        }

        if (!currentUser.ConsumerId.HasValue || currentUser.ConsumerId.Value <= 0)
        {
            return BadRequest("Current account is not linked to a consumer profile.");
        }

        var resolvedSellerUserId = await ResolveMessagingSellerIdAsync(sellerUserInt, cancellationToken);
        if (!resolvedSellerUserId.HasValue || resolvedSellerUserId.Value <= 0)
        {
            return NotFound("Seller not found.");
        }

        var summary = await _messagingRepository.CreateOrGetGeneralAsync(currentUser.ConsumerId.Value, resolvedSellerUserId.Value, cancellationToken);
        var actorView = await _messagingRepository.GetConversationAsync(summary.ConversationId, ToMessageActor(currentUser), cancellationToken);

        return actorView is null
            ? NotFound()
            : Ok(await ToConversationDtoAsync(actorView, currentUser, ConversationActorScope.Consumer, cancellationToken));
    }

    [HttpGet("conversations/resolve")]
    public async Task<ActionResult<ConversationDto>> ResolveConversation([FromQuery] ResolveConversationQuery query, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var contextType = ParseContextType(query.ContextType);
        if (!contextType.HasValue)
        {
            return BadRequest("ContextType must be either 'general' or 'order'.");
        }

        int? sellerUserInt = null;
        if (!string.IsNullOrWhiteSpace(query.SellerUserId))
        {
            if (!int.TryParse(query.SellerUserId.Trim(), out var parsedSellerUserId) || parsedSellerUserId <= 0)
            {
                return BadRequest("SellerUserId must be a positive integer when provided.");
            }

            sellerUserInt = await ResolveMessagingSellerIdAsync(parsedSellerUserId, cancellationToken) ?? parsedSellerUserId;
        }

        var conversation = await _messagingRepository.FindConversationAsync(
            ToMessageActor(currentUser),
            contextType.Value,
            sellerUserInt,
            query.OrderId,
            cancellationToken);

        if (conversation is null)
        {
            return NoContent();
        }

        return Ok(await ToConversationDtoAsync(conversation, currentUser, ConversationActorScope.Consumer, cancellationToken));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResult<ConversationDto>>> ListConversations([FromQuery] ConversationListQuery query, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var scope = ParseConversationActorScope(query.Role);
        if (!scope.HasValue)
        {
            return BadRequest("Role must be either 'seller' or 'consumer' when provided.");
        }

        var page = Math.Max(query.Page ?? DefaultPageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultConversationPageSize, 1, MaxPageSize);

        var paged = await _messagingRepository.ListByActorAsync(ToMessageActor(currentUser), scope.Value, page, pageSize, cancellationToken);
        var items = await ToConversationDtosAsync(paged.Items, currentUser, scope.Value, cancellationToken);

        return Ok(new PagedResult<ConversationDto>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Items = items,
        });
    }

    [HttpGet("conversations/{conversationId:int}")]
    public async Task<ActionResult<ConversationDto>> GetConversation(int conversationId, [FromQuery] string? role, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var scope = ParseConversationActorScope(role);
        if (!scope.HasValue)
        {
            return BadRequest("Role must be either 'seller' or 'consumer' when provided.");
        }

        var conversation = await _messagingRepository.GetConversationAsync(conversationId, ToMessageActor(currentUser), cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(await ToConversationDtoAsync(conversation, currentUser, scope.Value, cancellationToken));
    }

    [HttpPost("conversations/{conversationId:int}/messages")]
    [ConditionalValidateAntiForgeryToken]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MessageDto>> SendMessage(int conversationId, [FromForm] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var body = request.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body) && request.Attachment is null)
        {
            return BadRequest("Either Body or Attachment is required.");
        }

        if (body.Length > 2000)
        {
            return BadRequest("Body must be 2000 characters or less.");
        }

        byte[]? attachmentData = null;
        string? attachmentContentType = null;
        string? attachmentFileName = null;
        if (request.Attachment is not null)
        {
            if (!IsValidAttachment(request.Attachment))
            {
                return BadRequest("Attachment must be a valid image or video (jpg, jpeg, png, webp, mp4, webm, mov) and 5MB or smaller.");
            }

            await using var attachmentStream = new MemoryStream();
            await request.Attachment.CopyToAsync(attachmentStream, cancellationToken);
            attachmentData = attachmentStream.ToArray();
            attachmentContentType = string.IsNullOrWhiteSpace(request.Attachment.ContentType)
                ? "application/octet-stream"
                : request.Attachment.ContentType.Trim();
            attachmentFileName = string.IsNullOrWhiteSpace(request.Attachment.FileName)
                ? null
                : Path.GetFileName(request.Attachment.FileName);
        }

        var message = await _messagingRepository.SendMessageAsync(
            conversationId,
            ToMessageActor(currentUser),
            body,
            attachmentData,
            attachmentContentType,
            attachmentFileName,
            cancellationToken);

        if (message is null)
        {
            return NotFound();
        }

        return Ok(ToMessageDto(message));
    }

    [HttpGet("conversations/{conversationId:int}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> ListMessages(int conversationId, [FromQuery] MessageListQuery query, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var pageSize = Math.Clamp(query.PageSize ?? DefaultMessagePageSize, 1, MaxPageSize);
        var messages = await _messagingRepository.ListMessagesAsync(
            conversationId,
            ToMessageActor(currentUser),
            query.Before,
            pageSize,
            cancellationToken);

        return messages is null
            ? NotFound()
            : Ok(messages.Select(ToMessageDto).ToList());
    }

    [HttpGet("storefront/faqs")]
    public async Task<ActionResult<IReadOnlyList<StorefrontFaqDto>>> GetStorefrontFaqs([FromQuery] string? sellerUserId, CancellationToken cancellationToken)
    {
        return Ok(await LoadStorefrontFaqsAsync(cancellationToken));
    }

    [HttpGet("storefront/ordered-products")]
    public async Task<ActionResult<IReadOnlyList<StorefrontOrderedProductDto>>> GetStorefrontOrderedProducts([FromQuery] string? sellerUserId, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (!currentUser.ConsumerId.HasValue || currentUser.ConsumerId.Value <= 0)
        {
            return BadRequest("Current account is not linked to a consumer profile.");
        }

        if (!int.TryParse(sellerUserId?.Trim(), out var parsedSellerUserId) || parsedSellerUserId <= 0)
        {
            return Ok(Array.Empty<StorefrontOrderedProductDto>());
        }

        try
        {
            var resolvedSellerUserId = await ResolveMessagingSellerIdAsync(parsedSellerUserId, cancellationToken) ?? parsedSellerUserId;
            return Ok(await LoadStorefrontOrderedProductsAsync(currentUser.ConsumerId.Value, currentUser.UserId, resolvedSellerUserId, cancellationToken));
        }
        catch (DbException)
        {
            return Ok(Array.Empty<StorefrontOrderedProductDto>());
        }
        catch (InvalidOperationException)
        {
            return Ok(Array.Empty<StorefrontOrderedProductDto>());
        }
    }

    [HttpPost("storefront/faqs/click")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> ClickStorefrontFaq([FromBody] StorefrontFaqClickRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (!currentUser.ConsumerId.HasValue || currentUser.ConsumerId.Value <= 0)
        {
            return BadRequest("Current account is not linked to a consumer profile.");
        }

        if (request is null || request.FaqId <= 0)
        {
            return BadRequest("FaqId is required.");
        }

        var faq = await LoadStorefrontFaqAsync(request.FaqId, cancellationToken);
        if (faq is null)
        {
            return NotFound("FAQ not found.");
        }

        return Ok(new
        {
            conversationId = 0,
            currentUserId = currentUser.UserId.ToString(),
            messages = new List<MessageDto>
            {
                CreateFaqMessageDto(-request.FaqId * 2L, 0, currentUser.UserId, faq.Question),
                CreateFaqMessageDto((-request.FaqId * 2L) + 1, 0, 0, faq.Answer)
            }
        });
    }

    [HttpGet("attachments/{messageId:long}")]
    public async Task<IActionResult> GetAttachment(long messageId, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var attachment = await _dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.MessageId == messageId
                && !message.IsDeleted
                && message.AttachmentData != null
                && (message.Conversation.BuyerUserId == currentUser.ConsumerId
                    || message.Conversation.SellerUserId == currentUser.UserId))
            .Select(message => new
            {
                message.AttachmentData,
                message.AttachmentContentType,
                message.AttachmentFileName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment?.AttachmentData is null || attachment.AttachmentData.Length == 0)
        {
            return NotFound();
        }

        var contentType = string.IsNullOrWhiteSpace(attachment.AttachmentContentType)
            ? "application/octet-stream"
            : attachment.AttachmentContentType.Trim();

        var fileName = string.IsNullOrWhiteSpace(attachment.AttachmentFileName)
            ? $"attachment-{messageId}"
            : attachment.AttachmentFileName.Trim();

        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = fileName
        }.ToString();

        return File(attachment.AttachmentData, contentType);
    }

    [HttpGet("sellers/{sellerUserId:int}/avatar")]
    public async Task<IActionResult> GetSellerAvatar(int sellerUserId, CancellationToken cancellationToken)
    {
        if (sellerUserId <= 0)
        {
            return NotFound();
        }

        await using var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var availableColumns = await LoadSellerColumnLookupAsync(connection, cancellationToken);
            var candidatePathColumn = FindColumn(availableColumns, "logo_path");
            var candidateDataColumn = FindColumn(availableColumns, "logo_data", "logodata");
            var candidateContentTypeColumn = FindColumn(availableColumns, "logo_content_type", "logocontenttype");

            if (candidatePathColumn is null && candidateDataColumn is null)
            {
                return NotFound();
            }

            var selectColumns = new List<string>();
            if (candidatePathColumn is not null)
            {
                selectColumns.Add($"[{candidatePathColumn}] AS LogoPath");
            }

            if (candidateDataColumn is not null)
            {
                selectColumns.Add($"[{candidateDataColumn}] AS LogoData");
            }

            if (candidateContentTypeColumn is not null)
            {
                selectColumns.Add($"[{candidateContentTypeColumn}] AS LogoContentType");
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT TOP (1) {string.Join(", ", selectColumns)}
                FROM dbo.Sellers
                WHERE user_id = @SellerUserID OR seller_id = @SellerUserID;
                """;
            command.CommandType = CommandType.Text;
            AddParameter(command, "@SellerUserID", sellerUserId, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return NotFound();
            }

            if (candidateDataColumn is not null)
            {
                var logoDataOrdinal = reader.GetOrdinal("LogoData");
                if (!reader.IsDBNull(logoDataOrdinal))
                {
                    var logoData = (byte[])reader.GetValue(logoDataOrdinal);
                    if (logoData.Length > 0)
                    {
                        var contentType = "image/jpeg";
                        if (candidateContentTypeColumn is not null)
                        {
                            var contentTypeOrdinal = reader.GetOrdinal("LogoContentType");
                            if (!reader.IsDBNull(contentTypeOrdinal))
                            {
                                var dbContentType = reader.GetString(contentTypeOrdinal);
                                if (!string.IsNullOrWhiteSpace(dbContentType))
                                {
                                    contentType = dbContentType.Trim();
                                }
                            }
                        }

                        return File(logoData, contentType);
                    }
                }
            }

            if (candidatePathColumn is not null)
            {
                var pathOrdinal = reader.GetOrdinal("LogoPath");
                if (!reader.IsDBNull(pathOrdinal))
                {
                    var logoPath = reader.GetString(pathOrdinal);
                    var physicalPath = ResolveSellerLogoPhysicalPath(logoPath);
                    if (!string.IsNullOrWhiteSpace(physicalPath) && System.IO.File.Exists(physicalPath))
                    {
                        var contentType = GetImageContentType(physicalPath);
                        return PhysicalFile(physicalPath, contentType);
                    }
                }
            }

            return NotFound();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [HttpPost("conversations/{conversationId:int}/read")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int conversationId, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var updated = await _messagingRepository.MarkReadAsync(conversationId, ToMessageActor(currentUser), cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("messages/{messageId:long}")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(long messageId, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var deleted = await _messagingRepository.SoftDeleteMessageAsync(messageId, currentUser.UserId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static bool IsValidAttachment(IFormFile attachment)
    {
        if (attachment.Length <= 0 || attachment.Length > 5 * 1024 * 1024)
        {
            return false;
        }

        var extension = Path.GetExtension(attachment.FileName);
        return AllowedExtensions.Contains(extension);
    }

    private static ConversationContextType? ParseContextType(string contextType)
    {
        if (string.Equals(contextType?.Trim(), "general", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationContextType.General;
        }

        if (string.Equals(contextType?.Trim(), "order", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationContextType.Order;
        }

        return null;
    }

    private static ConversationActorScope? ParseConversationActorScope(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return ConversationActorScope.Consumer;
        }

        if (string.Equals(role.Trim(), "seller", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationActorScope.Seller;
        }

        if (string.Equals(role.Trim(), "consumer", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationActorScope.Consumer;
        }

        return null;
    }

    private static MessageActorContext ToMessageActor(AuthenticatedUserContext currentUser)
        => new(currentUser.UserId, currentUser.ConsumerId, currentUser.SellerId);

    private sealed class StorefrontVariantInfo
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public decimal? Price { get; init; }
        public int Quantity { get; init; }
        public string? ImagePath { get; init; }
        public bool HasImageData { get; init; }
    }

    private async Task<int?> ResolveMessagingSellerIdAsync(int sellerIdentifier, CancellationToken cancellationToken)
    {
        var aliases = await ResolveMessagingSellerAliasesAsync(sellerIdentifier, cancellationToken);
        if (aliases.Length == 0)
        {
            return null;
        }

        var canonicalSellerId = await _dbContext.Products
            .AsNoTracking()
            .Where(product => aliases.Contains(product.SellerId))
            .Select(product => (int?)product.SellerId)
            .FirstOrDefaultAsync(cancellationToken);

        return canonicalSellerId ?? aliases[0];
    }

    private async Task<int[]> ResolveMessagingSellerAliasesAsync(int sellerIdentifier, CancellationToken cancellationToken)
    {
        if (sellerIdentifier <= 0)
        {
            return Array.Empty<int>();
        }

        var directProductSellerId = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerIdentifier)
            .Select(product => (int?)product.SellerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (directProductSellerId.HasValue)
        {
            return new[] { directProductSellerId.Value };
        }

        var sellerProfile = await _dbContext.Sellers
            .AsNoTracking()
            .Where(seller => seller.SellerId == sellerIdentifier || seller.UserId == sellerIdentifier)
            .OrderByDescending(seller => seller.SellerId == sellerIdentifier)
            .ThenBy(seller => seller.SellerId)
            .Select(seller => new { seller.SellerId, seller.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (sellerProfile is null)
        {
            return Array.Empty<int>();
        }

        var aliases = new[] { sellerProfile.SellerId, sellerProfile.UserId }
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (aliases.Length == 0)
        {
            return Array.Empty<int>();
        }

        return aliases;
    }

    private async Task<List<StorefrontFaqDto>> LoadStorefrontFaqsAsync(CancellationToken cancellationToken)
    {
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                FaqID,
                Question,
                Answer,
                COALESCE(NULLIF(LTRIM(RTRIM(Category)), N''), N'General') AS Category
            FROM dbo.FAQs
            WHERE UserType = N'consumer'
            ORDER BY Category, FaqID;
            """;

        var items = new List<StorefrontFaqDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StorefrontFaqDto
            {
                FaqId = reader.GetInt32(0),
                Question = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Answer = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Category = reader.IsDBNull(3) ? "General" : reader.GetString(3),
            });
        }

        return items;
    }

    private async Task<StorefrontFaqDto?> LoadStorefrontFaqAsync(int faqId, CancellationToken cancellationToken)
    {
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TOP (1)
                FaqID,
                Question,
                Answer,
                COALESCE(NULLIF(LTRIM(RTRIM(Category)), N''), N'General') AS Category
            FROM dbo.FAQs
            WHERE FaqID = @FaqID
              AND UserType = N'consumer';
            """;
        AddParameter(command, "@FaqID", faqId, DbType.Int32);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StorefrontFaqDto
        {
            FaqId = reader.GetInt32(0),
            Question = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Answer = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Category = reader.IsDBNull(3) ? "General" : reader.GetString(3),
        };
    }

    private async Task<List<StorefrontOrderedProductDto>> LoadStorefrontOrderedProductsAsync(int consumerId, int currentUserId, int sellerUserId, CancellationToken cancellationToken)
    {
        // Fetch seller's active products directly from the database by seller_id
        try
        {
            var sellerProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.SellerId == sellerUserId && p.Status == "active")
                .OrderBy(p => p.ProductId)
                .Take(24)
                .Select(p => new StorefrontOrderedProductDto
                {
                    ProductId = p.ProductId,
                    OrderId = 0,
                    ProductName = string.IsNullOrWhiteSpace(p.ProductName) ? $"Product #{p.ProductId}" : p.ProductName,
                    ProductUrl = $"/Home/Product?id={p.ProductId}",
                    StoreUrl = $"/Home/SellerShop?id={sellerUserId}",
                    SellerUserId = sellerUserId.ToString(),
                })
                .ToListAsync(cancellationToken);

            if (sellerProducts.Count > 0)
            {
                return await EnrichStorefrontOrderedProductsAsync(sellerProducts, cancellationToken);
            }
        }
        catch (DbException)
        {
            // Fall through to in-memory fallback if database query fails
        }
        catch (InvalidOperationException)
        {
            // Fall through to in-memory fallback if query operation fails
        }

        // Fallback: return mock products from ProductData.Orders based on seller
        var sellerAliases = await ResolveMessagingSellerAliasesAsync(sellerUserId, cancellationToken);
        if (sellerAliases.Length == 0)
        {
            sellerAliases = new[] { sellerUserId };
        }

        var purchaseHistoryItems = ProductData.Orders
            .Where(order => order?.OrderItems != null && order.OrderItems.Count > 0)
            .OrderByDescending(order => order.CreatedAt)
            .SelectMany(order => order.OrderItems
                .Where(item => item != null
                    && item.ProductId > 0
                    && (sellerAliases.Contains(item.SellerId) || item.SellerId == sellerUserId))
                .Select(item => new
                {
                    order.CreatedAt,
                    Product = new StorefrontOrderedProductDto
                    {
                        ProductId = item.ProductId,
                        OrderId = 0,
                        ProductName = string.IsNullOrWhiteSpace(item.Name) ? $"Product #{item.ProductId}" : item.Name,
                        ImageUrl = string.IsNullOrWhiteSpace(item.Image) ? "/images/placeholder.png" : item.Image,
                        Price = item.Price > 0 ? item.Price : null,
                        Stock = Math.Max(item.Quantity, 0),
                        ProductUrl = $"/Home/Product?id={item.ProductId}",
                        StoreUrl = $"/Home/SellerShop?id={sellerUserId}",
                        SellerUserId = sellerUserId.ToString(),
                    }
                }))
            .GroupBy(entry => entry.Product.ProductId)
            .Select(group => group
                .OrderByDescending(entry => entry.CreatedAt)
                .Select(entry => entry.Product)
                .First())
            .Take(24)
            .ToList();

        return purchaseHistoryItems;
    }

    private async Task<List<StorefrontOrderedProductDto>> EnrichStorefrontOrderedProductsAsync(
        List<StorefrontOrderedProductDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var productIds = items
            .Select(item => item.ProductId)
            .Where(productId => productId > 0)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
        {
            return items;
        }

        List<StorefrontVariantInfo> variantRows;
        try
        {
            variantRows = await _dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId))
                .OrderBy(variant => variant.Id)
                .Select(variant => new StorefrontVariantInfo
                {
                    Id = variant.Id,
                    ProductId = variant.ProductId,
                    Price = variant.Price,
                    Quantity = variant.Quantity,
                    ImagePath = variant.ImagePath,
                    HasImageData = variant.ImageData != null
                })
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            variantRows = new List<StorefrontVariantInfo>();
        }
        catch (InvalidOperationException)
        {
            variantRows = new List<StorefrontVariantInfo>();
        }

        var variantsByProductId = variantRows
            .GroupBy(variant => variant.ProductId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return items
            .Select(item =>
            {
                variantsByProductId.TryGetValue(item.ProductId, out var variants);
                var firstVariant = variants?
                    .FirstOrDefault(variant => variant.HasImageData || !string.IsNullOrWhiteSpace(variant.ImagePath))
                    ?? variants?.FirstOrDefault();
                var stock = variants?.Sum(variant => Math.Max(variant.Quantity, 0)) ?? 0;
                var price = variants?
                    .Where(variant => variant.Price.HasValue && variant.Price.Value > 0)
                    .Select(variant => variant.Price)
                    .FirstOrDefault();
                var imageUrl = firstVariant is null
                    ? "/images/placeholder.png"
                    : Url.Action(nameof(ProductsController.GetVariantImage), "Products", new
                    {
                        variantId = firstVariant.Id
                    }) ?? $"/api/products/variant-image/{firstVariant.Id}";

                return new StorefrontOrderedProductDto
                {
                    ProductId = item.ProductId,
                    OrderId = item.OrderId,
                    ProductName = item.ProductName,
                    ImageUrl = imageUrl,
                    Price = price,
                    Stock = stock,
                    ProductUrl = item.ProductUrl,
                    StoreUrl = item.StoreUrl,
                    SellerUserId = item.SellerUserId,
                };
            })
            .ToList();
    }

    private async Task<List<ConversationDto>> ToConversationDtosAsync(
        IEnumerable<MessageConversationSummary> summaries,
        AuthenticatedUserContext currentUser,
        ConversationActorScope scope,
        CancellationToken cancellationToken)
    {
        var summaryList = summaries.ToList();
        if (summaryList.Count == 0)
        {
            return [];
        }

        var consumerIds = summaryList
            .Select(summary => summary.BuyerUserId)
            .Distinct()
            .ToList();

        var sellerUserIds = summaryList
            .Select(summary => summary.SellerUserId)
            .Distinct()
            .ToList();

        var consumers = await _dbContext.Consumers
            .AsNoTracking()
            .Where(consumer => consumerIds.Contains(consumer.ConsumerId))
            .ToDictionaryAsync(consumer => consumer.ConsumerId, cancellationToken);

        var userIds = consumers.Values
            .Select(consumer => consumer.UserId)
            .Concat(sellerUserIds)
            .Distinct()
            .ToList();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);

        var sellers = await _dbContext.Sellers
            .AsNoTracking()
            .Where(seller => sellerUserIds.Contains(seller.UserId) || sellerUserIds.Contains(seller.SellerId))
            .ToListAsync(cancellationToken);

        var sellerLookup = BuildSellerLookup(sellers);

        return summaryList
            .Select(summary => ToConversationDto(summary, currentUser, scope, users, consumers, sellerLookup))
            .ToList();
    }

    private async Task<ConversationDto> ToConversationDtoAsync(
        MessageConversationSummary summary,
        AuthenticatedUserContext currentUser,
        ConversationActorScope scope,
        CancellationToken cancellationToken)
    {
        var consumers = await _dbContext.Consumers
            .AsNoTracking()
            .Where(consumer => consumer.ConsumerId == summary.BuyerUserId)
            .ToDictionaryAsync(consumer => consumer.ConsumerId, cancellationToken);

        var userIds = consumers.Values
            .Select(consumer => consumer.UserId)
            .Concat(new[] { summary.SellerUserId })
            .Distinct()
            .ToList();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);

        var sellers = await _dbContext.Sellers
            .AsNoTracking()
            .Where(seller =>
                seller.UserId == summary.SellerUserId
                || seller.SellerId == summary.SellerUserId)
            .ToListAsync(cancellationToken);

        return ToConversationDto(summary, currentUser, scope, users, consumers, BuildSellerLookup(sellers));
    }

    private static ConversationDto ToConversationDto(
        MessageConversationSummary summary,
        AuthenticatedUserContext currentUser,
        ConversationActorScope scope,
        IReadOnlyDictionary<int, User> users,
        IReadOnlyDictionary<int, Consumer> consumers,
        IReadOnlyDictionary<int, SellerProfile> sellers)
    {
        var viewerIsSeller = scope == ConversationActorScope.Seller || currentUser.UserId == summary.SellerUserId;
        var contextLabel = summary.ContextType == ConversationContextType.Order
            ? $"Order #{summary.OrderId}"
            : "General inquiry";

        var counterpartyUserId = viewerIsSeller ? summary.BuyerUserId : summary.SellerUserId;
        var displayName = viewerIsSeller
            ? BuildConsumerDisplayName(summary.BuyerUserId, users, consumers)
            : BuildSellerDisplayName(summary.SellerUserId, users, sellers);
        var avatarUrl = viewerIsSeller
            ? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(displayName)}&background=111827&color=ffffff&size=128&bold=true"
            : $"/api/messages/sellers/{summary.SellerUserId}/avatar";

        return new ConversationDto
        {
            ConversationId = summary.ConversationId,
            CurrentUserId = currentUser.UserId.ToString(),
            BuyerUserId = summary.BuyerUserId.ToString(),
            SellerUserId = summary.SellerUserId.ToString(),
            DisplayName = displayName,
            DisplaySubtitle = contextLabel,
            AvatarUrl = avatarUrl,
            ContextLabel = contextLabel,
            CounterpartyRole = viewerIsSeller ? "consumer" : "seller",
            CounterpartyId = counterpartyUserId.ToString(),
            CanReply = true,
            ContextType = summary.ContextType == ConversationContextType.Order ? "order" : "general",
            OrderId = summary.OrderId,
            LastMessageAt = summary.LastMessageAt,
            BuyerLastReadAt = summary.BuyerLastReadAt,
            SellerLastReadAt = summary.SellerLastReadAt,
            LastMessagePreview = summary.LastMessagePreview,
            UnreadCount = summary.UnreadCount,
            CreatedAt = summary.CreatedAt,
            UpdatedAt = summary.UpdatedAt,
        };
    }

    private static string BuildConsumerDisplayName(
        int consumerId,
        IReadOnlyDictionary<int, User> users,
        IReadOnlyDictionary<int, Consumer> consumers)
    {
        if (consumers.TryGetValue(consumerId, out var consumer))
        {
            var fullName = string.Join(
                " ",
                new[] { consumer.FirstName, consumer.MiddleName, consumer.LastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Select(part => part!.Trim()));

            if (!string.IsNullOrWhiteSpace(consumer.Username))
            {
                return consumer.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (users.TryGetValue(consumer.UserId, out var consumerUser) && !string.IsNullOrWhiteSpace(consumerUser.Email))
            {
                return consumerUser.Email.Trim();
            }
        }

        return "Consumer";
    }

    private static string BuildSellerDisplayName(
        int sellerUserId,
        IReadOnlyDictionary<int, User> users,
        IReadOnlyDictionary<int, SellerProfile> sellers)
    {
        if (sellers.TryGetValue(sellerUserId, out var seller) && !string.IsNullOrWhiteSpace(seller.BusinessName))
        {
            return seller.BusinessName.Trim();
        }

        if (users.TryGetValue(sellerUserId, out var user) && !string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email.Trim();
        }

        return "Seller";
    }

    private static MessageDto ToMessageDto(MessageItem item)
        => new()
        {
            MessageId = item.MessageId,
            ConversationId = item.ConversationId,
            SenderUserId = item.SenderUserId.ToString(),
            Body = item.Body,
            AttachmentUrl = item.AttachmentUrl,
            AttachmentContentType = item.AttachmentContentType,
            AttachmentFileName = item.AttachmentFileName,
            SentAt = item.SentAt,
            IsDeleted = item.IsDeleted,
        };

    private static Dictionary<int, SellerProfile> BuildSellerLookup(IEnumerable<SellerProfile> sellers)
    {
        var lookup = new Dictionary<int, SellerProfile>();

        foreach (var seller in sellers)
        {
            lookup.TryAdd(seller.SellerId, seller);
            lookup.TryAdd(seller.UserId, seller);
        }

        return lookup;
    }

    private async Task<HashSet<string>> LoadSellerColumnLookupAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Sellers';
            """;
        command.CommandType = CommandType.Text;

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        return columns;
    }

    private static string? FindColumn(HashSet<string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string? ResolveSellerLogoPhysicalPath(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;
        }

        var normalized = logoPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(_environment.WebRootPath ?? string.Empty, "uploads", "logos", fileName),
            Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "logos", fileName),
            Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "LOGIN", "WebApplication1", "wwwroot", "uploads", "logos", fileName)),
            Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "MYPROFILE", "NextHorizon", "wwwroot", "uploads", "logos", fileName))
        };

        return candidates.FirstOrDefault(System.IO.File.Exists);
    }

    private static string GetImageContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static MessageDto CreateFaqMessageDto(long messageId, int conversationId, int senderUserId, string body)
        => new()
        {
            MessageId = messageId,
            ConversationId = conversationId,
            SenderUserId = senderUserId.ToString(),
            Body = body,
            AttachmentUrl = null,
            AttachmentContentType = null,
            AttachmentFileName = null,
            SentAt = DateTime.UtcNow,
            IsDeleted = false
        };
}
