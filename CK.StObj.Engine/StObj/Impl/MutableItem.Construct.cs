using System;
using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;
using System.Reflection;

namespace CK.Setup
{
    partial class MutableItem
    {
        void AddPreConstructProperty( PropertyInfo p, object o, BuildValueCollector valueCollector )
        {
            if( _preConstruct == null ) _preConstruct = new List<PropertySetter>();
            _preConstruct.Add( new PropertySetter( p, o, valueCollector ) );
        }

        void AddPostBuildProperty( PropertyInfo p, object o, BuildValueCollector valueCollector )
        {
            Debug.Assert( Specialization == null, "Called on leaf only." );
            if( _leafData.PostBuildProperties == null ) _leafData.PostBuildProperties = new List<PropertySetter>();
            _leafData.PostBuildProperties.Add( new PropertySetter( p, o, valueCollector ) );
        }

        internal void CallConstruct( IActivityMonitor monitor, BuildValueCollector valueCollector, IStObjValueResolver valueResolver )
        {
            Debug.Assert( _constructParameterEx != null, "Always allocated." );
            if( _preConstruct != null )
            {
                foreach( var p in _preConstruct )
                {
                    SetPropertyValue( monitor, p );
                }
            }
            if( _constructParametersAbove != null )
            {
                foreach( var above in _constructParametersAbove )
                {
                    DoCallStObjConstruct( monitor, valueCollector, valueResolver, above.Item1, above.Item2 );
                }
            }
            if( Type.StObjConstruct != null )
            {
                Debug.Assert( _constructParameterEx != null );
                DoCallStObjConstruct( monitor, valueCollector, valueResolver, Type.StObjConstruct, _constructParameterEx );
            }
        }

        private void DoCallStObjConstruct( IActivityMonitor monitor, BuildValueCollector valueCollector, IStObjValueResolver valueResolver, MethodInfo stobjConstruct, IReadOnlyList<MutableParameter> mutableParameters )
        {
            object[] parameters = new object[mutableParameters.Count];
            int i = 0;
            foreach( MutableParameter t in mutableParameters )
            {
                // We inject our "setup monitor" for IActivityMonitor parameter type.
                if( t.IsSetupLogger )
                {
                    t.SetParameterValue( monitor );
                    t.BuilderValueIndex = Int32.MaxValue;
                }
                else
                {
                    MutableItem resolved = null;
                    if( t.Value == System.Type.Missing )
                    {
                        // Parameter reference have already been resolved as dependencies for graph construction since 
                        // no Value has been explicitly set for the parameter.
                        resolved = t.CachedResolvedStObj;
                        if( resolved != null )
                        {
                            Debug.Assert( resolved.InitialObject != System.Type.Missing );
                            t.SetParameterValue( resolved.InitialObject );
                        }
                    }
                    if( valueResolver != null ) valueResolver.ResolveParameterValue( monitor, t );
                    if( t.Value == System.Type.Missing && !t.IsRealParameterOptional )
                    {
                        if( !t.IsOptional )
                        {
                            // By throwing an exception here, we stop the process and avoid the construction 
                            // of an invalid object graph...
                            // This behavior (FailFastOnFailureToResolve) may be an option once. For the moment: log the error.
                            monitor.Fatal( $"{t}: Unable to resolve non optional. Attempting to use a default value to continue the setup process in order to detect other errors." );
                        }
                        t.SetParameterValue( t.Type.IsValueType ? Activator.CreateInstance( t.Type ) : null );
                    }
                    if( resolved != null && t.Value == resolved.InitialObject )
                    {
                        t.BuilderValueIndex = -(resolved.IndexOrdered + 1);
                    }
                    else
                    {
                        t.BuilderValueIndex = valueCollector.RegisterValue( t.Value );
                    }
                }
                parameters[i++] = t.Value;
            }
            stobjConstruct.Invoke( _leafData.StructuredObject, parameters );
        }

        internal void SetPostBuildProperties( IActivityMonitor monitor )
        {
            Debug.Assert( Specialization == null, "Called on leaves only." );
            if( _leafData.PostBuildProperties != null )
            {
                foreach( var p in _leafData.PostBuildProperties )
                {
                    SetPropertyValue( monitor, p );
                }
            }
        }

        public readonly struct PropertySetter
        {
            public readonly PropertyInfo Property;
            public readonly object Value;
            internal readonly int IndexValue;

            internal PropertySetter( PropertyInfo p, object o, BuildValueCollector valueCollector )
            {
                Property = p;
                Value = o;
                if( o is MutableItem ) IndexValue = -1;
                else
                {
                    IndexValue = valueCollector.RegisterValue( o );
                }
            }
        }

        void SetPropertyValue( IActivityMonitor monitor, PropertySetter p )
        {
            object o = p.Value;
            MutableItem m = o as MutableItem;
            if( m != null ) o = m.InitialObject;
            DoSetPropertyValue( monitor, p.Property, o );
        }

        void DoSetPropertyValue( IActivityMonitor monitor, PropertyInfo p, object o )
        {
            try
            {
                p.SetValue( _leafData.StructuredObject, o, null );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While setting '{p.DeclaringType.FullName}.{p.Name}'.", ex );
            }
        }
    }
}
