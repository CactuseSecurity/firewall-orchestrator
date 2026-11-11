using System.Diagnostics;

namespace FWO.Services.RulePositioning.Blocks;

/// <summary>
/// Evaluates validated positioning recipes depth-first and stops at the first failure.
/// </summary>
public sealed class PositioningRecipeExecutor<TContext, TOutput>
{
    /// <summary>
    /// Evaluates one recipe without retaining evaluation state between calls.
    /// </summary>
    public async Task<PositioningBlockResult<TOutput>> ExecuteAsync(
        PositioningRecipe<TContext, TOutput>? recipe,
        TContext context,
        CancellationToken cancellationToken = default)
    {
        if (recipe == null)
        {
            PositioningBlockFailure<TOutput> failure = new(
                PositioningBlockFailureCategory.InvalidRecipe,
                "recipe");
            RulePositioningDiagnosticLog.WriteFailure("recipe.execution", new Dictionary<string, object?>
            {
                ["selection_category"] = PositioningBlockFailureCategory.InvalidRecipe.ToString(),
                ["originating_block_kind"] = "recipe"
            });
            return failure;
        }

        (int nodeCount, int maxDepth) = RecipeShape(recipe.PlanRoot);
        RulePositioningDiagnosticLog.WriteInfo("recipe.execution", "start", new Dictionary<string, object?>
        {
            ["root_block_kind"] = recipe.PlanRoot.Kind,
            ["node_count"] = nodeCount,
            ["max_depth"] = maxDepth
        });
        ExecutionState state = new();
        PositioningBlockResult<TOutput> result = await EvaluateNodeAsync(
            recipe.PlanRoot,
            context,
            state,
            cancellationToken).ConfigureAwait(false);
        RulePositioningDiagnosticLog.WriteInfo(
            "recipe.execution",
            result is PositioningBlockFailure<TOutput> ? "failure" : "success",
            ResultFacts(result));
        return result;
    }

    /// <summary>
    /// Evaluates children in declaration order and invokes the parent only after all succeed.
    /// </summary>
    private static async Task<PositioningBlockResult<TOutput>> EvaluateNodeAsync(
        PositioningRecipeNode<TContext, TOutput> node,
        TContext context,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int sequence = Interlocked.Increment(ref state.Sequence);
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<TOutput> childValues = new(node.Children.Count);
        foreach (PositioningRecipeNode<TContext, TOutput> child in node.Children)
        {
            PositioningBlockResult<TOutput> childResult =
                await EvaluateNodeAsync(child, context, state, cancellationToken).ConfigureAwait(false);
            if (childResult is PositioningBlockFailure<TOutput>)
            {
                WriteNode(node, sequence, stopwatch.ElapsedMilliseconds, childValues.Count, childResult);
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
            PositioningBlockResult<TOutput> nonNullResult = result ?? UnexpectedFailure(node.Kind);
            WriteNode(node, sequence, stopwatch.ElapsedMilliseconds, childValues.Count, nonNullResult);
            return nonNullResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PositioningBlockFailure<TOutput> failure = UnexpectedFailure(node.Kind);
            RulePositioningDiagnosticLog.WriteException("recipe.node", exception, NodeFacts(
                node, sequence, stopwatch.ElapsedMilliseconds, childValues.Count, failure));
            return failure;
        }
    }

    private static void WriteNode(
        PositioningRecipeNode<TContext, TOutput> node,
        int sequence,
        long durationMs,
        int actualChildCount,
        PositioningBlockResult<TOutput> result)
    {
        Dictionary<string, object?> facts = NodeFacts(node, sequence, durationMs, actualChildCount, result);
        if (result is PositioningBlockFailure<TOutput>)
        {
            RulePositioningDiagnosticLog.WriteFailure("recipe.node", facts);
        }
        else
        {
            RulePositioningDiagnosticLog.WriteInfo("recipe.node", "success", facts);
        }
    }

    private static Dictionary<string, object?> NodeFacts(
        PositioningRecipeNode<TContext, TOutput> node,
        int sequence,
        long durationMs,
        int actualChildCount,
        PositioningBlockResult<TOutput> result)
    {
        Dictionary<string, object?> facts = new()
        {
            ["node_sequence"] = sequence,
            ["kind"] = node.Kind,
            ["output_role"] = node.Block.OutputRole.Name,
            ["required_child_roles"] = node.Block.RequiredChildRoles.Select(role => role.Name).Take(16).ToArray(),
            ["required_child_count"] = node.Children.Count,
            ["actual_child_count"] = actualChildCount,
            ["duration_ms"] = durationMs,
            ["result_type"] = result.GetType().Name
        };
        if (result is PositioningBlockFailure<TOutput> failure)
        {
            facts["failure_category"] = failure.Category.ToString();
            facts["originating_block_kind"] = failure.OriginatingBlockKind;
        }
        return facts;
    }

    private static Dictionary<string, object?> ResultFacts(PositioningBlockResult<TOutput> result)
    {
        Dictionary<string, object?> facts = new() { ["result_type"] = result.GetType().Name };
        if (result is PositioningBlockFailure<TOutput> failure)
        {
            facts["failure_category"] = failure.Category.ToString();
            facts["originating_block_kind"] = failure.OriginatingBlockKind;
        }
        return facts;
    }

    private static (int NodeCount, int MaxDepth) RecipeShape(
        PositioningRecipeNode<TContext, TOutput> node)
    {
        int count = 1;
        int depth = 1;
        foreach (PositioningRecipeNode<TContext, TOutput> child in node.Children)
        {
            (int childCount, int childDepth) = RecipeShape(child);
            count += childCount;
            depth = Math.Max(depth, childDepth + 1);
        }
        return (count, depth);
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

    private sealed class ExecutionState
    {
        internal int Sequence;
    }
}
