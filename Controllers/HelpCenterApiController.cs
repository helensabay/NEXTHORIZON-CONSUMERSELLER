using System.Security.Cryptography;
using System.Text;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models.HelpCenter;
using MyAspNetApp.Security;

namespace MyAspNetApp.Controllers;

[ApiController]
[Route("api/help")]
public sealed class HelpCenterApiController : ControllerBase
{
    private const int MaxSearchResults = 10;
    private const string AssistantSupportFaqSessionKey = "HelpAssistant.SupportFaqId";
    private const string ConsumerUserType = "Consumer";
    private const string ActiveStatus = "Active";
    private const string DefaultCategoryIcon = "fa-solid fa-circle-question";
    private const string ContactCategorySlug = "contact";
    private const string ContactCategoryTitle = "Contact Support";
    private const string ContactCategoryDescription = "Reach support directly or submit a ticket for help.";
    private const string ContactCategoryIcon = "fa-solid fa-life-ring";
    private const string SupportAgentRole = "Agent";
    private const string ConsumerSenderRole = "Consumer";
    private const string LiveAgentQueueWelcomeMessage = "Thank you for reaching out regarding your concern. An agent will be assigned to you shortly. There are currently 3 people ahead of you in the queue.";
    private const string LiveAgentWaitingReplyMessage = "An agent will assist you shortly.";
    private const string LiveAgentUnavailableMessage = "Live Agent is temporarily unavailable right now. Please try again shortly.";
    private static readonly TimeSpan LiveAgentInactivityTimeout = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _dbContext;
    private readonly IAuthenticatedUserContextService _authenticatedUserContextService;

    public HelpCenterApiController(
        AppDbContext dbContext,
        IAuthenticatedUserContextService authenticatedUserContextService)
    {
        _dbContext = dbContext;
        _authenticatedUserContextService = authenticatedUserContextService;
    }

