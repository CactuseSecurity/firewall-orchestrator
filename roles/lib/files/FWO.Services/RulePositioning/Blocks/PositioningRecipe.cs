namespace FWO.Services.RulePositioning.Blocks;

/// <summary>
/// An immutable, validated execution plan rooted at one positioning block.
/// </summary>
public sealed class PositioningRecipe<TContext, TOutput>
{
    public IPositioningBlock<TContext, TOutput> Root { get; }
    internal PositioningRecipeNode<TContext, TOutput> PlanRoot { get; }

    /// <summary>
    /// Validates and snapshots a block graph into an immutable recipe.
    /// </summary>
    public PositioningRecipe(IPositioningBlock<TContext, TOutput> root)
    {
        Root = root ?? throw new PositioningRecipeException("recipe", "A recipe root is required.");
        HashSet<IPositioningBlock<TContext, TOutput>> active =
            new(ReferenceEqualityComparer.Instance);
        PlanRoot = BuildNode(root, active);
    }

    /// <summary>
    /// Validates and snapshots one node and its descendants.
    /// </summary>
    private static PositioningRecipeNode<TContext, TOutput> BuildNode(
        IPositioningBlock<TContext, TOutput> block,
        HashSet<IPositioningBlock<TContext, TOutput>> active)
    {
        string kind = RequireKind(block.Kind);
        RequireRole(block.OutputRole, kind);
        if (!active.Add(block))
        {
            throw new PositioningRecipeException(kind, "A positioning recipe must not contain cycles.");
        }

        try
        {
            IReadOnlyList<IPositioningBlock<TContext, TOutput>> children = block.Children
                ?? throw new PositioningRecipeException(kind, "A block must expose its children.");
            IReadOnlyList<PositioningBlockRole> requiredRoles = block.RequiredChildRoles
                ?? throw new PositioningRecipeException(kind, "A block must expose its child roles.");
            if (children.Count != requiredRoles.Count)
            {
                throw new PositioningRecipeException(kind, "A block must declare one role per child.");
            }

            List<PositioningRecipeNode<TContext, TOutput>> childPlans = new(children.Count);
            for (int index = 0; index < children.Count; index++)
            {
                IPositioningBlock<TContext, TOutput> child = children[index]
                    ?? throw new PositioningRecipeException(kind, "A block child must not be null.");
                RequireRole(requiredRoles[index], kind);
                if (child.OutputRole != requiredRoles[index])
                {
                    throw new PositioningRecipeException(kind, "A block child has an incompatible role.");
                }
                childPlans.Add(BuildNode(child, active));
            }
            return new PositioningRecipeNode<TContext, TOutput>(block, kind, childPlans);
        }
        finally
        {
            active.Remove(block);
        }
    }

    /// <summary>
    /// Rejects blank or unstable block kinds.
    /// </summary>
    private static string RequireKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new PositioningRecipeException("recipe", "A block kind must not be empty.");
        }
        return kind;
    }

    /// <summary>
    /// Rejects default or otherwise empty block-role values.
    /// </summary>
    private static void RequireRole(PositioningBlockRole role, string blockKind)
    {
        if (string.IsNullOrWhiteSpace(role.Name))
        {
            throw new PositioningRecipeException(blockKind, "A block role must not be empty.");
        }
    }
}

/// <summary>
/// Indicates that a block graph could not be made into a valid immutable recipe.
/// </summary>
public sealed class PositioningRecipeException : ArgumentException
{
    public PositioningBlockFailureCategory Category { get; } = PositioningBlockFailureCategory.InvalidRecipe;
    public string OriginatingBlockKind { get; }

    internal PositioningRecipeException(string originatingBlockKind, string message)
        : base(message)
    {
        OriginatingBlockKind = originatingBlockKind;
    }
}

internal sealed record PositioningRecipeNode<TContext, TOutput>(
    IPositioningBlock<TContext, TOutput> Block,
    string Kind,
    IReadOnlyList<PositioningRecipeNode<TContext, TOutput>> Children);
