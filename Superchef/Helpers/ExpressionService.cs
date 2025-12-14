using System.Linq.Expressions;

namespace Superchef.Helpers;

public class ExpressionService
{
    public static Expression<Func<Store, bool>> ShowStoreToCustomer
    {
        get
        {
            return s => !s.IsDeleted;
        }
    }

    public static Expression<Func<Item, bool>> ShowItemToCustomer
    {
        get
        {
            return i =>
                i.IsActive &&
                !i.IsDeleted &&
                !i.Store.IsDeleted &&
                i.Variants.Any(v =>
                    v.IsActive &&
                    !v.IsDeleted
                );
        }
    }

    public static Expression<Func<Variant, bool>> ShowVariantToCustomer
    {
        get
        {
            return v =>
                v.IsActive &&
                !v.IsDeleted &&
                v.Item.IsActive &&
                !v.Item.IsDeleted &&
                !v.Item.Store.IsDeleted;
        }

    }

    public static Expression<Func<OrderItem, bool>> AllowCalculateOrderItemQuantity
    {
        get
        {
            return oi =>
                oi.Variant.Item.IsActive &&
                !oi.Variant.Item.IsDeleted &&
                !oi.Variant.Item.Store.IsDeleted &&
                oi.Variant.Item.Variants.Any(v =>
                    v.IsActive &&
                    !v.IsDeleted
                );
        }
    }
}