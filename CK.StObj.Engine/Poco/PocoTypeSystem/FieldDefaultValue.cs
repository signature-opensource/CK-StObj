using CK.CodeGen;
using CK.Core;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

sealed class FieldDefaultValue : IPocoFieldDefaultValue
{
    public static readonly FieldDefaultValue StringDefault = new FieldDefaultValue( String.Empty, "\"\"" );
    public static readonly FieldDefaultValue DateTimeDefault = new FieldDefaultValue( Util.UtcMinValue, "CK.Core.Util.UtcMinValue" );
    // Applies to NormalizedCultureInfo and ExtendedCultureInfo.
    public static readonly FieldDefaultValue CultureDefault = new FieldDefaultValue( NormalizedCultureInfo.CodeDefault, "CK.Core.NormalizedCultureInfo.CodeDefault" );
    public static readonly FieldDefaultValue MCStringDefault = new FieldDefaultValue( MCString.Empty, "CK.Core.MCString.Empty" );
    public static readonly FieldDefaultValue CodeStringDefault = new FieldDefaultValue( CodeString.Empty, "CK.Core.CodeString.Empty" );

    public FieldDefaultValue( object? simpleValue, string source )
    {
        SimpleValue = simpleValue;
        ValueCSharpSource = source;
    }

    public FieldDefaultValue( string source )
    {
        ValueCSharpSource = source;
    }

    public FieldDefaultValue( object simpleValue, PocoTypeSystemBuilder.IStringBuilderPool sbPool )
        : this( WriteSourceValue( simpleValue, sbPool ) )
    {
        SimpleValue = simpleValue;
    }

    static string WriteSourceValue( object value, PocoTypeSystemBuilder.IStringBuilderPool sbPool )
    {
        var w = new StringCodeWriter( sbPool.Get() );
        var source = w.Append( value ).ToString();
        sbPool.GetStringAndReturn( w.StringBuilder );
        return source;
    }

    public static FieldDefaultValue? CreateFromParameter( IActivityMonitor monitor,
                                                          PocoTypeSystemBuilder.IStringBuilderPool sbPool,
                                                          ParameterInfo definer )
    {
        if( !definer.HasDefaultValue || definer.DefaultValue == null ) return null;
        return new FieldDefaultValue( definer.DefaultValue, sbPool );
    }

    public static FieldDefaultValue? CreateFromAttribute( IActivityMonitor monitor,
                                                          PocoTypeSystemBuilder.IStringBuilderPool sbPool,
                                                          IExtMemberInfo definer )
    {
        // Use the conversion from the constructor for the value.
        var a = definer.GetCustomAttributes<DefaultValueAttribute>().FirstOrDefault();
        if( a == null ) return null;
        var value = a.Value;
        if( value == null ) return null;
        if( ReferenceEquals( value, String.Empty ) ) return StringDefault;
        return new FieldDefaultValue( value, WriteSourceValue( value, sbPool ) );
    }

    public static FieldDefaultValue? CreateFromDefaultValue( IActivityMonitor monitor,
                                                             PocoTypeSystemBuilder.IStringBuilderPool sbPool,
                                                             Type t )
    {
        try
        {
            var value = Activator.CreateInstance( t )!;
            if( ReferenceEquals( value, String.Empty ) ) return StringDefault;
            return new FieldDefaultValue( value, WriteSourceValue( value, sbPool ) );
        }
        catch( Exception ex )
        {
            monitor.Error( $"Unable to create a FieldDefaultValue from type '{t:N}'.", ex );
            return null;
        }
    }

    public bool CheckSameOrNone( IActivityMonitor monitor, IExtMemberInfo defaultValueSource, PocoTypeSystemBuilder.IStringBuilderPool sbPool, IExtMemberInfo other )
    {
        var a = other.GetCustomAttributes<DefaultValueAttribute>().FirstOrDefault();
        if( a?.Value == null || a.Value == SimpleValue ) return true;
        var source = WriteSourceValue( a.Value, sbPool );
        if( source != ValueCSharpSource )
        {
            monitor.Error( $"Default values difference between '{defaultValueSource.DeclaringType}.{defaultValueSource.Name}' = '{ValueCSharpSource}' and '{other.DeclaringType}.{other.Name}' = '{source}'." );
            return false;
        }
        return true;
    }

    /// <inheritdoc />
    public object? SimpleValue { get; }

    /// <inheritdoc />
    public string ValueCSharpSource { get; }

    public override string ToString() => ValueCSharpSource;
}