    [HttpGet("home")]
    public async Task<ActionResult<HelpHomeResponseDto>> GetHome(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await GetCategorySummariesAsync(cancellationToken);

            var featuredFaqs = await QueryVisibleFaqs()
                .OrderBy(faq => faq.Category)
                .ThenBy(faq => faq.FaqId)
                .Select(faq => new
                {
                    faq.FaqId,
                    faq.Question,
                    faq.Answer,
                    faq.Category,
                })
                .Take(4)
                .ToListAsync(cancellationToken);

            var featured = featuredFaqs
                .Select(faq => new HelpFeaturedFaqDto(
                    faq.FaqId,
                    faq.Question,
                    faq.Answer,
                    NormalizeSlug(faq.Category),
                    faq.Category.Trim()))
                .ToList();

            return Ok(new HelpHomeResponseDto(categories, featured));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Ok(BuildFallbackHomeResponse());
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<HelpCategorySummaryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await GetCategorySummariesAsync(cancellationToken);
            return Ok(categories);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Ok(BuildFallbackCategories());
        }
    }

    [HttpGet("categories/{slug}")]
    public async Task<ActionResult<HelpCategoryDetailDto>> GetCategory(string slug, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return NotFound();
        }

        if (string.Equals(normalizedSlug, ContactCategorySlug, StringComparison.Ordinal))
        {
            return Ok(new HelpCategoryDetailDto(
                ContactCategorySlug,
                ContactCategoryTitle,
                ContactCategoryDescription,
                ContactCategoryIcon,
                int.MaxValue,
                Array.Empty<HelpFaqDto>()));
        }

        try
        {
            var categoryName = await ResolveCategoryNameAsync(normalizedSlug, cancellationToken);
            if (categoryName is null)
            {
                return NotFound();
            }

            var category = CreateCategorySummary(categoryName, await GetCategoryDisplayOrderAsync(categoryName, cancellationToken));

            var faqs = await QueryVisibleFaqs()
                .Where(faq => faq.Category.Trim() == categoryName)
                .OrderBy(faq => faq.FaqId)
                .Select(faq => new HelpFaqDto(faq.FaqId, faq.Question, faq.Answer, faq.FaqId))
                .ToListAsync(cancellationToken);

            return Ok(new HelpCategoryDetailDto(
                category.Slug,
                category.Title,
                category.Description,
                category.IconKey,
                category.DisplayOrder,
                faqs));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            var fallback = FindFallbackCategory(normalizedSlug);
            return fallback is null ? NotFound() : Ok(fallback);
        }
    }

    [HttpGet("search")]
    [EnableRateLimiting("help-search")]
    public async Task<ActionResult<IReadOnlyList<HelpSearchResultDto>>> Search([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Ok(Array.Empty<HelpSearchResultDto>());
        }

        try
        {
            await CaptureAssistantQuestionIfMissingAsync(normalizedQuery, cancellationToken);

            var pattern = $"%{normalizedQuery}%";

            var results = await QueryVisibleFaqs()
                .Where(faq => EF.Functions.Like(faq.Question, pattern)
                    || EF.Functions.Like(faq.Answer, pattern)
                    || EF.Functions.Like(faq.Category, pattern))
                .OrderBy(faq => faq.Category)
                .ThenBy(faq => faq.FaqId)
                .Select(faq => new
                {
                    faq.FaqId,
                    faq.Question,
                    faq.Answer,
                    faq.Category,
                })
                .Take(MaxSearchResults)
                .ToListAsync(cancellationToken);

            return Ok(results.Select(result => new HelpSearchResultDto(
                result.FaqId,
                result.Question,
                result.Answer,
                NormalizeSlug(result.Category),
                result.Category.Trim())).ToList());
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            var fallback = BuildFallbackSearchResults(normalizedQuery);
            return Ok(fallback);
        }
    }

    [HttpPost("assistant/category")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<object>> SelectAssistantCategory([FromBody] SelectAssistantCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var categorySlug = NormalizeSlug(request.CategorySlug);
            var categoryName = await ResolveCategoryNameAsync(categorySlug, cancellationToken);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return BadRequest("CategorySlug must reference an active consumer FAQ category.");
            }

            var timestamp = await GetDatabaseNowAsync(cancellationToken);
            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            var userType = currentUser is null ? "Guest" : ConsumerUserType;
            var supportFaqId = HttpContext.Session.GetInt32(AssistantSupportFaqSessionKey);
            var storedSupportFaqId = await UpsertAssistantSupportFaqAsync(
                supportFaqId,
                categoryName,
                userType,
                timestamp,
                cancellationToken);

            HttpContext.Session.SetInt32(AssistantSupportFaqSessionKey, storedSupportFaqId);

            return Ok(new
            {
                supportFaqId = storedSupportFaqId,
                category = categoryName,
                question = string.Empty,
                status = "Waiting"
            });
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpPost("assistant/question")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<object>> CaptureAssistantQuestion([FromBody] CaptureLiveAgentQuestionRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("Message is required.");
        }

        try
        {
            await CaptureAssistantQuestionIfMissingAsync(message, cancellationToken);
            return Ok(new { stored = true });
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpPost("assistant/resolve")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<object>> ResolveAssistantSupportFaq(CancellationToken cancellationToken)
    {
        var supportFaqId = HttpContext.Session.GetInt32(AssistantSupportFaqSessionKey);
        if (!supportFaqId.HasValue)
        {
            return NoContent();
        }

        try
        {
            var timestamp = await GetDatabaseNowAsync(cancellationToken);
            var resolved = await ResolveAssistantSupportFaqAsync(supportFaqId.Value, timestamp, cancellationToken);
            if (resolved)
            {
                HttpContext.Session.Remove(AssistantSupportFaqSessionKey);
            }

            return Ok(new { resolved });
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpGet("contact")]
    public async Task<ActionResult<HelpContactResponseDto>> GetContact(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var channels = await _dbContext.SupportContactChannels
                .AsNoTracking()
                .Where(channel => channel.IsActive)
                .OrderBy(channel => channel.DisplayOrder)
                .Select(channel => new SupportContactDto(
                    channel.ChannelType,
                    channel.Label,
                    channel.Value,
                    channel.DisplayText,
                    channel.ActionHref,
                    channel.DisplayOrder))
                .ToListAsync(cancellationToken);

            return Ok(new HelpContactResponseDto(channels));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Ok(new HelpContactResponseDto(BuildFallbackContactChannels()));
        }
    }

    [HttpPost("tickets")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<CreateSupportTicketResponse>> CreateTicket([FromBody] CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var categorySlug = NormalizeSlug(request.CategorySlug);
        var categoryName = string.IsNullOrWhiteSpace(categorySlug)
            ? null
            : await ResolveCategoryNameAsync(categorySlug, cancellationToken);

        if (!string.IsNullOrWhiteSpace(categorySlug) && categoryName is null)
        {
            return BadRequest("CategorySlug must reference an active consumer FAQ category.");
        }

        var timestamp = await GetDatabaseNowAsync(cancellationToken);
        var ticket = new SupportTicket
        {
            ReferenceCode = CreateReferenceCode(timestamp),
            UserId = currentUser.UserId,
            ConsumerId = currentUser.ConsumerId,
            FaqCategory = categoryName,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = SupportTicketStatus.Open,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new CreateSupportTicketResponse(
                ticket.SupportTicketId,
                ticket.ReferenceCode,
                ticket.Status.ToString(),
                ticket.CreatedAt));
    }

    [HttpGet("live-agent/sessions/current")]
    public async Task<ActionResult<LiveAgentSessionResponse>> GetCurrentLiveAgentSession(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var session = await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);
            if (session is null)
            {
                return NoContent();
            }

            return Ok(await CreateLiveAgentSessionResponseAsync(session, cancellationToken));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return NoContent();
        }
    }

    [HttpGet("live-agent/sessions/last-ended")]
    public async Task<ActionResult<LastEndedLiveAgentNoticeResponse>> GetLastEndedLiveAgentNotice(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var activeSession = await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);
            if (activeSession is not null)
            {
                return NoContent();
            }

            var notice = await CreateLastEndedLiveAgentNoticeAsync(currentUser.UserId, cancellationToken);
            if (notice is null)
            {
                return NoContent();
            }

            return Ok(notice);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return NoContent();
        }
    }

    [HttpPost("live-agent/sessions")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<LiveAgentSessionResponse>> CreateLiveAgentSession([FromBody] CreateLiveAgentSessionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var categorySlug = NormalizeSlug(request.CategorySlug);
            var categoryName = await ResolveCategoryNameAsync(categorySlug, cancellationToken);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return BadRequest("CategorySlug must reference an active consumer FAQ category.");
            }

            var activeSession = await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);
            if (activeSession is not null)
            {
                return Ok(await CreateLiveAgentSessionResponseAsync(activeSession, cancellationToken));
            }

            var timestamp = await GetDatabaseNowAsync(cancellationToken);
            var supportFaqRecord = new SupportFaqRecord
            {
                Category = categoryName,
                Question = string.Empty,
                Status = "Waiting",
                DurationMinutes = 0,
                UserType = ConsumerUserType,
                AgentId = null,
                CreatedAt = timestamp,
                StartTime = null,
                EndTime = null,
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.SupportFaqRecords.Add(supportFaqRecord);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.SupportMessages.Add(new SupportMessage
            {
                ConversationId = supportFaqRecord.Id,
                SenderId = 0,
                SenderRole = SupportAgentRole,
                MessageText = LiveAgentQueueWelcomeMessage,
                CreatedAt = timestamp,
            });

            var session = new LiveAgentSession
            {
                SupportFaqId = supportFaqRecord.Id,
                UserId = currentUser.UserId,
                ConsumerId = currentUser.ConsumerId,
                CategorySlug = NormalizeSlug(categoryName),
                CategoryTitle = categoryName,
                FirstQuestion = string.Empty,
                Status = LiveAgentSessionStatus.Waiting,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
            };

            _dbContext.LiveAgentSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                await CreateLiveAgentSessionResponseAsync(session, cancellationToken));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpPost("live-agent/sessions/{sessionId:int}/question")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<CaptureLiveAgentQuestionResponse>> CaptureLiveAgentQuestion(int sessionId, [FromBody] CaptureLiveAgentQuestionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var message = request.Message.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message is required.");
            }

            await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);

            var session = await _dbContext.LiveAgentSessions
                .FirstOrDefaultAsync(
                    item => item.LiveAgentSessionId == sessionId
                        && item.UserId == currentUser.UserId,
                    cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            if (session.Status == LiveAgentSessionStatus.Resolved)
            {
                return Conflict("This Live Agent session has already been resolved.");
            }

            var supportFaqRecord = await _dbContext.SupportFaqRecords
                .FirstOrDefaultAsync(item => item.Id == session.SupportFaqId, cancellationToken);

            if (supportFaqRecord is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(session.FirstQuestion))
            {
                await AppendLiveAgentMessagesAsync(session, supportFaqRecord, currentUser, message, includeWaitingReply: true, cancellationToken);
            }

            return Ok(new CaptureLiveAgentQuestionResponse(
                session.LiveAgentSessionId,
                supportFaqRecord.Id,
                session.Status.ToString(),
                supportFaqRecord.Question,
                session.UpdatedAt));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpPost("live-agent/sessions/{sessionId:int}/messages")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<AppendLiveAgentMessageResponse>> AppendLiveAgentMessage(int sessionId, [FromBody] AppendLiveAgentMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var message = request.Message.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message is required.");
            }

            await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);

            var session = await _dbContext.LiveAgentSessions
                .FirstOrDefaultAsync(
                    item => item.LiveAgentSessionId == sessionId
                        && item.UserId == currentUser.UserId,
                    cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            if (session.Status == LiveAgentSessionStatus.Resolved)
            {
                return Conflict("This Live Agent session has already been resolved.");
            }

            var supportFaqRecord = await _dbContext.SupportFaqRecords
                .FirstOrDefaultAsync(item => item.Id == session.SupportFaqId, cancellationToken);

            if (supportFaqRecord is null)
            {
                return NotFound();
            }

            var messages = await AppendLiveAgentMessagesAsync(session, supportFaqRecord, currentUser, message, includeWaitingReply: true, cancellationToken);
            var assignedAgentName = await ResolveAssignedAgentNameAsync(supportFaqRecord, cancellationToken);

            return Ok(new AppendLiveAgentMessageResponse(
                session.LiveAgentSessionId,
                supportFaqRecord.Id,
                session.Status.ToString(),
                !string.IsNullOrWhiteSpace(session.FirstQuestion),
                HasAssignedAgent(supportFaqRecord),
                assignedAgentName,
                session.UpdatedAt,
                messages));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    [HttpPost("live-agent/sessions/{sessionId:int}/resolve")]
    [EnableRateLimiting("help-ticket-create")]
    [ConditionalValidateAntiForgeryToken]
    public async Task<ActionResult<ResolveLiveAgentSessionResponse>> ResolveLiveAgentSession(int sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHelpCenterSupportSchemaAsync(cancellationToken);

            var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
            if (currentUser is null)
            {
                return Unauthorized();
            }

            await NormalizeCurrentLiveAgentSessionAsync(currentUser.UserId, cancellationToken);

            var session = await _dbContext.LiveAgentSessions
                .FirstOrDefaultAsync(
                    item => item.LiveAgentSessionId == sessionId
                        && item.UserId == currentUser.UserId,
                    cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            var timestamp = await GetDatabaseNowAsync(cancellationToken);
            if (session.Status != LiveAgentSessionStatus.Resolved)
            {
                var supportFaqRecord = await _dbContext.SupportFaqRecords
                    .FirstOrDefaultAsync(item => item.Id == session.SupportFaqId, cancellationToken);

                if (supportFaqRecord is null)
                {
                    return NotFound();
                }

                ResolveLiveAgentSessionState(session, supportFaqRecord, LiveAgentSessionEndedReason.Resolved, timestamp);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(new ResolveLiveAgentSessionResponse(
                session.LiveAgentSessionId,
                session.Status.ToString(),
                session.UpdatedAt));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = LiveAgentUnavailableMessage });
        }
    }

    private async Task<LiveAgentSession?> NormalizeCurrentLiveAgentSessionAsync(int userId, CancellationToken cancellationToken)
    {
        var unresolvedSessions = await _dbContext.LiveAgentSessions
            .Where(item => item.UserId == userId && item.Status != LiveAgentSessionStatus.Resolved)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.LiveAgentSessionId)
            .ToListAsync(cancellationToken);

        if (unresolvedSessions.Count == 0)
        {
            return null;
        }

        var timestamp = await GetDatabaseNowAsync(cancellationToken);
        var supportFaqIds = unresolvedSessions
            .Select(item => item.SupportFaqId)
            .Distinct()
            .ToArray();
        var supportFaqRecords = supportFaqIds.Length == 0
            ? new Dictionary<int, SupportFaqRecord>()
            : await _dbContext.SupportFaqRecords
                .Where(item => supportFaqIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var hasChanges = false;

        foreach (var inactiveSession in unresolvedSessions.Where(item => IsLiveAgentSessionInactive(item, timestamp)).ToList())
        {
            if (supportFaqRecords.TryGetValue(inactiveSession.SupportFaqId, out var supportFaqRecord))
            {
                ResolveLiveAgentSessionState(inactiveSession, supportFaqRecord, LiveAgentSessionEndedReason.Inactive, timestamp);
            }
            else
            {
                ResolveLiveAgentSessionWithoutSupportRecord(inactiveSession, LiveAgentSessionEndedReason.Inactive, timestamp);
            }

            hasChanges = true;
        }

        unresolvedSessions = unresolvedSessions
            .Where(item => item.Status != LiveAgentSessionStatus.Resolved)
            .ToList();

        if (unresolvedSessions.Count == 0)
        {
            if (hasChanges)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        if (unresolvedSessions.Count == 1)
        {
            if (hasChanges)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return unresolvedSessions[0];
        }

        var activeSession = unresolvedSessions[0];
        var duplicateSessions = unresolvedSessions.Skip(1).ToList();

        foreach (var duplicateSession in duplicateSessions)
        {
            if (supportFaqRecords.TryGetValue(duplicateSession.SupportFaqId, out var supportFaqRecord))
            {
                ResolveLiveAgentSessionState(duplicateSession, supportFaqRecord, LiveAgentSessionEndedReason.Resolved, timestamp);
            }
            else
            {
                ResolveLiveAgentSessionWithoutSupportRecord(duplicateSession, LiveAgentSessionEndedReason.Resolved, timestamp);
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return activeSession;
    }

    private async Task<LastEndedLiveAgentNoticeResponse?> CreateLastEndedLiveAgentNoticeAsync(int userId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.LiveAgentSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && item.Status == LiveAgentSessionStatus.Resolved
                && item.EndedReason != LiveAgentSessionEndedReason.None)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.LiveAgentSessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        return new LastEndedLiveAgentNoticeResponse(
            session.LiveAgentSessionId,
            session.CategoryTitle,
            session.EndedReason.ToString(),
            session.UpdatedAt);
    }

    private async Task<LiveAgentSessionResponse> CreateLiveAgentSessionResponseAsync(LiveAgentSession session, CancellationToken cancellationToken)
    {
        var supportFaqRecord = await _dbContext.SupportFaqRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == session.SupportFaqId, cancellationToken);
        var hasAssignedAgent = HasAssignedAgent(supportFaqRecord);
        var assignedAgentName = await ResolveAssignedAgentNameAsync(supportFaqRecord, cancellationToken);
        var messages = await GetLiveAgentMessagesAsync(session.SupportFaqId, hasAssignedAgent, cancellationToken);

        return new LiveAgentSessionResponse(
            session.LiveAgentSessionId,
            session.SupportFaqId,
            session.Status.ToString(),
            session.CreatedAt,
            session.UpdatedAt,
            session.CategorySlug,
            session.CategoryTitle,
            !string.IsNullOrWhiteSpace(session.FirstQuestion),
            hasAssignedAgent,
            assignedAgentName,
            messages);
    }

    private async Task<string?> ResolveAssignedAgentNameAsync(SupportFaqRecord? supportFaqRecord, CancellationToken cancellationToken)
    {
        if (supportFaqRecord?.AgentId is not int agentUserId)
        {
            return null;
        }

        return await _dbContext.SupportAgents
            .AsNoTracking()
            .Where(item => item.UserId == agentUserId)
            .OrderByDescending(item => item.AgentStatus == "available")
            .ThenByDescending(item => item.ChatId)
            .Select(item => item.AgentName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LiveAgentMessageDto>> GetLiveAgentMessagesAsync(
        int supportFaqId,
        bool suppressQueueMessages,
        CancellationToken cancellationToken)
    {
        var messages = await _dbContext.SupportMessages
            .AsNoTracking()
            .Where(item => item.ConversationId == supportFaqId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new LiveAgentMessageDto(
                item.Id,
                item.ConversationId,
                item.SenderId,
                item.SenderRole,
                item.MessageText,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        if (!suppressQueueMessages)
        {
            return messages;
        }

        return messages
            .Where(item => !IsQueuedSystemMessage(item))
            .ToList();
    }

    private async Task<IReadOnlyList<LiveAgentMessageDto>> AppendLiveAgentMessagesAsync(
        LiveAgentSession session,
        SupportFaqRecord supportFaqRecord,
        AuthenticatedUserContext currentUser,
        string message,
        bool includeWaitingReply,
        CancellationToken cancellationToken)
    {
        var timestamp = await GetDatabaseNowAsync(cancellationToken);
        var createdMessages = new List<SupportMessage>();

        var userMessage = new SupportMessage
        {
            ConversationId = session.SupportFaqId,
            SenderId = currentUser.ConsumerId ?? currentUser.UserId,
            SenderRole = ConsumerSenderRole,
            MessageText = message,
            CreatedAt = timestamp,
        };

        createdMessages.Add(userMessage);
        _dbContext.SupportMessages.Add(userMessage);

        if (string.IsNullOrWhiteSpace(session.FirstQuestion))
        {
            session.FirstQuestion = message;
        }

        if (string.IsNullOrWhiteSpace(supportFaqRecord.Question))
        {
            supportFaqRecord.Question = message;
        }

        session.UpdatedAt = timestamp;

        if (includeWaitingReply && !HasAssignedAgent(supportFaqRecord))
        {
            var agentMessage = new SupportMessage
            {
                ConversationId = session.SupportFaqId,
                SenderId = 0,
                SenderRole = SupportAgentRole,
                MessageText = LiveAgentWaitingReplyMessage,
                CreatedAt = timestamp,
            };

            createdMessages.Add(agentMessage);
            _dbContext.SupportMessages.Add(agentMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return createdMessages
            .Select(item => new LiveAgentMessageDto(
                item.Id,
                item.ConversationId,
                item.SenderId,
                item.SenderRole,
                item.MessageText,
                item.CreatedAt))
            .ToList();
    }

    private static bool HasAssignedAgent(SupportFaqRecord? supportFaqRecord)
        => supportFaqRecord?.AgentId is not null;

    private static bool IsLiveAgentSessionInactive(LiveAgentSession session, DateTime timestamp)
        => timestamp - session.UpdatedAt >= LiveAgentInactivityTimeout;

    private static void ResolveLiveAgentSessionState(
        LiveAgentSession session,
        SupportFaqRecord supportFaqRecord,
        LiveAgentSessionEndedReason endedReason,
        DateTime timestamp)
    {
        supportFaqRecord.Status = "Resolved";
        supportFaqRecord.EndTime = timestamp;
        supportFaqRecord.DurationMinutes = supportFaqRecord.StartTime.HasValue
            ? Math.Max(0, (int)(timestamp - supportFaqRecord.StartTime.Value).TotalMinutes)
            : 0;

        session.Status = LiveAgentSessionStatus.Resolved;
        session.EndedReason = endedReason;
        session.UpdatedAt = timestamp;
    }

    private static void ResolveLiveAgentSessionWithoutSupportRecord(
        LiveAgentSession session,
        LiveAgentSessionEndedReason endedReason,
        DateTime timestamp)
    {
        session.Status = LiveAgentSessionStatus.Resolved;
        session.EndedReason = endedReason;
        session.UpdatedAt = timestamp;
    }

    private static bool IsQueuedSystemMessage(LiveAgentMessageDto item)
        => string.Equals(item.SenderRole, SupportAgentRole, StringComparison.Ordinal)
            && (string.Equals(item.MessageText, LiveAgentWaitingReplyMessage, StringComparison.Ordinal)
                || string.Equals(item.MessageText, LiveAgentQueueWelcomeMessage, StringComparison.Ordinal));

    private IQueryable<FaqRecord> QueryVisibleFaqs()
        => _dbContext.FaqRecords
            .AsNoTracking()
            .Where(faq => faq.UserType != null
                && faq.Status != null
                && faq.UserType.Trim() == ConsumerUserType
                && faq.Status.Trim() == ActiveStatus
                && faq.Category != null
                && faq.Category.Trim() != string.Empty);

    private async Task CaptureAssistantQuestionIfMissingAsync(string question, CancellationToken cancellationToken)
    {
        var supportFaqId = HttpContext.Session.GetInt32(AssistantSupportFaqSessionKey);
        if (!supportFaqId.HasValue)
        {
            return;
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.SupportFAQs
                SET Question = @Question
                WHERE Id = @Id
                    AND (Question IS NULL OR LTRIM(RTRIM(Question)) = N'');
                """;
            AddParameter(command, "@Question", question);
            AddParameter(command, "@Id", supportFaqId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int> UpsertAssistantSupportFaqAsync(
        int? supportFaqId,
        string categoryName,
        string userType,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (supportFaqId.HasValue)
            {
                await using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText =
                    """
                    UPDATE dbo.SupportFAQs
                    SET Category = @Category,
                        Question = @Question,
                        Status = @Status,
                        UserType = @UserType,
                        AgentId = @AgentId,
                        EndTime = @EndTime
                    WHERE Id = @Id
                        AND Status <> N'Resolved';
                    """;
                AddParameter(updateCommand, "@Category", categoryName);
                AddParameter(updateCommand, "@Question", string.Empty);
                AddParameter(updateCommand, "@Status", "Waiting");
                AddParameter(updateCommand, "@UserType", userType);
                AddParameter(updateCommand, "@AgentId", DBNull.Value);
                AddParameter(updateCommand, "@EndTime", DBNull.Value);
                AddParameter(updateCommand, "@Id", supportFaqId.Value);

                var updatedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                if (updatedRows > 0)
                {
                    return supportFaqId.Value;
                }
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT INTO dbo.SupportFAQs
                    (Category, Question, Status, DurationMinutes, UserType, AgentId, CreatedAt, EndTime, StartTime)
                OUTPUT INSERTED.Id
                VALUES
                    (@Category, @Question, @Status, @DurationMinutes, @UserType, @AgentId, @CreatedAt, @EndTime, @StartTime);
                """;
            AddParameter(insertCommand, "@Category", categoryName);
            AddParameter(insertCommand, "@Question", string.Empty);
            AddParameter(insertCommand, "@Status", "Waiting");
            AddParameter(insertCommand, "@DurationMinutes", 0);
            AddParameter(insertCommand, "@UserType", userType);
            AddParameter(insertCommand, "@AgentId", DBNull.Value);
            AddParameter(insertCommand, "@CreatedAt", timestamp);
            AddParameter(insertCommand, "@EndTime", DBNull.Value);
            AddParameter(insertCommand, "@StartTime", DBNull.Value);

            var insertedId = await insertCommand.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(insertedId);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> ResolveAssistantSupportFaqAsync(
        int supportFaqId,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.SupportFAQs
                SET Status = @Status,
                    EndTime = @EndTime,
                    DurationMinutes = CASE
                        WHEN StartTime IS NOT NULL THEN DATEDIFF(MINUTE, StartTime, @EndTime)
                        ELSE DATEDIFF(MINUTE, CreatedAt, @EndTime)
                    END
                WHERE Id = @Id
                    AND Status <> N'Resolved';
                """;
            AddParameter(command, "@Status", "Resolved");
            AddParameter(command, "@EndTime", timestamp);
            AddParameter(command, "@Id", supportFaqId);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<IReadOnlyList<HelpCategorySummaryDto>> GetCategorySummariesAsync(CancellationToken cancellationToken)
    {
        var categoryNames = await GetVisibleCategoryNamesAsync(cancellationToken);
        return categoryNames
            .Select((categoryName, index) => CreateCategorySummary(categoryName, index + 1))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetVisibleCategoryNamesAsync(CancellationToken cancellationToken)
    {
        var categoryNames = await QueryVisibleFaqs()
            .Select(faq => faq.Category)
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var categoryName in categoryNames)
        {
            var trimmed = categoryName?.Trim();
            var normalized = NormalizeSlug(trimmed);
            if (string.IsNullOrWhiteSpace(trimmed) || !seen.Add(normalized))
            {
                continue;
            }

            results.Add(trimmed);
        }

        return results;
    }

    private async Task<string?> ResolveCategoryNameAsync(string normalizedSlug, CancellationToken cancellationToken)
    {
        var categories = await GetVisibleCategoryNamesAsync(cancellationToken);
        return categories.FirstOrDefault(category => NormalizeSlug(category) == normalizedSlug);
    }

    private async Task<int> GetCategoryDisplayOrderAsync(string categoryName, CancellationToken cancellationToken)
    {
        var categories = await GetVisibleCategoryNamesAsync(cancellationToken);
        for (var index = 0; index < categories.Count; index++)
        {
            if (string.Equals(categories[index], categoryName, StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        return int.MaxValue;
    }

    private static HelpCategorySummaryDto CreateCategorySummary(string categoryName, int displayOrder)
        => new(
            NormalizeSlug(categoryName),
            categoryName,
            $"Consumer FAQs for {categoryName}.",
            DefaultCategoryIcon,
            displayOrder);

    private static HelpHomeResponseDto BuildFallbackHomeResponse()
        => new(BuildFallbackCategories(), BuildFallbackFeaturedFaqs());

    private static IReadOnlyList<HelpCategorySummaryDto> BuildFallbackCategories()
        => new[]
        {
            CreateCategorySummary("Order Help", 1),
            CreateCategorySummary("Returns Help", 2),
            CreateCategorySummary("Billing Help", 3),
            CreateCategorySummary("Account Help", 4),
            CreateCategorySummary("Technical Help", 5),
            CreateCategorySummary("Tracking Help", 6),
            new HelpCategorySummaryDto(ContactCategorySlug, ContactCategoryTitle, ContactCategoryDescription, ContactCategoryIcon, int.MaxValue)
        };

    private static IReadOnlyList<HelpFeaturedFaqDto> BuildFallbackFeaturedFaqs()
        => new[]
        {
            new HelpFeaturedFaqDto(1, "Can I cancel my order after placing it?", "Yes, if your order has not shipped yet. Contact support right away if you need a cancellation.", NormalizeSlug("Order Help"), "Order Help"),
            new HelpFeaturedFaqDto(2, "How do I start a return?", "Open your order details and choose the return option if the item is eligible.", NormalizeSlug("Returns Help"), "Returns Help"),
            new HelpFeaturedFaqDto(3, "Why was my card charged twice?", "Pending authorizations can appear more than once before the bank settles the final charge.", NormalizeSlug("Billing Help"), "Billing Help"),
            new HelpFeaturedFaqDto(4, "How do I update my profile?", "Go to your account profile and edit the fields you want to change.", NormalizeSlug("Account Help"), "Account Help")
        };

    private static IReadOnlyList<HelpCategorySummaryDto> BuildFallbackCategoriesForSearch()
        => BuildFallbackCategories();

    private static IReadOnlyList<HelpSearchResultDto> BuildFallbackSearchResults(string query)
    {
        var results = BuildFallbackFeaturedFaqs()
            .Where(item => item.Question.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Answer.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.CategoryTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(item => new HelpSearchResultDto(item.Id, item.Question, item.Answer, item.CategorySlug, item.CategoryTitle))
            .Take(MaxSearchResults)
            .ToList();

        return results;
    }

    private static HelpCategoryDetailDto? FindFallbackCategory(string normalizedSlug)
    {
        return normalizedSlug switch
        {
            "order-help" or "order" => new HelpCategoryDetailDto("order-help", "Order Help", "Consumer FAQs for Order Help.", DefaultCategoryIcon, 1, new[]
            {
                new HelpFaqDto(1, "Can I cancel my order after placing it?", "Yes, if your order has not shipped yet. Contact support right away if you need a cancellation.", 1),
                new HelpFaqDto(2, "What does Processing mean?", "Processing means your order is being prepared for shipment.", 2),
                new HelpFaqDto(3, "How do I know if my order was successful?", "You will receive an order confirmation and can check My Purchases for the current status.", 3),
            }),
            "returns-help" or "returns" => new HelpCategoryDetailDto("returns-help", "Returns Help", "Consumer FAQs for Returns Help.", DefaultCategoryIcon, 2, new[]
            {
                new HelpFaqDto(4, "How do I start a return?", "Open your order details and choose the return option if the item is eligible.", 1),
                new HelpFaqDto(5, "How long do refunds take?", "Refund timing depends on your payment method and bank processing.", 2),
                new HelpFaqDto(6, "Can I exchange a size?", "If the item is eligible, you can request an exchange instead of a refund.", 3),
            }),
            "billing-help" or "billing" => new HelpCategoryDetailDto("billing-help", "Billing Help", "Consumer FAQs for Billing Help.", DefaultCategoryIcon, 3, new[]
            {
                new HelpFaqDto(7, "Why was my card charged twice?", "Pending authorizations can appear more than once before the bank settles the final charge.", 1),
                new HelpFaqDto(8, "Where is my receipt?", "Receipts are shown in your order confirmation and account history.", 2),
                new HelpFaqDto(9, "What payment methods are supported?", "We support major cards and popular local payment options where available.", 3),
            }),
            "account-help" or "account" => new HelpCategoryDetailDto("account-help", "Account Help", "Consumer FAQs for Account Help.", DefaultCategoryIcon, 4, new[]
            {
                new HelpFaqDto(10, "How do I update my profile?", "Go to your account profile and edit the fields you want to change.", 1),
                new HelpFaqDto(11, "I forgot my password. What now?", "Use the password reset flow from the login page.", 2),
                new HelpFaqDto(12, "How do I sign out of all devices?", "Change your password and review your account security settings.", 3),
            }),
            "technical-help" or "technical" => new HelpCategoryDetailDto("technical-help", "Technical Help", "Consumer FAQs for Technical Help.", DefaultCategoryIcon, 5, new[]
            {
                new HelpFaqDto(13, "The page is not loading correctly.", "Try refreshing the page or clearing your browser cache.", 1),
                new HelpFaqDto(14, "Images are missing or broken.", "Make sure your browser can reach the static asset paths and then reload.", 2),
                new HelpFaqDto(15, "Where can I report a bug?", "Use the contact options on this page and include the page name plus the steps to reproduce it.", 3),
            }),
            "tracking-help" or "tracking" => new HelpCategoryDetailDto("tracking-help", "Tracking Help", "Consumer FAQs for Tracking Help.", DefaultCategoryIcon, 6, new[]
            {
                new HelpFaqDto(16, "How do I track my order?", "Open your order details to view the latest shipping updates.", 1),
                new HelpFaqDto(17, "Tracking says no information yet.", "Carrier scans can take time to appear after dispatch.", 2),
                new HelpFaqDto(18, "Can I change the delivery address?", "Address changes may not be possible after the parcel has been handed off.", 3),
            }),
            "contact" => new HelpCategoryDetailDto(ContactCategorySlug, ContactCategoryTitle, ContactCategoryDescription, ContactCategoryIcon, int.MaxValue, Array.Empty<HelpFaqDto>()),
            _ => null
        };
    }

    private static IReadOnlyList<SupportContactDto> BuildFallbackContactChannels()
        => new[]
        {
            new SupportContactDto("email", "Email Support", "support@nexthorizon.ph", "support@nexthorizon.ph", "mailto:support@nexthorizon.ph", 1),
            new SupportContactDto("phone", "Phone Support", "+63 917 123 4567", "+63 917 123 4567", "tel:+639171234567", 2),
            new SupportContactDto("chat", "Live Chat", "Open live support chat", "Open live support chat", "/Help/Assistant", 3),
            new SupportContactDto("hours", "Support Hours", "Mon-Fri, 9:00 AM - 6:00 PM PHT", "Mon-Fri, 9:00 AM - 6:00 PM PHT", string.Empty, 4),
        };

    private static bool IsDatabaseUnavailable(Exception exception)
    {
        if (exception is TimeoutException or SqlException or InvalidOperationException)
        {
            return true;
        }

        if (exception.InnerException is not null)
        {
            return IsDatabaseUnavailable(exception.InnerException);
        }

        return false;
    }

    private static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var pendingSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingSeparator = false;
                continue;
            }

            if (character == '&')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append("and");
                pendingSeparator = false;
                continue;
            }

            pendingSeparator = builder.Length > 0;
        }

        return builder.ToString().Trim('-');
    }

    private async Task<DateTime> GetDatabaseNowAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYSDATETIME();";
            var value = await command.ExecuteScalarAsync(cancellationToken);

            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
                _ => DateTime.Now
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string CreateReferenceCode(DateTime timestamp)
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var suffix = Convert.ToHexString(bytes);
        return $"HC-{timestamp:yyyyMMddHHmmss}-{suffix}";
    }

    private Task EnsureHelpCenterSupportSchemaAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
            IF OBJECT_ID(N'dbo.SupportFAQs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SupportFAQs
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Category NVARCHAR(120) NOT NULL CONSTRAINT DF_SupportFAQs_Category DEFAULT(N'General'),
                    Question NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SupportFAQs_Question DEFAULT(N''),
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_SupportFAQs_Status DEFAULT(N'Waiting'),
                    DurationMinutes INT NOT NULL CONSTRAINT DF_SupportFAQs_DurationMinutes DEFAULT(0),
                    UserType NVARCHAR(40) NOT NULL CONSTRAINT DF_SupportFAQs_UserType DEFAULT(N'Consumer'),
                    AgentId INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportFAQs_CreatedAt DEFAULT(SYSDATETIME()),
                    EndTime DATETIME2 NULL,
                    StartTime DATETIME2 NULL
                );
            END;

            IF OBJECT_ID(N'dbo.SupportMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SupportMessages
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ConversationId INT NOT NULL,
                    SenderId INT NOT NULL,
                    SenderRole NVARCHAR(40) NOT NULL,
                    MessageText NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportMessages_CreatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'dbo.LiveAgentSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveAgentSessions
                (
                    LiveAgentSessionId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SupportFaqId INT NOT NULL,
                    UserId INT NOT NULL,
                    ConsumerId INT NULL,
                    CategorySlug NVARCHAR(80) NOT NULL,
                    CategoryTitle NVARCHAR(120) NOT NULL,
                    FirstQuestion NVARCHAR(MAX) NOT NULL CONSTRAINT DF_LiveAgentSessions_FirstQuestion DEFAULT(N''),
                    Status TINYINT NOT NULL CONSTRAINT DF_LiveAgentSessions_Status DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LiveAgentSessions_CreatedAt DEFAULT(SYSDATETIME()),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_LiveAgentSessions_UpdatedAt DEFAULT(SYSDATETIME()),
                    EndedReason TINYINT NOT NULL CONSTRAINT DF_LiveAgentSessions_EndedReason DEFAULT(0)
                );
            END;

            IF OBJECT_ID(N'dbo.SupportTickets', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SupportTickets
                (
                    SupportTicketId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ReferenceCode NVARCHAR(40) NOT NULL,
                    UserId INT NOT NULL,
                    ConsumerId INT NULL,
                    FaqCategory NVARCHAR(120) NULL,
                    Subject NVARCHAR(160) NOT NULL,
                    Body NVARCHAR(MAX) NOT NULL,
                    Status TINYINT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportTickets_CreatedAt DEFAULT(SYSDATETIME()),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportTickets_UpdatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'dbo.SupportContactChannels', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SupportContactChannels
                (
                    SupportContactChannelId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ChannelType NVARCHAR(40) NOT NULL,
                    Label NVARCHAR(80) NOT NULL,
                    Value NVARCHAR(320) NOT NULL,
                    DisplayText NVARCHAR(320) NOT NULL,
                    ActionHref NVARCHAR(400) NOT NULL,
                    DisplayOrder INT NOT NULL CONSTRAINT DF_SupportContactChannels_DisplayOrder DEFAULT(0),
                    IsActive BIT NOT NULL CONSTRAINT DF_SupportContactChannels_IsActive DEFAULT(1)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.SupportContactChannels)
            BEGIN
                INSERT INTO dbo.SupportContactChannels (ChannelType, Label, Value, DisplayText, ActionHref, DisplayOrder, IsActive)
                VALUES
                    (N'email', N'Email Support', N'support@nexthorizon.ph', N'support@nexthorizon.ph', N'mailto:support@nexthorizon.ph', 1, 1),
                    (N'phone', N'Phone Support', N'+63 917 123 4567', N'+63 917 123 4567', N'tel:+639171234567', 2, 1),
                    (N'chat', N'Live Chat', N'Open live support chat', N'Open live support chat', N'/Help/Assistant', 3, 1),
                    (N'hours', N'Support Hours', N'Mon-Fri, 9:00 AM - 6:00 PM PHT', N'Mon-Fri, 9:00 AM - 6:00 PM PHT', N'', 4, 1);
            END;
            """;

        return _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
