namespace Superchef.Models;

#nullable disable warnings

public record ItemCardDM
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public decimal Rating { get; set; }
    public int Sold { get; set; }
    public string? DeleteLink { get; set; }
    public string? DeleteMsg { get; set; }
}

public record StoreCardDM
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
    public decimal Rating { get; set; }
    public int ItemCount { get; set; }
    public string? DeleteLink { get; set; }
    public string? DeleteMsg { get; set; }
}

public record CategoryCardDM
{
    public string Link { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
}

public record OrderItemCardDM
{
    public string? Link { get; set; }
    public string Image { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
    public string Variant { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public record SlotCardDM
{
    public string Text { get; set; }
    public bool IsDisabled { get; set; }
}

public record StepContainerDM
{
    public int CurrentStep { get; set; }
    public string Title { get; set; }
}

public record SummaryContainerDM
{
    public decimal TotalPrice { get; set; }
    public int TotalItems { get; set; }
    public string? SubmitText { get; set; }
    public string? ExitText { get; set; }
    public string ExitLink { get; set; }
}