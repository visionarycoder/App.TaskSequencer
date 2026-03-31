namespace Core.Models
{
    public class Identifier : IComparable<Identifier>, IEquatable<Identifier>
    {
        public string Value { get; }

        public Identifier(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int CompareTo(Identifier? other)
        {
            if (other is null)
                return 1;
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(Identifier? other)
        {
            return other is not null && Value.Equals(other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Identifier);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Identifier? left, Identifier? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Identifier left, Identifier right)
        {
            return !(left == right);
        }

        public static bool operator <(Identifier? left, Identifier right)
        {
            return left is not null && left.CompareTo(right) < 0;
        }

        public static bool operator <=(Identifier? left, Identifier right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        public static bool operator >(Identifier? left, Identifier right)
        {
            return left is not null && left.CompareTo(right) > 0;
        }

        public static bool operator >=(Identifier? left, Identifier? right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }
    }
}
