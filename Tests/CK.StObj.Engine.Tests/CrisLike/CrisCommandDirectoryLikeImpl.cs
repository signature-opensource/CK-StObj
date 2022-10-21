using CK.CodeGen;
using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike
{
    /// <summary>
    /// Code generator of the <see cref="CrisCommandDirectoryLikeImpl"/> service.
    /// </summary>
    public partial class CrisCommandDirectoryLikeImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            // IRL we may need some services (here we don't handle results) but we keep the relay that allows services
            // to be available.
            return new CSCodeGenerationResult( nameof( DoImplement ) );
        }

        CSCodeGenerationResult DoImplement( IActivityMonitor monitor,
                                            Type classType,
                                            ICSCodeGenerationContext c,
                                            ITypeScope scope,
                                            IPocoDirectory poco
                                            // This is where other required services can come...
                                            //  Setup.Json.JsonSerializationCodeGen? json = null
                                            )
        {
            if( classType != typeof( CrisCommandDirectoryLike ) ) throw new InvalidOperationException( "Applies only to the CrisCommandDirectoryLike class." );

            // In real Cris, there is a CommandRegistry that registers the commands/handlers/service etc. into an intermediate Entry descriptor
            // with the final (most specific) TResult.
            // Here we shortcut the process and work with the basic IPocoRootInfo:
            if( !poco.OtherInterfaces.TryGetValue( typeof( ICommand ), out IReadOnlyList<IPocoFamilyInfo>? commandPocos ) )
            {
                commandPocos = Array.Empty<IPocoFamilyInfo>();
            }

            CodeWriterExtensions.Append( scope, "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            scope.Append( "static IReadOnlyList<ICommandModel> CreateCommands()" ).NewLine()
                 .OpenBlock()
                 .Append( "var list = new ICommandModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            int idx = 0;
            foreach( var e in commandPocos )
            {
                var f = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.PocoFactoryClass );
                f.Definition.BaseTypes.Add( new ExtendedTypeName( "ICommandModel" ) );
                f.Append( "public Type CommandType => PocoClassType;" ).NewLine()
                 .Append( "public int CommandIdx => " ).Append( idx++ ).Append( ";" ).NewLine()
                 .Append( "public string CommandName => Name;" ).NewLine()
                 .Append( "ICommand ICommandModel.Create() => (ICommand)Create();" ).NewLine();

                // The CommandModel is the _factory field.
                var p = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.PocoClass );
                p.Append( "public ICommandModel CommandModel => _factory;" ).NewLine();

                scope.Append( p.FullName ).Append( "._factory," ).NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .Append( "return list;" )
                 .CloseBlock();

            return CSCodeGenerationResult.Success;
        }

    }
}
