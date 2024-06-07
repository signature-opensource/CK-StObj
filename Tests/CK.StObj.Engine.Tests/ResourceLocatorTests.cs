#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.Setupable.Engine.Tests\Resources.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Linq;
using NUnit.Framework;
using CK.Core;
using CK.Setup;

namespace CK.StObj.Engine.Tests.SubNamespace
{
    class OneType
    {
    }
}

namespace Another.Namespace
{
    class OneTypeInAnotherNamespace
    {
    }
}

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class ResourceLocatorTests
    {
        [Test]
        public void ResourceLocationReliesOnDefaultNamespaceOfTheAssembly()
        {
            {
                ResourceLocator r = new ResourceLocator( typeof( ResourceLocatorTests ), "Res" );
                Assert.That( r.GetString( "TextFile.txt", true ), Is.EqualTo( "A content." ) );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( ResourceLocatorTests ), "~CK.StObj.Engine.Tests.Res" );
                Assert.That( r.GetString( "TextFile.txt", true ), Is.EqualTo( "A content." ) );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( CK.StObj.Engine.Tests.SubNamespace.OneType ), "Sub.Res" );
                Assert.That( r.GetString( "TextFile.txt", true ), Is.EqualTo( "A content 2." ) );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( CK.StObj.Engine.Tests.SubNamespace.OneType ), "~CK.StObj.Engine.Tests.SubNamespace.Sub.Res" );
                Assert.That( r.GetString( "TextFile.txt", true ), Is.EqualTo( "A content 2." ) );
            }
        }

        [Test]
        public void ResourceLocationInAnotherNamespace()
        {
            {
                ResourceLocator r = new ResourceLocator( typeof( Another.Namespace.OneTypeInAnotherNamespace ), null );
                Assert.Throws<CKException>( () => r.GetString( "TextFile.txt", true ), "No way to get the resource without ~ root trick." );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( Another.Namespace.OneTypeInAnotherNamespace ), "~CK.StObj.Engine.Tests.Another.Namespace" );
                Assert.That( r.GetString( "TextFile.txt", true ), Is.EqualTo( "A content 3." ), "The compiler injects the Default namespace of the Assembly." );
            }
        }

        [Test]
        public void GetMultipleNames()
        {
            {
                ResourceLocator r = new ResourceLocator( typeof( ResourceLocatorTests ), null );
                Assert.That( r.GetNames( "" ).ToArray(), Is.EquivalentTo( new string[] { "Another.Namespace.TextFile.txt", "Res.TextFile.txt", "SubNamespace.Sub.Multi.Multi.Text1.txt", "SubNamespace.Sub.Multi.Multi.Text2.txt", "SubNamespace.Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "Res." ).ToArray(), Is.EquivalentTo( new string[] { "Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "SubNamespace." ).ToArray(), Is.EquivalentTo( new string[] { "SubNamespace.Sub.Multi.Multi.Text1.txt", "SubNamespace.Sub.Multi.Multi.Text2.txt", "SubNamespace.Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "SubNamespace.Sub." ).ToArray(), Is.EquivalentTo( new string[] { "SubNamespace.Sub.Multi.Multi.Text1.txt", "SubNamespace.Sub.Multi.Multi.Text2.txt", "SubNamespace.Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "SubNamespace.Sub.Multi." ).ToArray(), Is.EquivalentTo( new string[] { "SubNamespace.Sub.Multi.Multi.Text1.txt", "SubNamespace.Sub.Multi.Multi.Text2.txt" } ) );
                Assert.That( r.GetNames( "SubNamespace.Sub.Res." ).ToArray(), Is.EquivalentTo( new string[] { "SubNamespace.Sub.Res.TextFile.txt" } ) );

                Assert.DoesNotThrow( () => r.GetNames( "Res." ).Select( name => r.GetString( name, true ) ) );
                Assert.DoesNotThrow( () => r.GetNames( "SubNamespace." ).Select( name => r.GetString( name, true ) ) );
                Assert.DoesNotThrow( () => r.GetNames( "SubNamespace.Sub." ).Select( name => r.GetString( name, true ) ) );
                Assert.DoesNotThrow( () => r.GetNames( "SubNamespace.Sub.Res." ).Select( name => r.GetString( name, true ) ) );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( ResourceLocatorTests ), "Res" );
                Assert.That( r.GetNames( "" ).ToArray(), Is.EquivalentTo( new string[] { "TextFile.txt" } ) );
                Assert.That( r.GetNames( "T" ).ToArray(), Is.EquivalentTo( new string[] { "TextFile.txt" } ) );
                Assert.That( r.GetNames( "Tex" ).ToArray(), Is.EquivalentTo( new string[] { "TextFile.txt" } ) );
                Assert.That( r.GetNames( "TextFile." ).ToArray(), Is.EquivalentTo( new string[] { "TextFile.txt" } ) );
                Assert.That( r.GetNames( "Sub" ), Is.Empty );

                Assert.DoesNotThrow( () => r.GetNames( "" ).Select( name => r.GetString( name, true ) ) );
            }
            {
                ResourceLocator r = new ResourceLocator( typeof( ResourceLocatorTests ), "SubNamespace" );
                Assert.That( r.GetNames( "" ).ToArray(), Is.EquivalentTo( new string[] { "Sub.Multi.Multi.Text1.txt", "Sub.Multi.Multi.Text2.txt", "Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "Sub." ).ToArray(), Is.EquivalentTo( new string[] { "Sub.Multi.Multi.Text1.txt", "Sub.Multi.Multi.Text2.txt", "Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "Sub.Multi." ).ToArray(), Is.EquivalentTo( new string[] { "Sub.Multi.Multi.Text1.txt", "Sub.Multi.Multi.Text2.txt" } ) );
                Assert.That( r.GetNames( "Sub.Res." ).ToArray(), Is.EquivalentTo( new string[] { "Sub.Res.TextFile.txt" } ) );
                Assert.That( r.GetNames( "Res" ), Is.Empty );

                Assert.DoesNotThrow( () => r.GetNames( "" ).Select( name => r.GetString( name, true ) ) );
            }
        }
    }
}
