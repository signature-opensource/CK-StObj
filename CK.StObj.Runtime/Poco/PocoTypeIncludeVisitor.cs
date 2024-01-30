using CK.Core;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Specializes <see cref="PocoTypeVisitor"/> to visit <see cref="IPocoType.ObliviousType"/>, <see cref="IAbstractPocoType.Specializations"/>,
    /// <see cref="IAbstractPocoType.GenericArguments"/> and <see cref="IEnumPocoType.UnderlyingType"/>.
    /// </summary>
    public class PocoTypeIncludeVisitor : PocoTypeVisitor
    {
        readonly bool _visitImplementationLessAbstractPoco;
        readonly bool _visitSecondaryPocoTypes;

        /// <summary>
        /// Gets whether this visitor is configured to visit the <see cref="IPrimaryPocoType.SecondaryTypes"/>.
        /// <para>
        /// A <see cref="ISecondaryPocoType"/> that is provided to <see cref="PocoTypeVisitor.VisitRoot(IPocoType, bool)"/>
        /// is always visited.
        /// </para>
        /// </summary>
        public bool VisitSecondaryPocoTypes => _visitSecondaryPocoTypes;

        /// <summary>
        /// Gets whether this visitor is configured to visit <see cref="IAbstractPocoType.Specializations"/> that
        /// have no primary pocos.
        /// <para>
        /// A <see cref="IAbstractPocoType"/> that is provided to <see cref="PocoTypeVisitor.VisitRoot(IPocoType, bool)"/>
        /// is always visited, even if its <see cref="IAbstractPocoType.PrimaryPocoTypes"/> is empty.
        /// </para>
        /// </summary>
        public bool VisitImplementationLessAbstractPoco => _visitImplementationLessAbstractPoco;

        /// <summary>
        /// Initializes a new <see cref="PocoTypeIncludeVisitor"/>.
        /// </summary>
        /// <param name="visitImplementationLessAbstractPoco">True to visit <see cref="IAbstractPocoType"/> that have no primary Pocos.</param>
        /// <param name="visitSecondaryPoco">True to visit <see cref="IPrimaryPocoType.SecondaryTypes"/>.</param>
        public PocoTypeIncludeVisitor( bool visitImplementationLessAbstractPoco = false, bool visitSecondaryPoco = false )
        {
            _visitImplementationLessAbstractPoco = visitImplementationLessAbstractPoco;
            _visitSecondaryPocoTypes = visitSecondaryPoco;
        }

        /// <summary>
        /// Overridden to visit the <see cref="IPocoType.ObliviousType"/>.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <returns>True if the type has been visited, false if it has been skipped (already visited).</returns>
        protected override bool Visit( IPocoType t )
        {
            if( !base.Visit( t ) ) return false;
            // VisitSecondaryPoco visits its PrimaryPoco that is its Oblivious: no need to visit it again.
            if( !t.IsOblivious && t.Kind != PocoTypeKind.SecondaryPoco ) Visit( t.ObliviousType );
            return true;
        }

        /// <summary>
        /// Overridden to visit the generic arguments, the primaries and the specializations.
        /// </summary>
        /// <param name="abstractPoco">The abstract poco.</param>
        protected override void VisitAbstractPoco( IAbstractPocoType abstractPoco )
        {
            foreach( var a in abstractPoco.GenericArguments )
            {
                Visit( a.Type );
            }
            foreach( var a in abstractPoco.PrimaryPocoTypes )
            {
                Visit( a );
            }
            foreach( var s in abstractPoco.Specializations )
            {
                if( _visitImplementationLessAbstractPoco || !s.PrimaryPocoTypes.Any() )
                {
                    Visit( s );
                }
            }
        }

        /// <summary>
        /// Overridden to optionally visit secondary Pocos.
        /// </summary>
        /// <param name="primary">The primary poco type.</param>
        protected override void VisitPrimaryPoco( IPrimaryPocoType primary )
        {
            base.VisitPrimaryPoco( primary );
            if( _visitSecondaryPocoTypes )
            {
                foreach( var s in primary.SecondaryTypes )
                {
                    Visit( s );
                }
            }
        }

        /// <summary>
        /// Overridden to visit the <see cref="IEnumPocoType.UnderlyingType"/>.
        /// </summary>
        /// <param name="e">The enum type.</param>
        protected override void VisitEnum( IEnumPocoType e )
        {
            Visit( e.UnderlyingType );
        }

    }

}
