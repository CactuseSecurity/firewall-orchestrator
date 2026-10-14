namespace FWO.Services.RulePositioning.Blocks;

/// <summary>
/// Evaluates validated positioning recipes depth-first and stops at the first failure.
/// </summary>
public sealed class PositioningRecipeExecutor<TContext, TOutput>
{
    /// <summary>
    /// Evaluates one recipe without retaining evaluation state between calls.
    /// </summary>
    public Task<PositioningBlockResult<TOutput>> ExecuteAsync(
        PositioningRecipe<TContext, TOutput>? recipe,
        TContext context,
        CancellationToken cancellationToken = default)
    {
        if (recipe == null)
        {
            return Task.FromResult<PositioningBlockResult<TOutput>>(
                new PositioningBlockFailure<TOutput>(
                    PositioningBlockFailureCategory.InvalidRecipe,
                    "recipe"));
        }
        return EvaluateNodeAsync(recipe.PlanRoot, context, cancellationToken);
    }

    /// <summary>
    /// Evaluates children in declaration order and invokes the parent only after all succeed.
    /// </summary>
    private static async Task<PositioningBlockResult<TOutput>> EvaluateNodeAsync(
        PositioningRecipeNode<TContext, TOutput> node,
        TContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<TOutput> childValues = new(node.Children.Count);
        foreach (PositioningRecipeNode<TContext, TOutput> child in node.Children)
        {
            PositioningBlockResult<TOutput> childResult =
                await EvaluateNodeAsync(child, context, cancellationToken).ConfigureAwait(false);
            if (childResult is PositioningBlockFailure<TOutput>)
            {
                return childResult;
            }
            childValues.Add(((PositioningBlockSuccess<TOutput>)childResult).Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            PositioningBlockResult<TOutput>? result = await node.Block
                .EvaluateAsync(context, childValues.AsReadOnly(), cancellationToken)
                .ConfigureAwait(false);
            return result ?? UnexpectedFailure(node.Kind);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return UnexpectedFailure(node.Kind);
        }
    }

    /// <summary>
    /// Converts unexpected implementation failures into a safe typed result.
    /// </summary>
    private static PositioningBlockFailure<TOutput> UnexpectedFailure(string blockKind)
    {
        return new PositioningBlockFailure<TOutput>(
            PositioningBlockFailureCategory.UnexpectedExecutionFailure,
            blockKind);
    }
}
