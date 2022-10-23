using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
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

            sealed class Null : NullReferenceType, IPrimaryPocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new PrimaryPocoType NonNullable => Unsafe.As<PrimaryPocoType>( base.NonNullable );

                public IPocoFamilyInfo FamilyInfo => NonNullable.FamilyInfo;

                public IPrimaryPocoType PrimaryInterface => this;

                IConcretePocoType IConcretePocoType.Nullable => this;
                IConcretePocoType IConcretePocoType.NonNullable => NonNullable;

                public IReadOnlyList<IPrimaryPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                public IEnumerable<IConcretePocoType> AllowedTypes => NonNullable.PrimaryInterface.Nullable.AllowedTypes;

                IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.Nullable => this;
                IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.NonNullable => NonNullable;

                public string CSharpBodyConstructorSourceCode => NonNullable.CSharpBodyConstructorSourceCode;

                IPrimaryPocoType IPrimaryPocoType.Nullable => this;

                IPrimaryPocoType IPrimaryPocoType.NonNullable => NonNullable;
            }

            readonly IPocoFieldDefaultValue _def;
            [AllowNull]
            PrimaryPocoField[] _fields;
            [AllowNull]
            string _ctorCode;

            public PrimaryPocoType( PocoTypeSystem s,
                                    IPocoFamilyInfo family,
                                    Type primaryInterface )
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.IPoco, t => new Null( t ) )
            {
                FamilyInfo = family;
                _def = new FieldDefaultValue( Activator.CreateInstance( family.PocoClass )!, $"new {CSharpName}()" );
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new IPrimaryPocoType Nullable => Unsafe.As<IPrimaryPocoType>( base.Nullable );

            public IPocoFamilyInfo FamilyInfo { get; }

            public IPrimaryPocoType PrimaryInterface => this;

            IConcretePocoType IConcretePocoType.Nullable => Nullable;

            IConcretePocoType IConcretePocoType.NonNullable => this;

            public string CSharpBodyConstructorSourceCode => _ctorCode;

            public IReadOnlyList<IPrimaryPocoField> Fields => _fields;

            internal bool SetFields( IActivityMonitor monitor,
                                     StringCodeWriter sharedWriter,
                                     PrimaryPocoField[] fields )
            {
                _fields = fields;
                var d = CompositeHelper.CreateDefaultValueInfo( monitor, sharedWriter, this );
                if( d.IsDisallowed )
                {
                    monitor.Error( $"Unable to create '{CSharpName}' constructor code. See previous errors." );
                    return false;
                }
                _ctorCode = d.RequiresInit ? d.DefaultValue.ValueCSharpSource : String.Empty;
                return true;
            }

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            public override bool IsWritableType( Type type ) => FamilyInfo.Interfaces.Any( i => i.PocoInterface == type );

            public override bool IsReadableType( Type type ) => IsWritableType( type ) || FamilyInfo.OtherInterfaces.Any( i => i == type );

            [AllowNull]
            public IEnumerable<IConcretePocoType> AllowedTypes { get; internal set; }

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.Nullable => Nullable;

            IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.NonNullable => this;

            IPrimaryPocoType IPrimaryPocoType.Nullable => Nullable;

            IPrimaryPocoType IPrimaryPocoType.NonNullable => this;
        }
    }

}



