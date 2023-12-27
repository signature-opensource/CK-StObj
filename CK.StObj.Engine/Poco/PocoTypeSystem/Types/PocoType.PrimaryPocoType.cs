using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        public static PrimaryPocoType CreatePrimaryPoco( PocoTypeSystem s, IPocoFamilyInfo family )
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

                IAbstractPocoType IReadOnlyList<IAbstractPocoType>.this[int index] => NonNullable.AbstractTypes[index].Nullable;

                IEnumerator<IAbstractPocoType> IEnumerable<IAbstractPocoType>.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => NonNullable.AbstractTypes.Select( a => a.Nullable ).GetEnumerator();
            }

            readonly IPocoFieldDefaultValue _def;
            readonly IPocoFamilyInfo _familyInfo;
            [AllowNull] PrimaryPocoField[] _fields;
            [AllowNull] string _ctorCode;

            public PrimaryPocoType( PocoTypeSystem s,
                                    IPocoFamilyInfo family,
                                    Type primaryInterface )
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.PrimaryPoco, t => new Null( t ) )
            {
                _familyInfo = family;
                // The full name is the ImplTypeName. This works because the generated type is not a nested type (and not a generic of course).
                Throw.DebugAssert( !family.PocoClass.FullName!.Contains( '+' ) );
                _def = new FieldDefaultValue( $"new {family.PocoClass.FullName}()" );
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
                    if( !_fields.Any( f => f.IsExchangeable ) )
                    {
                        SetNotExchangeable( monitor, $"its last field type '{r.Type}' becomes not exchangeable." );
                    }
                }
            }

            internal bool SetFields( IActivityMonitor monitor,
                                     PocoTypeSystem.IStringBuilderPool sbPool,
                                     PrimaryPocoField[] fields )
            {
                _fields = fields;
                var d = CompositeHelper.CreateDefaultValueInfo( monitor, sbPool, this );
                if( d.IsDisallowed )
                {
                    monitor.Error( $"Unable to create '{CSharpName}' constructor code. See previous errors." );
                    return false;
                }
                _ctorCode = d.RequiresInit ? d.DefaultValue.ValueCSharpSource : String.Empty;
                // Sets the initial IsExchangeable status.
                if( !_fields.Any( f => f.IsExchangeable ) )
                {
                    SetNotExchangeable( monitor, $"none of its {_fields.Length} fields are exchangeable." );
                }
                return true;
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

            public override bool IsReadableType( IPocoType type )
            {
                // type.IsNullable may be true: we don't care.
                if( type == this || type.Kind == PocoTypeKind.Any ) return true;
                if( type.Kind == PocoTypeKind.SecondaryPoco )
                {
                    return ((ISecondaryPocoType)type).PrimaryPocoType == this;
                }
                if( type.Kind == PocoTypeKind.AbstractPoco )
                {
                    var t = type.Type;
                    return t == typeof(IPoco)
                           || (FamilyInfo.IsClosedPoco && t == typeof( IClosedPoco ))
                           || FamilyInfo.OtherInterfaces.Any( i => i == t );
                }
                return false;
            }

            public override bool IsWritableType( IPocoType type )
            {
                return type == this
                       || (!type.IsNullable && FamilyInfo.Interfaces.Any( i => i.PocoInterface == type.Type ));
            }

            [AllowNull]
            public IReadOnlyList<IAbstractPocoType> AbstractTypes { get; internal set; }

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            IPrimaryPocoType IPrimaryPocoType.Nullable => Nullable;

            IPrimaryPocoType IPrimaryPocoType.NonNullable => this;
        }
    }

}



