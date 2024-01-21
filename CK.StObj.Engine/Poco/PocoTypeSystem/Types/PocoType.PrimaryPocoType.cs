using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static PrimaryPocoType CreatePrimaryPoco( PocoTypeSystem s, IPocoFamilyInfo family )
        {
            return new PrimaryPocoType( s, family, family.PrimaryInterface.PocoInterface );
        }

        internal sealed class PrimaryPocoType : PocoType, IPrimaryPocoType
        {
            // Auto implementation of IReadOnlyList<IAbstractPocoType> AbstractTypes.
            sealed class Null : NullReferenceType, IPrimaryPocoType, IReadOnlyList<IAbstractPocoType>
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new PrimaryPocoType NonNullable => Unsafe.As<PrimaryPocoType>( base.NonNullable );

                public IPocoFamilyInfo FamilyInfo => NonNullable.FamilyInfo;

                public IReadOnlyList<IPrimaryPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                public IEnumerable<ISecondaryPocoType> SecondaryTypes => NonNullable.SecondaryTypes.Select( s => s.Nullable );

                ICompositePocoType ICompositePocoType.ObliviousType => NonNullable;

                IPrimaryPocoType IPrimaryPocoType.Nullable => this;

                IPrimaryPocoType IPrimaryPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                public string CSharpBodyConstructorSourceCode => NonNullable.CSharpBodyConstructorSourceCode;

                public IReadOnlyList<IAbstractPocoType> AbstractTypes => this;

                int IReadOnlyCollection<IAbstractPocoType>.Count => NonNullable.AbstractTypes.Count;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => NonNullable.ExternalOrCSharpName;

                public IEnumerable<IAbstractPocoType> MinimalAbstractTypes => NonNullable.MinimalAbstractTypes.Select( a => a.Nullable );

                IAbstractPocoType IReadOnlyList<IAbstractPocoType>.this[int index] => NonNullable.AbstractTypes[index].Nullable;

                IEnumerator<IAbstractPocoType> IEnumerable<IAbstractPocoType>.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();
            }

            readonly IPocoFieldDefaultValue _def;
            readonly IPocoFamilyInfo _familyInfo;
            [AllowNull] IAbstractPocoType[] _abstractTypes;
            [AllowNull] PrimaryPocoField[] _fields;
            string _ctorCode;
            IReadOnlyList<IAbstractPocoType>? _minimalAbstractTypes;

            public PrimaryPocoType( PocoTypeSystem s,
                                    IPocoFamilyInfo family,
                                    Type primaryInterface )
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.PrimaryPoco, static t => new Null( t ) )
            {
                _familyInfo = family;
                // The full name is the ImplTypeName. This works because the generated type is not a nested type (and not a generic of course).
                Throw.DebugAssert( !family.PocoClass.FullName!.Contains( '+' ) );
                _def = new FieldDefaultValue( $"new {family.PocoClass.FullName}()" );
                // Constructor will remain the empty string when all fields are DefaultValueInfo.IsAllowed (their C# default value).
                _ctorCode = string.Empty;
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new IPrimaryPocoType Nullable => Unsafe.As<IPrimaryPocoType>( _nullable );

            public IPocoFamilyInfo FamilyInfo => _familyInfo;

            public IPrimaryPocoType PrimaryInterface => this;

            ICompositePocoType ICompositePocoType.ObliviousType => this;

            public override string ImplTypeName => _familyInfo.PocoClass.FullName!;

            public string CSharpBodyConstructorSourceCode => _ctorCode;

            public IReadOnlyList<IPrimaryPocoField> Fields => _fields;

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                Throw.DebugAssert( r != null && _fields.Any( f => f == r ) && !r.Type.IsExchangeable );
                if( IsExchangeable )
                {
                    CompositeHelper.OnNoMoreExchangeable( monitor, this, _fields, r );
                }
            }

            internal bool SetFields( IActivityMonitor monitor, PrimaryPocoField[] fields )
            {
                _fields = fields;
                // Sets the initial IsExchangeable status.
                // A composite with no field is not exchangeable.
                CompositeHelper.CheckInitialExchangeable( monitor, this, _fields );
                return true;
            }

            internal void ComputeCtorCode( PocoTypeSystem.IStringBuilderPool sbPool )
            {
                var b = sbPool.Get();
                foreach( var f in _fields )
                {
                    var fInfo = f.DefaultValueInfo;
                    Throw.DebugAssert( "This has been detected by the PocoCycleAndDefaultVisitor.", !fInfo.IsDisallowed );
                    // If the field is Allowed, skip it.
                    if( fInfo.IsAllowed ) continue;
                    Throw.DebugAssert( fInfo.RequiresInit );
                    // Generate the source code for the initialization.
                    if( b.Length > 0 )
                    {
                        b.Append( ';' ).Append( Environment.NewLine );
                    }
                    b.Append( f.PrivateFieldName ).Append( " = " ).Append( fInfo.DefaultValue.ValueCSharpSource );
                }
                // If no field have been initialized, the default value is useless => the _ctorCode is let to the string.Empty.
                if( b.Length > 0 )
                {
                    b.Append( ';' );
                    _ctorCode = b.ToString();
                }
                sbPool.Return( b );
            }

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            public ExternalNameAttribute? ExternalName => _familyInfo.ExternalName;

            public string ExternalOrCSharpName => _familyInfo.ExternalName?.Name ?? CSharpName;

            public IEnumerable<ISecondaryPocoType> SecondaryTypes
            {
                get
                {
                    var b = FirstBackReference;
                    while( b != null )
                    {
                        if( b is ISecondaryPocoType sec ) yield return sec;
                        b = b.NextRef;
                    }
                }
            }

            public override bool CanReadFrom( IPocoType type )
            {
                // type.IsNullable may be true: we don't care.
                if( type.NonNullable == this || type.Kind == PocoTypeKind.Any ) return true;
                if( type.Kind == PocoTypeKind.SecondaryPoco )
                {
                    return ((ISecondaryPocoType)type).PrimaryPocoType == this;
                }
                if( type.Kind == PocoTypeKind.AbstractPoco )
                {
                    var t = type.Type;
                    return t == typeof( IPoco )
                           || (FamilyInfo.IsClosedPoco && t == typeof( IClosedPoco ))
                           || FamilyInfo.OtherInterfaces.Any( i => i == t );
                }
                return false;
            }

            public IReadOnlyList<IAbstractPocoType> AbstractTypes => _abstractTypes;

            internal void SetAbstractTypes( IAbstractPocoType[] types ) => _abstractTypes = types;

            public IEnumerable<IAbstractPocoType> MinimalAbstractTypes => _minimalAbstractTypes ??= AbstractPocoType.ComputeMinimal( _abstractTypes );

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            IPrimaryPocoType IPrimaryPocoType.Nullable => Nullable;

            IPrimaryPocoType IPrimaryPocoType.NonNullable => this;
        }
    }

}



