namespace Reconcile.Net.Internal;

internal readonly struct CompositeKey : IEquatable<CompositeKey>
{
    private readonly object?[]? _parts;
    private readonly int _hashCode;

    public CompositeKey(object?[] parts)
    {
        _parts = parts;
        var hash = default(HashCode);
        foreach (var part in parts)
        {
            hash.Add(part);
        }

        _hashCode = hash.ToHashCode();
    }

    private object?[] Parts => _parts ?? Array.Empty<object?>();

    public bool Equals(CompositeKey other)
    {
        var parts = Parts;
        var otherParts = other.Parts;
        if (parts.Length != otherParts.Length)
        {
            return false;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            if (!Equals(parts[i], otherParts[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is CompositeKey other && Equals(other);

    public override int GetHashCode() => _hashCode;
}
