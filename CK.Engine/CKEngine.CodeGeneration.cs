using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace CK.Setup;

public static partial class CKEngine
{
    /// <summary>
    /// The code generation phase: for each App (the unified one first), the StObj engine generates its code, this engine
    /// completes it, then signs, saves and compiles it.
    /// </summary>
    static bool GenerateCode( IActivityMonitor monitor, StObjEngine engine, AppGenContext[] apps )
    {
        using( monitor.OpenInfo( "Code Generation." ) )
        {
            for( int i = 0; i < apps.Length; i++ )
            {
                var app = apps[i];
                if( !engine.GenerateCode( i ) || !GenerateAppCode( monitor, app ) )
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
    /// The code generation of this engine: the participants receive the <see cref="AppGenContext"/>. They log their
    /// errors.
    /// </summary>
    static bool GenerateAppCode( IActivityMonitor monitor, AppGenContext app )
    {
        using( monitor.OpenInfo( $"Generating the code of the {app} (CK.Engine)." ) )
        using( monitor.CollectEntries( out var entries ) )
        {
            try
            {
                CKomposableMapGenerator.Generate( monitor, app );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While generating the code of the {app}.", ex );
            }
            return entries.Count == 0;
        }
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
