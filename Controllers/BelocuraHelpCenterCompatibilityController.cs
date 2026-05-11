using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models.HelpCenter;

namespace MyAspNetApp.Controllers;

[ApiController]
[Route("api/faqs")]
public sealed class BelocuraSellerFaqController : ControllerBase
{
    private static readonly string[] FallbackCategories = ["Order Help", "Returns Help", "Billing Help", "Account Help"];

    private readonly AppDbContext _context;

    public BelocuraSellerFaqController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _context.FaqRecords
                .Where(faq => faq.Status.ToLower() == "active" && faq.UserType.ToLower() == "seller")
                .Select(faq => faq.Category)
                .Where(category => category != "")
                .Distinct()
                .OrderBy(category => category)
                .ToListAsync(cancellationToken);

            return Ok(categories.Count > 0 ? categories : FallbackCategories.ToList());
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Ok(FallbackCategories);
        }
    }

    [HttpGet("items/{category}")]
    public async Task<IActionResult> GetFaqsByCategory(string category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Ok(Array.Empty<object>());
        }

        try
        {
            var faqs = await _context.FaqRecords
                .Where(faq =>
                    faq.Status.ToLower() == "active" &&
                    faq.UserType.ToLower() == "seller" &&
                    faq.Category.ToLower() == category.ToLower())
                .OrderBy(faq => faq.FaqId)
                .Select(faq => new
                {
                    faq.FaqId,
                    faq.Question,
                    faq.Answer
                })
                .ToListAsync(cancellationToken);

            if (faqs.Count > 0)
            {
                return Ok(faqs);
            }

            return Ok(BuildFallbackFaqs(category));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            return Ok(BuildFallbackFaqs(category));
        }
    }

    private static object[] BuildFallbackFaqs(string category) =>
    [
        new { FaqId = 1, Question = $"How do I get help with {category}?", Answer = "Choose Chat with Agent and send your concern to seller support." },
        new { FaqId = 2, Question = "How long does support take?", Answer = "A support agent will respond as soon as one is available." }
    ];

    private static bool IsDatabaseUnavailable(Exception exception)
        => exception is SqlException or TimeoutException ||
           exception.InnerException is not null && IsDatabaseUnavailable(exception.InnerException);
}

[ApiController]
[Route("api/support")]
public sealed class BelocuraSupportCompatibilityController : ControllerBase
{
    private readonly AppDbContext _context;

    public BelocuraSupportCompatibilityController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartConversation(
        [FromQuery] int sellerId,
        [FromQuery] string category,
        [FromQuery] string question,
        CancellationToken cancellationToken)
    {
        if (sellerId <= 0 || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(question))
        {
            return BadRequest(new { error = "Missing required fields" });
        }

        var supportFaq = new SupportFaqRecord
        {
            Category = category,
            Question = question,
            Status = "Waiting",
            UserType = "Seller",
            CreatedAt = DateTime.Now
        };

        _context.SupportFaqRecords.Add(supportFaq);
        await _context.SaveChangesAsync(cancellationToken);

        _context.SupportMessages.Add(new SupportMessage
        {
            ConversationId = supportFaq.Id,
            SenderId = sellerId,
            SenderRole = "Seller",
            MessageText = question,
            CreatedAt = DateTime.Now
        });

        _context.SupportMessages.Add(new SupportMessage
        {
            ConversationId = supportFaq.Id,
            SenderId = 0,
            SenderRole = "Agent",
            MessageText = "An agent will assist you shortly.",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = supportFaq.Id,
            supportFaqId = supportFaq.Id,
            reused = false
        });
    }

    [HttpPost("message")]
    public async Task<IActionResult> SaveMessage([FromBody] SaveSupportMessageRequest request, CancellationToken cancellationToken)
    {
        if (request.SupportFAQId <= 0)
        {
            return BadRequest(new { error = "Support FAQ id is required" });
        }

        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            return BadRequest(new { error = "Message text is required" });
        }

        var supportFaq = await _context.SupportFaqRecords.FindAsync([request.SupportFAQId], cancellationToken);
        if (supportFaq is null)
        {
            return NotFound(new { error = "Support FAQ not found" });
        }

        if (string.Equals(supportFaq.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supportFaq.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Cannot send a message to a resolved conversation" });
        }

        _context.SupportMessages.Add(new SupportMessage
        {
            ConversationId = request.SupportFAQId,
            SenderId = request.SenderId,
            SenderRole = string.IsNullOrWhiteSpace(request.SenderRole) ? "Seller" : request.SenderRole,
            MessageText = request.MessageText,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Message saved" });
    }

    [HttpGet("faq/{faqId:int}/messages")]
    public async Task<IActionResult> GetFaqMessages(int faqId, CancellationToken cancellationToken)
    {
        var supportFaq = await _context.SupportFaqRecords.FindAsync([faqId], cancellationToken);
        if (supportFaq is null)
        {
            return NotFound(new { error = "SupportFAQ not found" });
        }

        var messages = await _context.SupportMessages
            .Where(message => message.ConversationId == faqId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new
            {
                message.Id,
                message.SenderId,
                message.SenderRole,
                message.MessageText,
                message.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            agentName = supportFaq.AgentId.HasValue ? "Seller Support" : null,
            status = supportFaq.Status,
            messages
        });
    }

    [HttpGet("{conversationId:int}/state")]
    public async Task<IActionResult> GetConversationState(int conversationId, CancellationToken cancellationToken)
    {
        var supportFaq = await _context.SupportFaqRecords.FindAsync([conversationId], cancellationToken);
        if (supportFaq is null)
        {
            return NotFound(new { error = "No open conversation found" });
        }

        return Ok(new { id = supportFaq.Id, status = supportFaq.Status, supportFaqId = supportFaq.Id });
    }

    [HttpPut("{conversationId:int}/status")]
    public async Task<IActionResult> UpdateConversationStatus(
        int conversationId,
        [FromQuery] string status,
        [FromQuery] int? supportFaqId = null,
        [FromQuery] bool inactivity = false,
        CancellationToken cancellationToken = default)
    {
        var faqId = supportFaqId.GetValueOrDefault(conversationId);
        var supportFaq = await _context.SupportFaqRecords.FindAsync([faqId], cancellationToken);
        if (supportFaq is null)
        {
            return NotFound(new { error = "SupportFAQ not found" });
        }

        supportFaq.Status = string.IsNullOrWhiteSpace(status) ? "Resolved" : status;
        if (string.Equals(supportFaq.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supportFaq.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            supportFaq.EndTime = DateTime.Now;
        }

        if (inactivity)
        {
            _context.SupportMessages.Add(new SupportMessage
            {
                ConversationId = faqId,
                SenderId = 0,
                SenderRole = "Agent",
                MessageText = "The conversation will now be closed due to inactivity.",
                CreatedAt = DateTime.Now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Status updated", status = supportFaq.Status });
    }

    public sealed class SaveSupportMessageRequest
    {
        public int ConversationId { get; set; }
        public int SupportFAQId { get; set; }
        public int SenderId { get; set; }
        public string SenderRole { get; set; } = "Seller";
        public string MessageText { get; set; } = string.Empty;
        public string? Category { get; set; }
    }
}
