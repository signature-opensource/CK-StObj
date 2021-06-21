using System;

namespace CK.Setup.Json
{
    public readonly struct ECMAScriptStandardJsonName : IEquatable<ECMAScriptStandardJsonName>
    {
        public bool IsCanonical { get; }

        public string Name { get; }

        public ECMAScriptStandardJsonName( string name, bool isCanonical )
        {
            IsCanonical = isCanonical;
            Name = name;
        }

        public override bool Equals( object? obj ) => obj is ECMAScriptStandardJsonName o ? Equals( o ) : false;

        public bool Equals( ECMAScriptStandardJsonName o ) => Name == o.Name;

        public override int GetHashCode() => Name.GetHashCode( StringComparison.Ordinal );

        public static bool operator ==( ECMAScriptStandardJsonName left, ECMAScriptStandardJsonName right ) => left.Equals( right );

        public static bool operator !=( ECMAScriptStandardJsonName left, ECMAScriptStandardJsonName right ) => !(left == right);

    }
}
