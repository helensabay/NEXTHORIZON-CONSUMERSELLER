namespace MyAspNetApp.Models;

public class LandingPageViewModel
{
    public bool ShowLoginWall { get; set; }
    public List<Product> Products { get; set; } = new();
}
