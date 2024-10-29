using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

partial class CachedType
{
    static readonly NullMarker _nullMarker = new NullMarker();

    sealed class NullMarker : ICachedType
    {
        public Type Type => throw new NotImplementedException();

        public bool IsGenericType => throw new NotImplementedException();

        public bool IsTypeDefinition => throw new NotImplementedException();

        public string CSharpName => throw new NotImplementedException();

        public CachedAssembly Assembly => throw new NotImplementedException();

        public bool IsNullable => throw new NotImplementedException();

        public ICachedType Nullable => throw new NotImplementedException();

        public ICachedType NonNullable => throw new NotImplementedException();

        public ImmutableArray<ICachedType> Interfaces => throw new NotImplementedException();

        public ICachedType? BaseType => throw new NotImplementedException();

        public int TypeDepth => throw new NotImplementedException();

        public ICachedType? GenericTypeDefinition => throw new NotImplementedException();

        public ICachedType? DeclaringType => throw new NotImplementedException();

        public ImmutableArray<ICachedType> GenericArguments => throw new NotImplementedException();

        public ImmutableArray<ICachedMember> DeclaredMembers => throw new NotImplementedException();

        public ImmutableArray<ICachedType> DeclaredBaseTypes => throw new NotImplementedException();

        public GlobalTypeCache TypeCache => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public ImmutableArray<CustomAttributeData> CustomAttributes => throw new NotImplementedException();

        public ICachedType? ElementType => throw new NotImplementedException();

        public TypeKind Kind => throw new NotImplementedException();

        public StringBuilder Write( StringBuilder b ) => throw new NotImplementedException();

        public override string ToString() => throw new NotImplementedException();

        internal static ICachedType? Filter( ICachedType declaringType ) => declaringType == _nullMarker ? null : declaringType;
    }
}
