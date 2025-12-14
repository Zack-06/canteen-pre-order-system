using System.Linq.Expressions;

namespace Superchef.Helpers;

public class ExpressionService
{
    

    public static Expression<Func<Store, bool>> ShowStoreToCustomerExpr
    {
        get
        {
            return s => !s.IsDeleted;
        }
    }

    public static bool ShowToCustomer(Item i)
    {
        return i.IsActive &&
                !i.IsDeleted &&
                !i.Store.IsDeleted &&
                i.Variants.Any(v =>
                    v.IsActive &&
                    !v.IsDeleted
                );
    }

    public static Expression<Func<Item, bool>> ShowItemToCustomerExpr
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

    public static bool ShowToCustomer(Variant v)
    {
        return v.IsActive &&
                !v.IsDeleted &&
                v.Item.IsActive &&
                !v.Item.IsDeleted &&
                !v.Item.Store.IsDeleted;
    }

    public static Expression<Func<Variant, bool>> ShowVariantToCustomerExpr
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

    public static Expression<Func<OrderItem, bool>> AllowCalculateOrderItemQuantityExpr
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

    public static Expression<Func<Cart, bool>> ShowCartToCustomerExpr
    {
        get
        {
            return c => 
                c.Variant.IsActive &&
                !c.Variant.IsDeleted &&
                c.Variant.Item.IsActive &&
                !c.Variant.Item.IsDeleted &&
                !c.Variant.Item.Store.IsDeleted;
        }
    }
}