namespace Superchef.Models;

#nullable disable warnings

public record ItemCard
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public decimal Rating { get; set; }
    public int Sold { get; set; }
    public bool IsFavorite { get; set; }
}

public record StoreCard
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
    public decimal Rating { get; set; }
    public int ItemCount { get; set; }
}

public record CategoryCard
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
}