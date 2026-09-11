namespace WorkoutTracker.Services;

/// <summary>
/// Hands a newly-created food's id from FoodEditorViewModel back to whichever
/// RecipeBuilderViewModel instance sent the member there via "Can't find it?
/// Add a new food" — Shell's relative pop ("..") has no return-value channel
/// of its own, and the popped-back-to page instance is still alive with its
/// in-progress draft intact, so this only needs to carry the one new id across.
/// </summary>
public interface IRecipeDraftBridge
{
    void SetPendingFoodId(Guid foodId);
    Guid? ConsumePendingFoodId();
}

public class RecipeDraftBridge : IRecipeDraftBridge
{
    private Guid? _pendingFoodId;

    public void SetPendingFoodId(Guid foodId) => _pendingFoodId = foodId;

    public Guid? ConsumePendingFoodId()
    {
        var id = _pendingFoodId;
        _pendingFoodId = null;
        return id;
    }
}
