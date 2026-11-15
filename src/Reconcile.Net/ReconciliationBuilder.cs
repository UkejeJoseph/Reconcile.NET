using Reconcile.Net.Internal;

namespace Reconcile.Net;

/// <summary>Declares match passes over two record streams and runs the reconciliation.</summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
public sealed class ReconciliationBuilder<TLeft, TRight>
{
    private readonly IEnumerable<TLeft> _left;
    private readonly IEnumerable<TRight> _right;
    private readonly List<PassDefinition<TLeft, TRight>> _passes = new();
    private PairPassDefinition<TLeft, TRight>? _openingPass;
    private bool _openingPassSealed;
    private ReconciliationOptions _options = new();

    internal ReconciliationBuilder(IEnumerable<TLeft> left, IEnumerable<TRight> right)
    {
        _left = left;
        _right = right;
    }

    /// <summary>
    /// Applies optional guard rails to this reconciliation, such as a ceiling on the remainder product
    /// an unkeyed pass may run over. Options are off by default, so calling this is never required.
    /// </summary>
    /// <param name="options">The options to apply. Replaces any previously supplied options.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="options"/> is null.</exception>
    public ReconciliationBuilder<TLeft, TRight> WithOptions(ReconciliationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    /// <summary>
    /// Adds an exact key to the first match pass. Calling it more than once builds a composite key:
    /// records match only when every key part is equal.
    /// Null never equals null: a record whose key selector returns null (any part of a composite key)
    /// never key-matches on that pass. It stays available to later passes and otherwise surfaces in
    /// the unmatched lists, where a blank-reference row belongs.
    /// </summary>
    /// <typeparam name="TKey">The key type. Compared with its default equality.</typeparam>
    /// <param name="leftKey">Selects the key from a left record.</param>
    /// <param name="rightKey">Selects the key from a right record.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="InvalidOperationException">When called after <see cref="ThenMatchOn"/> or <see cref="ThenMatchGroups"/>.</exception>
    public ReconciliationBuilder<TLeft, TRight> MatchOn<TKey>(
        Func<TLeft, TKey> leftKey,
        Func<TRight, TKey> rightKey)
    {
        ArgumentNullException.ThrowIfNull(leftKey);
        ArgumentNullException.ThrowIfNull(rightKey);
        if (_openingPassSealed)
        {
            throw new InvalidOperationException(
                $"{nameof(MatchOn)} defines the first pass and must be called before " +
                $"{nameof(ThenMatchOn)} or {nameof(ThenMatchGroups)}. Use {nameof(ThenMatchOn)} to add later passes.");
        }

        if (_openingPass is null)
        {
            _openingPass = new PairPassDefinition<TLeft, TRight>();
            _passes.Add(_openingPass);
        }

        _openingPass.KeyParts.Add(KeyPart<TLeft, TRight>.Create(leftKey, rightKey));
        return this;
    }

    /// <summary>
    /// Adds a match pass that runs only on the records left unmatched by all earlier passes.
    /// Passes run in declaration order.
    /// </summary>
    /// <param name="configure">Declares the pass criteria: <see cref="MatchPassBuilder{TLeft, TRight}.Key{TKey}"/>,
    /// <see cref="MatchPassBuilder{TLeft, TRight}.Amount"/>, <see cref="MatchPassBuilder{TLeft, TRight}.AmountPercent"/>,
    /// and <see cref="MatchPassBuilder{TLeft, TRight}.Date(Func{TLeft, DateTime}, Func{TRight, DateTime}, TimeSpan)"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">When the configured pass declares no criteria.</exception>
    public ReconciliationBuilder<TLeft, TRight> ThenMatchOn(
        Action<MatchPassBuilder<TLeft, TRight>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _openingPassSealed = true;
        var pass = new PairPassDefinition<TLeft, TRight>();
        configure(new MatchPassBuilder<TLeft, TRight>(pass));
        if (!pass.HasCriteria)
        {
            throw new InvalidOperationException(
                "A match pass must define at least one criterion: Key, Amount, or Date.");
        }

        _passes.Add(pass);
        return this;
    }

    /// <summary>
    /// Adds a one-to-many pass: each right record is matched against the sum of the amounts of all
    /// remaining left records that share its group key. The whole group is claimed together.
    /// </summary>
    /// <param name="configure">Declares the group key and the amount criterion. Both are required.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">When the configured pass lacks a key or an amount criterion.</exception>
    public ReconciliationBuilder<TLeft, TRight> ThenMatchGroups(
        Action<GroupPassBuilder<TLeft, TRight>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _openingPassSealed = true;
        var pass = new GroupPassDefinition<TLeft, TRight>();
        configure(new GroupPassBuilder<TLeft, TRight>(pass));
        if (!pass.HasRequiredCriteria)
        {
            throw new InvalidOperationException(
                "A group pass requires at least one Key and an Amount criterion.");
        }

        _passes.Add(pass);
        return this;
    }

    /// <summary>Enumerates both streams once and runs the declared passes in order.</summary>
    /// <returns>An immutable result with matched, unmatched, and summary views.</returns>
    /// <exception cref="InvalidOperationException">When no pass has been declared.</exception>
    public ReconciliationResult<TLeft, TRight> Run()
    {
        if (_passes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Define at least one match pass ({nameof(MatchOn)}, {nameof(ThenMatchOn)}, " +
                $"or {nameof(ThenMatchGroups)}) before calling {nameof(Run)}.");
        }

        return MatchingEngine.Run(_left, _right, _passes, _options);
    }
}
