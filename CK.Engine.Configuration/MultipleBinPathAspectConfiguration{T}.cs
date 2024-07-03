using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Base class for specific BinPath aspect configurations.
    /// <para>
    /// This handles <see cref="OtherConfigurations"/> automatically accross all the instances added in any of them.
    /// </para>
    /// </summary>
    /// <typeparam name="TSelf">This type itself (Curiously Recurring Pattern).</typeparam>
    public abstract class MultipleBinPathAspectConfiguration<TSelf> : MultipleBinPathAspectConfiguration where TSelf : MultipleBinPathAspectConfiguration<TSelf>, new()
    {
        readonly ExposedOthers _exposedOthers;

        sealed class ExposedOthers : IReadOnlyCollection<TSelf>
        {
            readonly IReadOnlyCollection<MultipleBinPathAspectConfiguration> _o;

            public ExposedOthers( IReadOnlyCollection<MultipleBinPathAspectConfiguration> o ) => _o = o;

            public int Count => _o.Count;

            public IEnumerator<TSelf> GetEnumerator() => _o.OfType<TSelf>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _o.GetEnumerator();
        }

        /// <summary>
        /// Initializes an empty BinPath aspect configuration that supports multiple configurations.
        /// </summary>
        protected MultipleBinPathAspectConfiguration()
        {
            _exposedOthers = new ExposedOthers( base.OtherConfigurations );
        }

        /// <summary>
        /// Gets the extra configurations if any.
        /// </summary>
        public new IReadOnlyCollection<TSelf> OtherConfigurations => _exposedOthers;

        /// <summary>
        /// Gets all the configurations (this one and the <see cref="OtherConfigurations"/>).
        /// </summary>
        public new IEnumerable<TSelf> AllConfigurations => base.AllConfigurations.OfType<TSelf>();

        /// <summary>
        /// Adds another configuration.
        /// </summary>
        /// <param name="other">The configuration to add.</param>
        public void AddOtherConfiguration( TSelf other ) => DoAddOtherConfiguration( other );

        /// <summary>
        /// Removes another configuration. Nothing is done if the <paramref name="other"/> configuration
        /// doesn't belong to this <see cref="OtherConfigurations"/>.
        /// </summary>
        /// <param name="other">The configuration to remove.</param>
        public void RemoveOtherConfiguration( TSelf other ) => DoRemoveOtherConfiguration( other );

        /// <summary>
        /// Rehydrates this instance from the xml element.
        /// </summary>
        /// <param name="e">The xml element to read.</param>
        public sealed override void InitializeFrom( XElement e )
        {
            var arrays = e.Elements( EngineConfiguration.xArray );
            bool hasArray = arrays.Any();
            bool hasMultipleArray = hasArray && arrays.Skip( 1 ).Any();
            bool hasMultipleElement = e.Elements().Skip( 1 ).Any();
            if( hasMultipleArray || (hasArray && hasMultipleElement) )
            {
                Throw.InvalidDataException( $"Invalid <Array> usage: <Array> must be the single root element in:{Environment.NewLine}{e}" );
            }
            if( hasArray )
            {
                bool inOthers = false;
                foreach( var one in arrays.Elements() )
                {
                    if( one.Name != e.Name )
                    {
                        Throw.InvalidDataException( $"Invalid <Array> content: <Array> must only contain <{e.Name}> elements in:{Environment.NewLine}{e}" );
                    }
                    if( inOthers )
                    {
                        var cOne = new TSelf();
                        cOne.InitializeFrom( one );
                        DoAddOtherConfiguration( cOne );
                    }
                    else
                    {
                        if( one.Elements( EngineConfiguration.xArray ).Any() )
                        {
                            Throw.InvalidDataException( $"Invalid <Array> child in:{Environment.NewLine}{one}" );
                        }
                        InitializeOneFrom( one );
                        inOthers = true;
                    }
                }
            }
            else
            {
                InitializeOneFrom( e );
            }
        }

        /// <summary>
        /// Appends an &lt;Array&gt; with this configuration and the <see cref="OtherConfigurations"/>
        /// if there are other configurations.
        /// <para>
        /// If there is no other configurations, this configuration alone is written.
        /// </para>
        /// </summary>
        /// <param name="e">The xml element to read.</param>
        protected sealed override void WriteXml( XElement e )
        {
            if( OtherConfigurations.Count > 0 )
            {
                var a = new XElement( EngineConfiguration.xArray );
                var first = new XElement( e.Name );
                WriteOneXml( first );
                a.Add( first );
                foreach( var o in OtherConfigurations )
                {
                    var oE = new XElement( e.Name );
                    o.WriteOneXml( oE );
                    a.Add( oE );
                }
                e.Add( a );
            }
            else
            {
                WriteOneXml( e );
            }
        }

        /// <summary>
        /// Initialize this configuration from the xml element regardless of any <see cref="OtherConfigurations"/>.
        /// </summary>
        /// <param name="e"></param>
        protected abstract void InitializeOneFrom( XElement e );

        /// <summary>
        /// Writes this configuration object regardless of <see cref="OtherConfigurations"/>.
        /// </summary>
        /// <param name="e">The element to populate.</param>
        protected abstract void WriteOneXml( XElement e );
    }

}
