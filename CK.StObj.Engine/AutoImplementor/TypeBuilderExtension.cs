#region LGPL License
/*----------------------------------------------------------------------------
* This file (CK.Reflection\TypeBuilderExtension.cs) is part of CiviKey. 
*  
* CiviKey is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Lesser General Public License as published 
* by the Free Software Foundation, either version 3 of the License, or 
* (at your option) any later version. 
*  
* CiviKey is distributed in the hope that it will be useful, 
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
* GNU Lesser General Public License for more details. 
* You should have received a copy of the GNU Lesser General Public License 
* along with CiviKey.  If not, see <http://www.gnu.org/licenses/>. 
*  
* Copyright © 2007-2015, 
*     Invenietis <http://www.invenietis.com>,
*     In’Tech INFO <http://www.intechinfo.fr>,
* All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CK.Setup;

/// <summary>
/// Provides extension methods on <see cref="TypeBuilder"/> class.
/// </summary>
public static class TypeBuilderExtension
{
    /// <summary>
    /// Creates constructors that relay calls to public and protected constructors in the base class.
    /// </summary> 
    /// <param name="this">This <see cref="TypeBuilder"/>.</param>
    /// <param name="baseConstructorfilter">
    /// Optional predicate used to filter constructors that must be implemented and set its <see cref="MethodAttributes"/>. 
    /// When this function returns null, the constructor is not implemented, otherwise it can return the baseConstructor's Attribute.
    /// When null, all public and protected constructors are replicated with the same access (public or protected).
    /// </param>
    /// <param name="constructorAttributesFilter">
    /// Optional predicate used to filter constructors' attributes. 
    /// When null, all attributes are redefined.
    /// </param>
    /// <param name="parameterAttributesFilter">
    /// Optional predicate used to filter constructors' arguments' attributes. 
    /// When null, all parameters are redefined.
    /// </param>
    public static void DefinePassThroughConstructors( this TypeBuilder @this,
                                                      Func<ConstructorInfo, MethodAttributes?>? baseConstructorfilter = null,
                                                      Func<ConstructorInfo, CustomAttributeData, bool>? constructorAttributesFilter = null,
                                                      Func<ParameterInfo, CustomAttributeData, bool>? parameterAttributesFilter = null )
    {
        Throw.CheckArgument( @this != null && @this.BaseType != null );
        Type baseType = @this.BaseType;
        foreach( var baseCtor in baseType.GetConstructors( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic ) )
        {
            if( baseCtor.IsPrivate ) continue;
            MethodAttributes? newCtorAttr = baseCtor.Attributes;
            if( baseConstructorfilter != null && !(newCtorAttr = baseConstructorfilter( baseCtor )).HasValue ) continue;
            var parameters = baseCtor.GetParameters();
            if( parameters.Length == 0 ) @this.DefineDefaultConstructor( newCtorAttr.Value );
            else
            {
                Type[] parameterTypes = CreateParametersType( parameters );
                // REVIEW: Type.EmptyTypes replaces GetRequiredCustomModifiers and GetOptionalCustomModifiers 
                Type[][] requiredCustomModifiers = parameters.Select( p => Type.EmptyTypes ).ToArray();
                Type[][] optionalCustomModifiers = parameters.Select( p => Type.EmptyTypes ).ToArray();

                var ctor = @this.DefineConstructor( newCtorAttr.Value, baseCtor.CallingConvention, parameterTypes, requiredCustomModifiers, optionalCustomModifiers );
                for( var i = 0; i < parameters.Length; ++i )
                {
                    ParameterInfo parameter = parameters[i];
                    ParameterBuilder pBuilder = ctor.DefineParameter( i + 1, parameter.Attributes, parameter.Name );
                    if( (parameter.Attributes & ParameterAttributes.HasDefault) != 0 )
                    {
                        pBuilder.SetConstant( parameter.DefaultValue );
                    }
                    if( parameterAttributesFilter != null )
                    {
                        GenerateCustomAttributeBuilder( parameter.CustomAttributes, pBuilder.SetCustomAttribute, a => parameterAttributesFilter( parameter, a ) );
                    }
                    else
                    {
                        GenerateCustomAttributeBuilder( parameter.CustomAttributes, pBuilder.SetCustomAttribute );
                    }
                }
                if( constructorAttributesFilter != null )
                {
                    GenerateCustomAttributeBuilder( baseCtor.CustomAttributes, ctor.SetCustomAttribute, a => constructorAttributesFilter( baseCtor, a ) );
                }
                else
                {
                    GenerateCustomAttributeBuilder( baseCtor.CustomAttributes, ctor.SetCustomAttribute );
                }
                var g = ctor.GetILGenerator();
                g.RepushActualParameters( true, parameters.Length + 1 );
                g.Emit( OpCodes.Call, baseCtor );
                g.Emit( OpCodes.Ret );
            }
        }
    }

    /// <summary>
    /// Creates an array of type of a method parameters.
    /// </summary>
    /// <param name="parametersInfo">Parameters from which type must be extracted.</param>
    /// <returns>Parameters' types.</returns>
    /// <remarks>
    /// Implementation is faster (and more simple?) than using a linq select...
    /// </remarks>
    public static Type[] CreateParametersType( ParameterInfo[] parametersInfo )
    {
        if( parametersInfo == null ) throw new ArgumentNullException( "parametersInfo" );
        int len = parametersInfo.Length;
        Type[] parameters = new Type[len];
        while( --len >= 0 ) parameters[len] = parametersInfo[len].ParameterType;
        return parameters;
    }

    /// <summary>
    /// Generates <see cref="CustomAttributeBuilder"/> from an enumerable of <see cref="CustomAttributeData"/>.
    /// </summary>
    /// <param name="customAttributes">Existing custom attribute data (can be obtained through <see cref="MemberInfo.GetCustomAttributesData"/>).</param>
    /// <param name="collector">Action that receives builders that reproduce the original custom attributes.</param>
    /// <param name="filter">Optional filter for attributes. When null, all attributes are collected.</param>
    public static void GenerateCustomAttributeBuilder( IEnumerable<CustomAttributeData> customAttributes,
                                                       Action<CustomAttributeBuilder> collector, Func<CustomAttributeData, bool>? filter = null )
    {
        Throw.CheckNotNullArgument( customAttributes );
        Throw.CheckNotNullArgument( collector );
        foreach( var attr in customAttributes )
        {
            if( filter != null && !filter( attr ) ) continue;
            var ctorArgs = attr.ConstructorArguments.Select( a => a.Value ).ToArray();

            var arProperties = attr.NamedArguments
                .Select( a => new { PropertyInfo = attr.AttributeType.GetProperty( a.MemberName ), TypedValue = a.TypedValue } )
                .Where( a => a.PropertyInfo != null );

            var arFields = attr.NamedArguments
              .Select( a => new { FieldInfo = attr.AttributeType.GetField( a.MemberName ), TypedValue = a.TypedValue } )
              .Where( a => a.FieldInfo != null );

            var namedPropertyInfos = arProperties.Select( a => a.PropertyInfo! ).ToArray();
            var namedPropertyValues = arProperties.Select( a => a.TypedValue.Value ).ToArray();
            var namedFieldInfos = arFields.Select( a => a.FieldInfo! ).ToArray();
            var namedFieldValues = arFields.Select( a => a.TypedValue.Value ).ToArray();

            var matchedConstructor = attr.AttributeType.GetConstructors().SingleOrDefault( c => ConstructorSignatureMatch( c, attr.ConstructorArguments ) );
            if( matchedConstructor == null ) throw new ArgumentException( String.Format( "No valid constructor found for attribute {0}.", attr.AttributeType.Name ) );

            collector( new CustomAttributeBuilder( matchedConstructor, ctorArgs, namedPropertyInfos, namedPropertyValues, namedFieldInfos, namedFieldValues ) );
        }
    }

    static bool ConstructorSignatureMatch( ConstructorInfo c, IList<CustomAttributeTypedArgument> ctorArgs )
    {
        var ctorParameters = c.GetParameters();
        if( ctorParameters.Length != ctorArgs.Count ) return false;

        for( int i = 0; i < ctorParameters.Length; ++i )
        {
            if( ctorParameters[i].ParameterType != ctorArgs[i].ArgumentType ) return false;
        }
        return true;
    }


    /// <summary>
    /// Converts a <see cref="CustomAttributeData"/> to its <see cref="CustomAttributeBuilder"/> so it can be applied
    /// to another generated member.
    /// </summary>
    /// <param name="data">the attribute data.</param>
    /// <returns>The attribute builder to apply.</returns>
    public static CustomAttributeBuilder CreateAttributeBuilder( CustomAttributeData data )
    {
        Throw.CheckNotNullArgument( data );

        var props = new List<PropertyInfo>();
        var propVals = new List<object>();
        var fields = new List<FieldInfo>();
        var fieldVals = new List<object>();
        foreach( var n in data.NamedArguments )
        {
            if( n.MemberInfo is FieldInfo f )
            {
                fields.Add( f );
                fieldVals.Add( n.TypedValue.Value! );
            }
            else if( n.MemberInfo is PropertyInfo p )
            {
                props.Add( p );
                propVals.Add( n.TypedValue.Value! );
            }
        }
        return new CustomAttributeBuilder( data.Constructor,
                                           data.ConstructorArguments.Select( a => a.Value ).ToArray(),
                                           props.ToArray(),
                                           propVals.ToArray(),
                                           fields.ToArray(),
                                           fieldVals.ToArray() );
    }


    /// <summary>
    /// Implements a method as a no operation method. Method can be virtual, abstract or not.
    /// </summary>
    /// <param name="tB">The <see cref="TypeBuilder"/> for the new type.</param>
    /// <param name="method">The method to implement.</param>
    /// <param name="isVirtual">Defaults to false: the method is sealed. True to keep the method virtual. </param>
    /// <returns>The <see cref="MethodBuilder"/> to enable, for instance, creation of custom attributes on the method.</returns>
    public static MethodBuilder ImplementEmptyStubMethod( this TypeBuilder tB, MethodInfo method, bool isVirtual = false )
    {
        if( tB == null ) throw new ArgumentNullException( "tB" );
        if( method == null ) throw new ArgumentNullException( "method" );

        ParameterInfo[] parameters = method.GetParameters();
        Type returnType = method.ReturnType;

        MethodAttributes mA = method.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
        if( isVirtual ) mA |= MethodAttributes.Virtual;
        MethodBuilder mB = tB.DefineMethod( method.Name, mA );
        if( method.ContainsGenericParameters )
        {
            int i = 0;

            Type[] genericArguments = method.GetGenericArguments();
            string[] names = genericArguments.Select( t => String.Format( "T{0}", i++ ) ).ToArray();

            var genericParameters = mB.DefineGenericParameters( names );
            for( i = 0; i < names.Length; ++i )
            {
                var genericTypeArgument = genericArguments[i].GetTypeInfo();
                GenericTypeParameterBuilder genericTypeBuilder = genericParameters[i];


                genericTypeBuilder.SetGenericParameterAttributes( genericTypeArgument.GenericParameterAttributes );
                genericTypeBuilder.SetInterfaceConstraints( genericTypeArgument.GetGenericParameterConstraints() );
            }
        }
        mB.SetReturnType( returnType );
        mB.SetParameters( CreateParametersType( parameters ) );
        EmitEmptyImplementation( mB, returnType, parameters );

        // This "forces" the mapping that is done autmatically as long as the the signatures match.
        // Enabling this on the 'in' parameter raises an explicit:
        //    System.TypeLoadException : Signature of the body and declaration in a method implementation do not match.  Type: 'L2'.  Assembly: 'TypeImplementorModule, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.
        // ...that clearly states that something's wrong in the signatures.
        //
        // This enforces the mapping: this avoids the bug of a virtual that would NOT be overridden. So we let it.
        //
        tB.DefineMethodOverride( mB, method );
        return mB;
    }

    private static void EmitEmptyImplementation( MethodBuilder vM, Type returnType, ParameterInfo[] parameters )
    {
        ILGenerator gVM = vM.GetILGenerator();
        for( int i = 0; i < parameters.Length; ++i )
        {
            // DefineParameter use 0 for the return parameter.
            ParameterInfo param = parameters[i];
            ParameterBuilder pB = vM.DefineParameter( i + 1, param.Attributes, param.Name );
            Debug.Assert( pB.IsIn == param.IsIn && pB.IsOptional == param.IsOptional && pB.IsOut == param.IsOut && pB.Name == param.Name && pB.Position == param.Position + 1 );
            foreach( var pAttr in param.CustomAttributes )
            {
                if( pAttr.AttributeType != typeof( System.Runtime.InteropServices.InAttribute ) )
                {
                    pB.SetCustomAttribute( CreateAttributeBuilder( pAttr ) );
                }
            }
            if( param.IsOut )
            {
                Debug.Assert( param.ParameterType.IsByRef, "'Out' is just an attribute on 'by ref' parameters (unfortunate for covariance support)." );
                gVM.StoreDefaultValueForOutParameter( param );
            }
        }
        if( returnType != typeof( void ) )
        {
            if( returnType.GetTypeInfo().IsValueType )
            {
                LocalBuilder retValue = gVM.DeclareLocal( returnType );
                gVM.Emit( OpCodes.Ldloca_S, retValue.LocalIndex );
                gVM.Emit( OpCodes.Initobj, returnType );
                gVM.LdLoc( retValue );
            }
            else
            {
                gVM.Emit( OpCodes.Ldnull );
            }
        }
        gVM.Emit( OpCodes.Ret );
    }

    /// <summary>
    /// Implement a property with getter/setter that relies on a private backup field.
    /// This is useful only to provide a temporary implementation of abstract properties that would be generated in a second time (this does not 
    /// provide more than auto implemented properties available in C# 3.0 and later.
    /// </summary>
    /// <param name="tB">The <see cref="TypeBuilder"/> for the new type.</param>
    /// <param name="property">The property to implement.</param>
    /// <param name="isVirtual">Defaults to false: the method is sealed. True to keep the method virtual. </param>
    /// <param name="alwaysImplementSetter">When true a setter is implemented even if the <paramref name="property"/> has no setter.</param>
    /// <returns>The <see cref="PropertyBuilder"/> to enable, for instance, creation of custom attributes on the property.</returns>
    public static PropertyBuilder ImplementStubProperty( this TypeBuilder tB, PropertyInfo property, bool isVirtual = false, bool alwaysImplementSetter = false )
    {
        Throw.CheckNotNullArgument( tB );
        Throw.CheckNotNullArgument( property );

        FieldBuilder backField = tB.DefineField( "_" + property.Name + property.GetHashCode(), property.PropertyType, FieldAttributes.Private );

        MethodInfo? getMethod = property.GetMethod;
        MethodBuilder? mGet = null;
        if( getMethod != null )
        {
            MethodAttributes mA = getMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
            if( isVirtual ) mA |= MethodAttributes.Virtual;
            mGet = tB.DefineMethod( getMethod.Name, mA, property.PropertyType, Type.EmptyTypes );
            ILGenerator g = mGet.GetILGenerator();
            g.LdArg( 0 );
            g.Emit( OpCodes.Ldfld, backField );
            g.Emit( OpCodes.Ret );
        }
        MethodInfo? setMethod = property.SetMethod;
        if( setMethod == null && alwaysImplementSetter )
        {
            // We only use Attributes and Name from the method info.
            setMethod = getMethod;
        }
        MethodBuilder? mSet = null;
        if( setMethod != null )
        {
            MethodAttributes mA = setMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
            if( isVirtual ) mA |= MethodAttributes.Virtual;
            mSet = tB.DefineMethod( setMethod.Name, mA, typeof( void ), new[] { property.PropertyType } );
            ILGenerator g = mSet.GetILGenerator();
            g.LdArg( 0 );
            g.LdArg( 1 );
            g.Emit( OpCodes.Stfld, backField );
            g.Emit( OpCodes.Ret );
        }

        PropertyBuilder p = tB.DefineProperty( property.Name, property.Attributes, property.PropertyType, Type.EmptyTypes );
        if( mGet != null ) p.SetGetMethod( mGet );
        if( mSet != null ) p.SetSetMethod( mSet );
        return p;
    }

}
