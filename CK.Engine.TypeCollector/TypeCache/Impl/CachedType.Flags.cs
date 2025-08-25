using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;

namespace CK.Engine.TypeCollector;

sealed partial class CachedType
{
    public bool IsTypeDefinition => _isGenericTypeDefinition;

    public bool IsGenericType => _isGenericType;

    public bool IsInterface => Type.IsInterface;

    public bool IsSuperTypeDefiner => _isSuperTypeDefiner ??= AttributesData.Any( a => a.AttributeType == typeof( CKTypeSuperDefinerAttribute ) );

    public bool IsTypeDefiner => _isTypeDefiner ??= ComputeTypeDefiner();

    public bool IsDelegate => _isDelegate;

    public bool IsClassOrInterface => (!_isDelegate && Type.IsClass) || Type.IsInterface;

    bool ComputeTypeDefiner()
    {
        return IsSuperTypeDefiner
               || AttributesData.Any( a => a.AttributeType == typeof( CKTypeDefinerAttribute ) )
               || (_baseType != null && _baseType.IsSuperTypeDefiner)
               || DirectInterfaces.Any( i => i.IsSuperTypeDefiner ); 
    }

    public bool IsHierarchicalType => _isHierarchicalType ??= ComputeHierarchyTypeInfo();

    public bool IsHierarchicalTypeRoot => IsHierarchicalType || _hierarchicalTypePath.Length == 1;

    public ImmutableArray<ICachedType> HierarchicalTypePath
    {
        get
        {
            if( _hierarchicalTypePath.IsDefault ) ComputeHierarchyTypeInfo();
            return _hierarchicalTypePath;
        }
    }

    bool ComputeHierarchyTypeInfo( List<ICachedType>? subordinates = null )
    {
        Throw.DebugAssert( _isHierarchicalType is null && _hierarchicalTypePath.IsDefault );
        if( subordinates == null ) subordinates = new List<ICachedType>() { this };
        else
        {
            int idxThis = subordinates.IndexOf( this );
            if( idxThis >= 0 )
            {
                var path = subordinates.Skip( idxThis ).Select( t => t.CSharpName ).Append( CSharpName );
                Throw.ArgumentException( "TParent", $"Invalid cycle in [HierarchicalType<T>] on '{path.Concatenate( "' <- '" )}'." );
            }
            subordinates.Add( this );
        }
        bool isRoot = false;
        foreach( var a in AttributesData )
        {
            isRoot |= a.AttributeType == typeof( HierarchicalTypeRootAttribute );
            if( a.AttributeType.IsGenericType
                && a.AttributeType.Name == "HierarchicalTypeAttribute`1"
                && a.AttributeType.GetGenericTypeDefinition() == typeof( HierarchicalTypeAttribute<> ) )
            {
                var t = a.AttributeType.GetGenericArguments()[0];
                if( t == _member )
                {
                    Throw.ArgumentException( "TParent", $"Invalid recursive [HierarchicalType<{Name}>] on '{CSharpName}'." );
                }
                var parentType = _cache.Get( t );
                if( parentType is not CachedType parent
                    || (!parentType.Type.IsValueType && !parentType.IsClassOrInterface) )
                {
                    return Throw.ArgumentException<bool>( "TParent", $"Invalid type in [HierarchicalType<{Name}>] on '{CSharpName}'. A hierachical type must be a struct or a class." );
                }
                if( !parent.ComputeHierarchyTypeInfo( subordinates ) )
                {
                    Throw.ArgumentException( "TParent", $"Invalid [HierarchicalType<{Name}>] on '{CSharpName}': type '{Name}' is not marked as a hierarchical type." );
                }
                _hierarchicalTypePath = parentType.HierarchicalTypePath.Add( this );
                _isHierarchicalType = true;
                return true;
            }
        }
        if( isRoot )
        {
            _hierarchicalTypePath = [this];
            _isHierarchicalType = true;
            return true;
        }
        return false;
    }
}
