using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Data;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PurchasesController : ControllerBase
    {
        // GET: api/purchases/{productId}/delivered
        [HttpGet("{productId}/delivered")]
        public IActionResult CheckPurchaseStatus(int productId)
        {
            var purchase = ProductData.PurchaseRecords.FirstOrDefault(p => p.ProductId == productId);
            return Ok(new
            {
                hasPurchased = purchase != null,
                isDelivered = purchase?.IsDelivered ?? false,
                deliveryDate = purchase?.DeliveryDate
            });
        }
    }
}
