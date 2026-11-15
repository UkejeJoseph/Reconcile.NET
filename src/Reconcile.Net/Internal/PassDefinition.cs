namespace Reconcile.Net.Internal;

internal abstract class PassDefinition<TLeft, TRight>
{
    private const int SingleKeyPartCount = 1;
    private const string KeyToken = "Key";
    private const string CompositeKeyTokenFormat = "Key[{0}]";
    private const string CriteriaSeparator = " + ";

    public List<KeyPart<TLeft, TRight>> KeyParts { get; } = new();

    public AmountCriterion<TLeft, TRight>? Amount { get; set; }

    public abstract void Execute(
        MatchContext<TLeft, TRight> context, int passNumber, ReconciliationOptions options);

    public abstract string Describe();

    protected bool TryLeftKeyOf(TLeft item, out CompositeKey key)
        => TryKeyOf(item, static (part, value) => part.Left(value), out key);

    protected bool TryRightKeyOf(TRight item, out CompositeKey key)
        => TryKeyOf(item, static (part, value) => part.Right(value), out key);

    protected string DescribeCriteria(DateCriterion<TLeft, TRight>? date)
    {
        var tokens = new List<string>();
        if (KeyParts.Count == SingleKeyPartCount)
        {
            tokens.Add(KeyToken);
        }
        else if (KeyParts.Count > SingleKeyPartCount)
        {
            tokens.Add(string.Format(
                System.Globalization.CultureInfo.InvariantCulture, CompositeKeyTokenFormat, KeyParts.Count));
        }

        if (Amount is not null)
        {
            tokens.Add(Amount.Describe());
        }

        if (date is not null)
        {
            tokens.Add(date.Describe());
        }

        return string.Join(CriteriaSeparator, tokens);
    }

    private bool TryKeyOf<TItem>(
        TItem item,
        Func<KeyPart<TLeft, TRight>, TItem, object?> selectPart,
        out CompositeKey key)
    {
        var values = new object?[KeyParts.Count];
        for (var i = 0; i < KeyParts.Count; i++)
        {
            var value = selectPart(KeyParts[i], item);
            if (value is null)
            {
                key = default;
                return false;
            }

            values[i] = value;
        }

        key = new CompositeKey(values);
        return true;
    }
}
