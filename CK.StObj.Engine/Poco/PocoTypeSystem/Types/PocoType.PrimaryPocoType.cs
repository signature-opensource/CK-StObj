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

                public IPrimaryPocoType PrimaryInterface => this;

                public IReadOnlyList<IPrimaryPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                public string CSharpBodyConstructorSourceCode => NonNullable.CSharpBodyConstructorSourceCode;

                IPrimaryPocoType IPrimaryPocoType.Nullable => this;

                IPrimaryPocoType IPrimaryPocoType.NonNullable => NonNullable;

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
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.IPoco, t => new Null( t ) )
            {
                _familyInfo = family;
                _def = new FieldDefaultValue( Activator.CreateInstance( family.PocoClass )!, $"new {family.PocoClass.Name}()" );
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new IPrimaryPocoType Nullable => Unsafe.As<IPrimaryPocoType>( base.Nullable );

            public IPocoFamilyInfo FamilyInfo => _familyInfo;

            public IPrimaryPocoType PrimaryInterface => this;

            public string CSharpBodyConstructorSourceCode => _ctorCode;

            public IReadOnlyList<IPrimaryPocoField> Fields => _fields;

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                Debug.Assert( r != null && _fields.Any( f => f == r ) && !r.Type.IsExchangeable );
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

            public override bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
            {
                if( !ignoreRootTypeIsNullable && type.IsNullable ) return false;
                return FamilyInfo.Interfaces.Any( i => i.PocoInterface == type.Type );
            }

            public override bool IsWritableType( IExtNullabilityInfo type )
            {
                return !type.IsNullable && FamilyInfo.Interfaces.Any( i => i.PocoInterface == type.Type );
            }

            public override bool IsReadableType( IExtNullabilityInfo type )
            {
                var t = type.Type;
                return t == typeof( object )
                       || t == typeof( IPoco )
                       || (FamilyInfo.IsClosedPoco && t == typeof( IClosedPoco ))
                       || FamilyInfo.Interfaces.Any( i => i.PocoInterface == t )
                       || FamilyInfo.OtherInterfaces.Any( i => i == t );
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



