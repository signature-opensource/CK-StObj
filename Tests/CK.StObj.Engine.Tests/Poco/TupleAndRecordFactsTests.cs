using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.StObj.Engine.Tests.Poco;

public class TupleAndRecordFactsTests
{
    class BuggyTupleUsage
    {
        public (int Power, string? Name, List<int> Values) Simple { get; set; }
    }

    class RefTupleSample
    {
        // This one is exposed by reference.
        (int Power, string? Name, List<int> Values) _vR;
        // This one is exposed by value.
        (int Power, string? Name, List<int> Values) _vV;

        public RefTupleSample()
        {
            _vR = (0, null, new List<int>());
            _vV = (0, null, new List<int>());
        }

        public ref (int Power, string? Name, List<int> Values) ByRefTuple => ref _vR;

        public ref (int Power, string? Name, List<int> Values) WritableByRefTuple
        {
            get => ref _vR;
            // Error CS8147  Properties which return by reference cannot have set accessors.
            // set => _vR = value;
        }

        public (int Power, string? Name, List<int> Values) ByValTuple { get => _vV; set => _vV = value; }
    }

    [Test]
    public void value_tuples_by_val_or_ref()
    {
        var t = new RefTupleSample();
        // RefTuple:
        //  - Initial nullability is correct.
        //  - Individual fields can be set.
        //  - Tuple as a whole can be set: respect of the nullability rules
        //    are under caller responsibility.
        Debug.Assert( t.ByRefTuple.Values != null );
        t.ByRefTuple.Name = "Hello!";
        Debug.Assert( t.ByRefTuple.Name == "Hello!" );
        t.ByRefTuple = (5, "Five", new List<int>());
        Debug.Assert( t.ByRefTuple.Name == "Five" );

        // CopyTuple:
        //  - Initial nullability is correct.
        //  - Individual fields cannot be set.
        //    Error CS1612  Cannot modify the return value of 'Class1.RefTupleSample.ByValTuple' because it is not a variable.
        //  - Tuple as a whole can be set: respect of the nullability rules
        //    are under caller responsibility.
        Debug.Assert( t.ByValTuple.Values != null );
        // t.CopyTuple.Name = "Hello!";
        t.ByValTuple = (5, "Five", new List<int>());
        Debug.Assert( t.ByValTuple.Name == "Five" );
        // Setting "Hello", preserving the other fields requires to 
        // copy them:
        t.ByValTuple = (t.ByValTuple.Power, "Hello", t.ByValTuple.Values);
    }

    // Positional record class: immutable.
    // Immutability is not exactly the primary point of Poco...

    // Moreover, record class can be specialized... Complicates the type closure.
    // 
    record SamplePC( List<int> Values, string Name = "Albert" );

    // Positional record struct: mutable.
    // Not really different from a Value tuple (with the same by ref/val
    // issues) except that it is a named, reusable, definition.
    // This should enter the Poco compliant types set...
    // 
    record struct SamplePV( int Power, List<int> Values, string Name = "Albert" );

    [Test]
    public void record_tests()
    {
        var s = new SamplePV( 5, new List<int>() );
        Debug.Assert( s.Name == "Albert" );
        s = s with { Name = "Pouet" };
        Debug.Assert( s.Name == "Pouet" );

        var c = new SamplePC( new List<int>() );
        c = c with { Name = "Pouet" };
    }
}
