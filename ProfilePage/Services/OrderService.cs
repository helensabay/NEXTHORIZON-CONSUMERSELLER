using NextHorizon.Models;

namespace NextHorizon.Services
{
    public class OrderService
    {
        public List<OrderViewModel> GetUserPurchases()
        {
            return new List<OrderViewModel>
            {
                new OrderViewModel
                {
                    OrderNumber = "SHN-88291",
                    Status = "To Pay",
                    ProductName = "Sport Shoes",
                    PaymentMethod = "GCash",
                    OrderDate = new DateTime(2026, 3, 5),
                    EstimatedArrival = "Mar 08 - Mar 10",
                    Color = "Black",
                    Size = "Large",
                    Quantity = 1,
                    TotalAmount = 1250.00m,
                    ProductImage = "/images/Products/shoes.jpg",
                    // Custom Modal Data
                    SellerName = "Stride Athletics Official",
                    ReceiverName = "Juan Dela Cruz",
                    PhoneNumber = "(+63) 912 345 6789",
                    ShippingAddress = "Unit 402, Sunshine Condominium, Cebu IT Park, Cebu City, 6000"
                },
                new OrderViewModel
                {
                    OrderNumber = "SHN-44102",
                    Status = "To Ship",
                    ProductName = "Minimalist Tote Bag",
                    PaymentMethod = "COD",
                    OrderDate = new DateTime(2026, 3, 4),
                    EstimatedArrival = "Mar 07 - Mar 09",
                    Color = "Beige",
                    Size = null,
                    Quantity = 2,
                    TotalAmount = 850.50m,
                    ProductImage = "/images/Products/totebag.jpg",
                    // Custom Modal Data
                    SellerName = "Monochrome Boutique",
                    ReceiverName = "Juan Dela Cruz",
                    PhoneNumber = "(+63) 912 345 6789",
                    ShippingAddress = "123 Street Name, Barangay, Cebu City, Philippines, 6000"
                },
                new OrderViewModel
                {
                    OrderNumber = "SHN-99303",
                    Status = "To Receive",
                    ProductName = "Urban Walkers",
                    PaymentMethod = "Credit Card",
                    OrderDate = new DateTime(2026, 3, 1),
                    EstimatedArrival = "Mar 06 - Mar 08",
                    Color = "White",
                    Size = "42",
                    Quantity = 1,
                    TotalAmount = 2499.00m,
                    ProductImage = "/images/Products/shirt.jpg",
                    // Custom Modal Data
                    SellerName = "Urban Footwear Co.",
                    ReceiverName = "Maria Clara (Gift)",
                    PhoneNumber = "(+63) 998 765 4321",
                    ShippingAddress = "456 Oak Drive, Quezon City, Metro Manila, 1100"
                },
                 new OrderViewModel
                {
                    OrderNumber = "SHN-99303",
                    Status = "Completed",
                    ProductName = "Urban Walkers",
                    PaymentMethod = "Credit Card",
                    OrderDate = new DateTime(2026, 3, 1),
                    EstimatedArrival = "Mar 06 - Mar 08",
                    Color = "White",
                    Size = "42",
                    Quantity = 1,
                    TotalAmount = 2499.00m,
                    ProductImage = "/images/Products/sportsBra.jpg",
                    // Custom Modal Data
                    SellerName = "Urban Footwear Co.",
                    ReceiverName = "Maria Clara (Gift)",
                    PhoneNumber = "(+63) 998 765 4321",
                    ShippingAddress = "456 Oak Drive, Quezon City, Metro Manila, 1100"
                },
                  new OrderViewModel
                {
                    OrderNumber = "SHN-99303",
                    Status = "To Review",
                    ProductName = "Urban Walkers",
                    PaymentMethod = "Credit Card",
                    OrderDate = new DateTime(2026, 3, 1),
                    EstimatedArrival = "Mar 06 - Mar 08",
                    Color = "White",
                    Size = "42",
                    Quantity = 1,
                    TotalAmount = 2499.00m,
                    ProductImage = "/images/Products/shirt2.jpg",
                    // Custom Modal Data
                    SellerName = "Urban Footwear Co.",
                    ReceiverName = "Maria Clara (Gift)",
                    PhoneNumber = "(+63) 998 765 4321",
                    ShippingAddress = "456 Oak Drive, Quezon City, Metro Manila, 1100"
                },
                   new OrderViewModel
                {
                    OrderNumber = "SHN-99303",
                    Status = "Returns",
                    ProductName = "Urban Walkers",
                    PaymentMethod = "Credit Card",
                    OrderDate = new DateTime(2026, 3, 1),
                    EstimatedArrival = "Mar 06 - Mar 08",
                    Color = "White",
                    Size = "42",
                    Quantity = 1,
                    TotalAmount = 2499.00m,
                    ProductImage = "/images/Products/shirt.jpg",
                    // Custom Modal Data
                    SellerName = "Urban Footwear Co.",
                    ReceiverName = "Maria Clara (Gift)",
                    PhoneNumber = "(+63) 998 765 4321",
                    ShippingAddress = "456 Oak Drive, Quezon City, Metro Manila, 1100"
                },
                    new OrderViewModel
                {
                    OrderNumber = "SHN-99303",
                    Status = "Cancelled",
                    ProductName = "Urban Walkers",
                    PaymentMethod = "Credit Card",
                    OrderDate = new DateTime(2026, 3, 1),
                    EstimatedArrival = "Mar 06 - Mar 08",
                    Color = "White",
                    Size = "42",
                    Quantity = 1,
                    TotalAmount = 2499.00m,
                    ProductImage = "/images/Products/waterbottle.jpg",
                    // Custom Modal Data
                    SellerName = "Urban Footwear Co.",
                    ReceiverName = "Maria Clara (Gift)",
                    PhoneNumber = "(+63) 998 765 4321",
                    ShippingAddress = "456 Oak Drive, Quezon City, Metro Manila, 1100"
                }

            };
        }
    }
}