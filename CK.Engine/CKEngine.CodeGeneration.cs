using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace CK.Setup;

public static partial class CKEngine
{
    /// <summary>
    /// Calls the handlers: the <see cref="GenContext"/> slots and, between its main slot and its end, the slots of each App.
    /// After its <see cref="Ending{T}"/>, the source code of an App is signed, saved and compiled.
    /// On error, the <see cref="OnError{T}"/> of the failed context is called instead of its <see cref="Ending{T}"/>,
    /// and the error propagates to the <see cref="GenContext"/>.
    /// </summary>
    static bool RunHandlers( IActivityMonitor monitor, EngineHandlers handlers, GenContext gen, StObjEngine engine )
    {
        bool success = handlers.Call( monitor, HandlerSlot.StartingGen, gen, null )
                       && handlers.Call( monitor, HandlerSlot.Gen, gen, null )
                       && RunApps( monitor, handlers, gen, engine );
        if( success )
        {
            return handlers.Call( monitor, HandlerSlot.EndingGen, gen, null );
        }
        handlers.Call( monitor, HandlerSlot.OnErrorGen, gen, null );
        return false;
    }

    static bool RunApps( IActivityMonitor monitor, EngineHandlers handlers, GenContext gen, StObjEngine engine )
    {
        for( int i = 0; i < gen.Apps.Length; i++ )
        {
            var app = gen.Apps[i];
            using( monitor.OpenInfo( $"Generating the {app}." ) )
            {
                if( !handlers.Call( monitor, HandlerSlot.StartingApp, gen, app )
                    || !handlers.Call( monitor, HandlerSlot.App, gen, app ) )
                {
                    handlers.Call( monitor, HandlerSlot.OnErrorApp, gen, app );
                    return false;
                }
                // A failed Ending is not followed by the OnError of the App: the GenContext fails.
                if( !handlers.Call( monitor, HandlerSlot.EndingApp, gen, app ) )
                {
                    return false;
                }
                if( app.IsUnifiedPure )
                {
                    monitor.Info( "Purely unified group: its source code is not used." );
                    continue;
                }
                if( !EmitSourceCode( monitor, app, engine.Groups[i], out var runSignature ) )
                {
                    return false;
                }
                engine.SetRunSignature( i, runSignature );
            }
        }
        return true;
    }

    /// <summary>
    /// Signs, saves (the <c>G0.cs</c>) and compiles (or parses) the source code of an App, according to
    /// the <see cref="IRunningBinPathGroup.SaveSource"/> and <see cref="IRunningBinPathGroup.CompileOption"/>.
    /// </summary>
    static bool EmitSourceCode( IActivityMonitor monitor, AppGenContext app, IRunningBinPathGroup group, out SHA1Value runSignature )
    {
        Throw.DebugAssert( !app.IsUnifiedPure && group.GeneratedSource != null && group.GeneratedAssembly != null );
        runSignature = group.RunSignature;
        try
        {
            if( !group.SaveSource
                && group.CompileOption == CompileOption.None
                && !runSignature.IsZero )
            {
                monitor.Info( "Configured GenerateSourceFile is false, CompileOption is None and SHA1 is known: nothing more to do." );
                return true;
            }
            ICodeWorkspace ws = app.Code;

            // Trick to avoid allocating the big string code more than once: the hash is first computed on the source
            // without the signature header, a part is injected at the top of the file (before anything else) and
            // the final big string is built only once.
            if( runSignature.IsZero )
            {
                using( var s = new HashStream( System.Security.Cryptography.HashAlgorithmName.SHA1 ) )
                using( var w = new StreamWriter( s ) )
                {
                    ws.WriteGlobalSource( w );
                    w.Flush();
                    runSignature = new SHA1Value( s.GetFinalResult().AsSpan() );
                }
                monitor.Info( $"Computed file signature: {runSignature}." );
            }
            else
            {
                monitor.Info( $"Using provided file signature: {runSignature}." );
            }

            // Injects the SHA1 signature at the top.
            ws.Global.BeforeNamespace.CreatePart( top: true ).Append( @"[assembly: CK.StObj.Signature( " )
                                                             .AppendSourceString( runSignature.ToString() )
                                                             .Append( " )]" );

            // The source code is available if SaveSource is true.
            string? code = null;

            // If asked to do so, we always save it, even if parsing or compilation fails
            // but only if the current G0.cs has not the same signature: this preserves any
            // manual modifications to the file so that code generation can be perfected before
            // being integrated back into the engine code.
            if( group.SaveSource
                && group.GeneratedSource.GetSignature( monitor ) != runSignature )
            {
                code = ws.GetGlobalSource();
                group.GeneratedSource.CreateOrUpdate( monitor, code );
            }
            if( group.CompileOption == CompileOption.None )
            {
                monitor.Info( "Configured CompileOption is None: nothing more to do." );
                return true;
            }
            code ??= ws.GetGlobalSource();

            var targetPath = group.CompileOption == CompileOption.Compile
                                ? group.GeneratedAssembly.Path
                                : default;
            using( monitor.OpenInfo( group.CompileOption == CompileOption.Compile
                                        ? $"Compiling source code (using C# v12.0 language version) and saving to '{targetPath}'."
                                        : "Only parsing source code, using C# v12.0 language version (skipping compilation)." ) )
            {
                var result = CodeGenerator.Generate( code,
                                                     targetPath.IsEmptyPath ? null : targetPath,
                                                     ws.AssemblyReferences,
                                                     new CSharpParseOptions( LanguageVersion.CSharp12 ) );

                result.LogResult( monitor );
                if( result.Success )
                {
                    if( group.CompileOption == CompileOption.Compile )
                    {
                        group.GeneratedAssembly.CreateOrUpdateSignatureFile( runSignature );
                    }
                }
                else
                {
                    monitor.Debug( code );
                    monitor.CloseGroup( "Failed" );
                }
                return result.Success;
            }
        }
        catch( Exception ex )
        {
            monitor.Error( "While generating final source code.", ex );
            return false;
        }
    }
}
