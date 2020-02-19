using System;

namespace CK.Setup
{

    /// <summary>
    /// Holds the head of a Chain of Responsibility composed of <see cref="StObjConfigurationLayer"/>.
    /// </summary>
    public sealed class StObjEngineConfigurator
    {
        StObjConfigurationLayer _first;

        /// <summary>
        /// Adds a configurator as the first configurator.
        /// </summary>
        /// <param name="configurator">Configurator to add. Must have a null <see cref="StObjConfigurationLayer.Host"/>.</param>
        public void AddLayer( StObjConfigurationLayer configurator )
        {
            if( configurator == null ) throw new ArgumentNullException( nameof( configurator ) );
            if( configurator.Host != null ) throw new ArgumentException( $"{nameof(StObjConfigurationLayer)} is already hosted.", nameof( configurator ) );
            configurator.Next = _first;
            _first = configurator;
            configurator.Host = this;
        }

        /// <summary>
        /// Removes a previously added configurator.
        /// </summary>
        /// <param name="configurator">Configurator to remove.</param>
        public void RemoveConfigurator( StObjConfigurationLayer configurator )
        {
            if( configurator == null ) throw new ArgumentNullException( nameof( configurator ) );
            if( configurator.Host != this ) throw new ArgumentException( $"{nameof(StObjConfigurationLayer)} is not hosted by this {nameof(StObjEngineConfigurator)}.", nameof( configurator ) );
            StObjConfigurationLayer prev = null;
            StObjConfigurationLayer x = _first;
            while( x != configurator ) x = x.Next;
            if( prev != null ) prev.Next = configurator.Next;
            else _first = configurator.Next;
            configurator.Host = null;
        }

        /// <summary>
        /// Gets the first <see cref="StObjConfigurationLayer"/>.
        /// Null if no layer has been added.
        /// </summary>
        public StObjConfigurationLayer FirstLayer => _first;
    }

}
