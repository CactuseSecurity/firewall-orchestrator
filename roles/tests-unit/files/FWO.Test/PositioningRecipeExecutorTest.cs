using FWO.Services.RulePositioning.Blocks;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class PositioningRecipeExecutorTest
{
    private static readonly PositioningBlockRole kNumberRole = new("test.number");
    private static readonly PositioningBlockRole kOtherRole = new("test.other");

    [Test]
    public async Task NestedAndMultiChildRecipe_PropagatesSuccessfulValues()
    {
        TestBlock first = Leaf("first", 2);
        TestBlock second = Leaf("second", 3);
        TestBlock sum = Parent("sum", [first, second], [kNumberRole, kNumberRole], values => values.Sum());
        TestBlock root = Parent("root", [sum], [kNumberRole], values => values[0] * 2);

        PositioningBlockResult<int> result = await Execute(root, 0);

        Assert.That(result, Is.TypeOf<PositioningBlockSuccess<int>>());
        Assert.That(((PositioningBlockSuccess<int>)result).Value, Is.EqualTo(10));
    }

    [Test]
    public async Task ChildFailure_IsPropagatedUnchangedAndShortCircuitsSiblingsAndParent()
    {
        PositioningBlockFailure<int> expectedFailure = new(
            PositioningBlockFailureCategory.ScopeNotFound,
            "missing-child");
        TestBlock failing = new("missing-child", kNumberRole, [], [], (_, _, _) =>
            Task.FromResult<PositioningBlockResult<int>>(expectedFailure));
        TestBlock skipped = Leaf("skipped", 4);
        TestBlock parent = Parent("parent", [failing, skipped], [kNumberRole, kNumberRole], values => values.Sum());

        PositioningBlockResult<int> result = await Execute(parent, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(expectedFailure));
            Assert.That(skipped.EvaluationCount, Is.Zero);
            Assert.That(parent.EvaluationCount, Is.Zero);
        });
    }

    [Test]
    public void Recipe_RejectsNullChild()
    {
        TestBlock parent = Parent("parent", [null!], [kNumberRole], values => values.Sum());

        PositioningRecipeException exception = Assert.Throws<PositioningRecipeException>(() =>
            new PositioningRecipe<int, int>(parent))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(PositioningBlockFailureCategory.InvalidRecipe));
            Assert.That(exception.OriginatingBlockKind, Is.EqualTo("parent"));
        });
    }

    [Test]
    public void Recipe_RejectsIncompatibleChildRole()
    {
        TestBlock child = new("child", kOtherRole, [], [], Success(1));
        TestBlock parent = Parent("parent", [child], [kNumberRole], values => values[0]);

        Assert.Throws<PositioningRecipeException>(() => new PositioningRecipe<int, int>(parent));
    }

    [Test]
    public void Recipe_RejectsMissingRoleAndInconsistentChildDeclarations()
    {
        TestBlock missingOutputRole = new("missing-role", default, [], [], Success(1));
        TestBlock inconsistent = Parent("inconsistent", [Leaf("child", 1)], [], values => values[0]);

        Assert.Multiple(() =>
        {
            Assert.Throws<PositioningRecipeException>(() =>
                new PositioningRecipe<int, int>(missingOutputRole));
            Assert.Throws<PositioningRecipeException>(() =>
                new PositioningRecipe<int, int>(inconsistent));
        });
    }

    [Test]
    public void Recipe_RejectsCycles()
    {
        TestBlock first = Leaf("first", 1);
        TestBlock second = Parent("second", [first], [kNumberRole], values => values[0]);
        first.MutableChildren.Add(second);
        first.MutableChildRoles.Add(kNumberRole);

        PositioningRecipeException exception = Assert.Throws<PositioningRecipeException>(() =>
            new PositioningRecipe<int, int>(second))!;

        Assert.That(exception.Category, Is.EqualTo(PositioningBlockFailureCategory.InvalidRecipe));
    }

    [Test]
    public async Task Recipe_SnapshotsChildrenAtConstruction()
    {
        TestBlock original = Leaf("original", 5);
        TestBlock replacement = Leaf("replacement", 99);
        TestBlock parent = Parent("parent", [original], [kNumberRole], values => values[0]);
        PositioningRecipe<int, int> recipe = new(parent);
        parent.MutableChildren[0] = replacement;

        PositioningBlockResult<int> result = await new PositioningRecipeExecutor<int, int>()
            .ExecuteAsync(recipe, 0);

        Assert.That(((PositioningBlockSuccess<int>)result).Value, Is.EqualTo(5));
        Assert.That(replacement.EvaluationCount, Is.Zero);
    }

    [Test]
    public void Cancellation_IsPropagated()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Execute(Leaf("leaf", 1), 0, source.Token));
    }

    [Test]
    public async Task UnexpectedException_ReturnsSafeFailureForOriginatingBlock()
    {
        const string secret = "credential-secret-4711";
        TestBlock throwing = new("throwing", kNumberRole, [], [], (_, _, _) =>
            throw new InvalidOperationException(secret));

        PositioningBlockResult<int> result = await Execute(throwing, 0);

        Assert.That(result, Is.TypeOf<PositioningBlockFailure<int>>());
        PositioningBlockFailure<int> failure = (PositioningBlockFailure<int>)result;
        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.UnexpectedExecutionFailure));
            Assert.That(failure.OriginatingBlockKind, Is.EqualTo("throwing"));
            Assert.That(failure.Diagnostic, Does.Not.Contain(secret));
        });
    }

    [Test]
    public async Task ConcurrentEvaluations_KeepContextAndStateIndependent()
    {
        TestBlock echo = new("echo", kNumberRole, [], [], async (context, _, token) =>
        {
            await Task.Delay(10, token);
            return new PositioningBlockSuccess<int>(context);
        });
        PositioningRecipe<int, int> recipe = new(echo);
        PositioningRecipeExecutor<int, int> executor = new();

        PositioningBlockResult<int>[] results = await Task.WhenAll(
            Enumerable.Range(1, 20).Select(value => executor.ExecuteAsync(recipe, value)));

        Assert.That(
            results.Cast<PositioningBlockSuccess<int>>().Select(result => result.Value),
            Is.EqualTo(Enumerable.Range(1, 20)));
    }

    [Test]
    public async Task NullRecipe_ReturnsTypedInvalidRecipeFailure()
    {
        PositioningBlockResult<int> result = await new PositioningRecipeExecutor<int, int>()
            .ExecuteAsync(null, 0);

        PositioningBlockFailure<int> failure = (PositioningBlockFailure<int>)result;
        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(PositioningBlockFailureCategory.InvalidRecipe));
            Assert.That(failure.OriginatingBlockKind, Is.EqualTo("recipe"));
        });
    }

    /// <summary>
    /// Executes one block as a complete recipe.
    /// </summary>
    private static Task<PositioningBlockResult<int>> Execute(
        TestBlock root,
        int context,
        CancellationToken cancellationToken = default)
    {
        return new PositioningRecipeExecutor<int, int>()
            .ExecuteAsync(new PositioningRecipe<int, int>(root), context, cancellationToken);
    }

    private static TestBlock Leaf(string kind, int value) =>
        new(kind, kNumberRole, [], [], Success(value));

    private static TestBlock Parent(
        string kind,
        IEnumerable<IPositioningBlock<int, int>> children,
        IEnumerable<PositioningBlockRole> childRoles,
        Func<IReadOnlyList<int>, int> evaluate) =>
        new(kind, kNumberRole, children, childRoles, (_, values, _) =>
            Task.FromResult<PositioningBlockResult<int>>(
                new PositioningBlockSuccess<int>(evaluate(values))));

    private static Func<int, IReadOnlyList<int>, CancellationToken, Task<PositioningBlockResult<int>>> Success(
        int value) => (_, _, _) => Task.FromResult<PositioningBlockResult<int>>(
            new PositioningBlockSuccess<int>(value));

    private sealed class TestBlock : IPositioningBlock<int, int>
    {
        private readonly Func<int, IReadOnlyList<int>, CancellationToken, Task<PositioningBlockResult<int>>> evaluation;

        public string Kind { get; }
        public PositioningBlockRole OutputRole { get; }
        public List<IPositioningBlock<int, int>> MutableChildren { get; }
        public List<PositioningBlockRole> MutableChildRoles { get; }
        public IReadOnlyList<IPositioningBlock<int, int>> Children => MutableChildren;
        public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => MutableChildRoles;
        public int EvaluationCount { get; private set; }

        public TestBlock(
            string kind,
            PositioningBlockRole outputRole,
            IEnumerable<IPositioningBlock<int, int>> children,
            IEnumerable<PositioningBlockRole> childRoles,
            Func<int, IReadOnlyList<int>, CancellationToken, Task<PositioningBlockResult<int>>> evaluation)
        {
            Kind = kind;
            OutputRole = outputRole;
            MutableChildren = children.ToList();
            MutableChildRoles = childRoles.ToList();
            this.evaluation = evaluation;
        }

        public Task<PositioningBlockResult<int>> EvaluateAsync(
            int context,
            IReadOnlyList<int> childValues,
            CancellationToken cancellationToken)
        {
            EvaluationCount++;
            return evaluation(context, childValues, cancellationToken);
        }
    }
}
