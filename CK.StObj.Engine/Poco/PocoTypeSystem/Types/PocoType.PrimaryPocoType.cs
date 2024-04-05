using CK.CodeGen;
using CK.Core;
using CommunityToolkit.HighPerformance;
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
        internal static PrimaryPocoType CreatePrimaryPoco( PocoTypeSystemBuilder s, IPocoFamilyInfo family )
        {
            return new PrimaryPocoType( s, family, family.PrimaryInterface.PocoInterface );
        }

        internal sealed class PrimaryPocoType : PocoType, IPrimaryPocoType
        {
            // Auto implementation of IReadOnlyList<IAbstractPocoType> AbstractTypes.
            // AbstractTypes is implemented by an adapter.
            sealed class Null : NullReferenceType, IPrimaryPocoType, IReadOnlyList<IAbstractPocoType>
            {
                [AllowNull] internal string _extOrCSName;

                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                public new PrimaryPocoType NonNullable => Unsafe.As<PrimaryPocoType>( base.NonNullable );

                public IPocoFamilyInfo FamilyInfo => NonNullable.FamilyInfo;

                public IReadOnlyList<IPrimaryPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                public IEnumerable<ISecondaryPocoType> SecondaryTypes => NonNullable.SecondaryTypes.Select( s => s.Nullable );

                public override IPrimaryPocoType ObliviousType => this;
                ICompositePocoType ICompositePocoType.ObliviousType => this;

                IPrimaryPocoType IPrimaryPocoType.Nullable => this;
                IPrimaryPocoType IPrimaryPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                INamedPocoType INamedPocoType.Nullable => this;
                INamedPocoType INamedPocoType.NonNullable => NonNullable;

                public string CSharpBodyConstructorSourceCode => NonNullable.CSharpBodyConstructorSourceCode;

                public IReadOnlyList<IAbstractPocoType> AbstractTypes => this;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => _extOrCSName;

                public IEnumerable<IAbstractPocoType> MinimalAbstractTypes => NonNullable.MinimalAbstractTypes.Select( a => a.Nullable );

                #region Auto implementation of AbstractTypes
                int IReadOnlyCollection<IAbstractPocoType>.Count => NonNullable.AbstractTypes.Count;

                public IReadOnlyList<IAbstractPocoType> AllAbstractTypes => NonNullable._allAbstractHolder;

                IAbstractPocoType IReadOnlyList<IAbstractPocoType>.this[int index] => NonNullable.AbstractTypes[index].Nullable;

                IEnumerator<IAbstractPocoType> IEnumerable<IAbstractPocoType>.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();
                #endregion

            }

            /// <summary>
            /// Holds and manage the <see cref="AllAbstractTypes"/> and <see cref="AbstractTypes"/>, implements
            /// the <see cref="Null.AllAbstractTypes"/> (of nullables). The <see cref="Null.AbstractTypes"/> is auto implemented
            /// by the <see cref="Null"/>.
            /// </summary>
            sealed class AllAbstractHolder : IReadOnlyList<IAbstractPocoType>
            {
                readonly IAbstractPocoType[] _allAbstractTypes;
                ArraySegment<IAbstractPocoType> _abstractTypes;

                public AllAbstractHolder( IAbstractPocoType[] allAbstractTypes )
                {
                    _allAbstractTypes = allAbstractTypes;
                    _abstractTypes = allAbstractTypes;
                }

                public IReadOnlyList<IAbstractPocoType> AllAbstractTypes => _allAbstractTypes;

                public IReadOnlyList<IAbstractPocoType> AbstractTypes => _abstractTypes;

                public void OnAbstractImplementationLess( IAbstractPocoType a )
                {
                    Throw.DebugAssert( _allAbstractTypes.Contains( a ) );
                    int idx = Array.IndexOf( _allAbstractTypes, a );
                    Array.Copy( _allAbstractTypes, idx + 1, _allAbstractTypes, idx, _allAbstractTypes.Length - idx - 1 );
                    _allAbstractTypes[^1] = a;
                    _abstractTypes = new ArraySegment<IAbstractPocoType>( _allAbstractTypes, 0, _abstractTypes.Count - 1 );
                }

                IAbstractPocoType IReadOnlyList<IAbstractPocoType>.this[int index] => _allAbstractTypes[index].Nullable;

                int IReadOnlyCollection<IAbstractPocoType>.Count => _allAbstractTypes.Length;

                IEnumerator<IAbstractPocoType> IEnumerable<IAbstractPocoType>.GetEnumerator() => _allAbstractTypes.Select( t => t.Nullable ).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_allAbstractTypes.Select( t => t.Nullable )).GetEnumerator();
            }

            readonly IPocoFieldDefaultValue _def;
            readonly IPocoFamilyInfo _familyInfo;
            [AllowNull] PrimaryPocoField[] _fields;
            ISecondaryPocoType[] _secondaryTypes;
            string _ctorCode;
            IReadOnlyList<IAbstractPocoType>? _minimalAbstractTypes;
            [AllowNull] AllAbstractHolder _allAbstractHolder;

            public PrimaryPocoType( PocoTypeSystemBuilder s,
                                    IPocoFamilyInfo family,
                                    Type primaryInterface )
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.PrimaryPoco, static t => new Null( t ) )
            {
                _familyInfo = family;
                // The full name is the ImplTypeName. This works because the generated type is not a nested type (and not a generic of course).
                Throw.DebugAssert( !family.PocoClass.FullName!.Contains( '+' ) );
                _def = new FieldDefaultValue( $"new {family.PocoClass.FullName}()" );
                _secondaryTypes = Array.Empty<ISecondaryPocoType>();
                // Constructor will remain the empty string when all fields are DefaultValueInfo.IsAllowed (their C# default value).
                _ctorCode = string.Empty;
                if( _familyInfo.ExternalName != null )
                {
                    Nullable._extOrCSName = _familyInfo.ExternalName.Name + '?';
                }
                else
                {
                    Nullable._extOrCSName = Nullable.CSharpName;
                }
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new Null Nullable => Unsafe.As<Null>( _nullable );

            public IPocoFamilyInfo FamilyInfo => _familyInfo;

            public IPrimaryPocoType PrimaryInterface => this;

            public override IPrimaryPocoType ObliviousType => Nullable;
            // Required... C# "Covarint return type" can do better!
            ICompositePocoType ICompositePocoType.ObliviousType => Nullable;

            public ExternalNameAttribute? ExternalName => _familyInfo.ExternalName;

            public string ExternalOrCSharpName => _familyInfo.ExternalName?.Name ?? CSharpName;

            public override string ImplTypeName => _familyInfo.PocoClass.FullName!;

            public string CSharpBodyConstructorSourceCode => _ctorCode;

            public IReadOnlyList<IPrimaryPocoField> Fields => _fields;

            internal void SetFields( PrimaryPocoField[] fields ) => _fields = fields;

            internal void ComputeCtorCode( PocoTypeSystemBuilder.IStringBuilderPool sbPool )
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

            public IEnumerable<ISecondaryPocoType> SecondaryTypes => _secondaryTypes;

            internal void SetSecondaryTypes( ISecondaryPocoType[] secondaryTypes ) => _secondaryTypes = secondaryTypes;

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
                    return type.Type == typeof( IPoco ) || _allAbstractHolder.AllAbstractTypes.Contains( type );
                }
                return false;
            }

            public IReadOnlyList<IAbstractPocoType> AbstractTypes => _allAbstractHolder.AbstractTypes;

            public IReadOnlyList<IAbstractPocoType> AllAbstractTypes => _allAbstractHolder.AllAbstractTypes;

            internal void SetAllAbstractTypes( IAbstractPocoType[] types )
            {
                _allAbstractHolder = new AllAbstractHolder( types );
            }

            internal void OnAbstractImplementationLess( IAbstractPocoType a )
            {
                Throw.DebugAssert( "ImplementationLess must be done in the initialization step.", _minimalAbstractTypes == null );
                Throw.DebugAssert( _allAbstractHolder != null );
                _allAbstractHolder.OnAbstractImplementationLess( a );
            }

            public IEnumerable<IAbstractPocoType> MinimalAbstractTypes => _minimalAbstractTypes ??= _allAbstractHolder.AbstractTypes.ComputeMinimal();

            ICompositePocoType ICompositePocoType.Nullable => Nullable;
            ICompositePocoType ICompositePocoType.NonNullable => this;

            IPrimaryPocoType IPrimaryPocoType.Nullable => Nullable;
            IPrimaryPocoType IPrimaryPocoType.NonNullable => this;

            INamedPocoType INamedPocoType.Nullable => Nullable;
            INamedPocoType INamedPocoType.NonNullable => this;
        }
    }

}



