# Json serialization

This package triggers the code generation of Json serialization and deserialization of Poco. Poco are the **roots of serialization**,
they bootstrap the serialization and deserialization process.

The key aspects of this serialization are:
- Serialization starts with Poco: data must be subordinated to a Poco, it must ultimately be referenced by a Poco property: see [Registered types only](#registered-types-only) below.
- We do NOT handle graphs: the serialized object must not reference itself: cycles are error.
- The serialization code is generated, this does not use reflection. The way this work is more complex that the basics System.Text.Json. More complex but also deeply "type safe" and
potentially more efficient.
- Adding code generation to support specific types or types family is possible. Recall that it is not a converter (see [here](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to)
for instance) that must be written but the code that generates the code to read/write the object.
An example is available in the tests [here](../Tests/CK.StObj.Engine.Tests/PocoJson/JsonSerializerViaToStringAndParseGenerator.cs).

## Usage

This package exposes two extension methods, one on every IPoco (write, serialize) and one on the PocoDirectory singleton (to read, deserialize):
```csharp
public static void Write( this IPoco? o, Utf8JsonWriter writer, bool withType = true ) {...}
public static IPoco? ReadPocoValue( this PocoDirectory directory, ref Utf8JsonReader reader ) {...}
```

Note that when written the Poco can be null (the extension method handles it and emits a `null`).
This package also overrides the Poco's class implementation `ToString()` method (if this method is not already generated) so that
the JSON representation of the Poco is returned (this comes in handy when debugging).

## Basic types

Serialization uses the [Utf8JsonWriter](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonwriter) and deserialization uses
the [Utf8JsonReader](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader) with the default options.

The following basic types are handled: bool, string, int, long, ulong, uint, double, float, decimal, byte, sbyte, short, ushort, DateTime, DateTimeOffset, TimeSpan,
byte[] (encoded in [base64](https://source.dot.net/#System.Text.Json/System/Text/Json/Writer/Utf8JsonWriter.WriteValues.Bytes.cs)),
[BigInteger](https://docs.microsoft.com/en-us/dotnet/api/system.numerics.biginteger) and Guid.

The following complex types are handled:

- A `T[]` (array), `IList<T>`, `List<T>`, `ISet<T>`, `Set<T>` is serialized as an array (the JSON representation is the same, the C# types can be interchanged). 
- A `IDictionary<,>` or `Dictionary<,>` is serialized as 
  - a Json object when the type of the key is string 
  - and as an array of 2-cells arrays when the key is an object. 
- Value tuples are serialized as arrays.

Nullable value types and nullable reference types are automatically handled.

Enum values are serialized with their numerical values.

## Registered types only

Only registered types can be de/serialized (often named "Known Types" in numerous serialization frameworks).
Even enums must be registered to be serializable. Knowing the complete set of the serializable types can be complex<a href="#n1" id="r1"><sup>1</sup></a>.

This is where the Poco help: as soon as a Poco must be serializable, the transitive
closure of the referenced types (the Poco's properties' types) are automatically registered.

> Only types that are really needed are registered (and the de/serialization code is generated). Code to handle what may seem basic types like `int?` 
> or `string[]` will be generated only if they are actually used by at least one serializable Poco.

A Poco property can be an untyped `object` (may be defined by a [UnionType](../CK.StObj.Model/Poco/UnionTypeAttribute.cs)), a value tuple, list, array, etc.
When de/serializing an `object` or any abstraction (an interface or a base class), a 2-cells array with the type name and the value is used:
polymorphism is required.

## Json polymorphism support

One of the big issue to solve when de/serializing objects is to manage polymorphism: when an abstraction must be serialized, the type of the serialized
object **must** also be written so that deserialization know what to instantiate.

### The classical approach
The classical approach here is to inject a `$type` (or `_$type$_` or any strange enough property that contains a string with the name of the type)
into the object properties. 
Since in JSON, collections (arrays, lists, sets) are expressed with `[arrays]` (and this cannot hold any property), another syntactical
construct is required for collections, typically something like `{ "$type" : "A(int)", "$values" : [1,2,3] }`.

Note that, in this approach, serialized objects with or without a type are structurally identical (only the existence of the `$type` property makes
the difference) whereas serialized collections ARE structurally different (they appear inside a "wrapper" object).

> When is a `$type` NOT required? When the object's type is *statically known*, when the de/serialized type is *unambiguous*. As soon as 
> an ambiguity exists, **polymorphism** is at stake.

The `object` type being the "worst case of polymorphism", but any interface or base class with specializations are *ambiguous*), the 
runtime type of the object MUST be serialized and MUST be used to deserialize the object.

We consider "Structural Difference" between unambiguous and polymorphic objects to be a good thing since this forces the reader to
take care of the *object's type* if there is one.
Without any difference in the shape of the data, a polymorphic object can be falsely read as an object of an *unambiguous* (wrong) type. 

### Structural Polymorphism

We are using a different approach that offers the following advantages:
- No "magic name" like `$type` or `$values`.
- Uniform type handling for objects and collections: there is always a "Structural Difference" between a *polymorphic* and an *unambiguous* shape.

Thanks to this, the code is simple and can be split in 3 layers:
- Nullable layer: the `null` occurrence is handled.
- Polymorphic layer: a 2-cells array is expected, the first cell containing the type name, the second one the unambiguous object's representation.
- Unambiguous layer (the exact type is known): the serialize/deserialize code handles purely the object's data.

Last (but not least) advantage of this approach: when deserializing, **the type always appear BEFORE the object's data**. This enable deserializer to
easily work in "pure streaming" (no lookup required, no intermediate instantiation): the Utf8Reader can be used directly in forward-only mode.

Below is a simple example with:
```csharp
class Person
{
   public string Name { get; set; }
}

class Student : Person
{
    public int Age { get; set; }
}

class Teacher : Person
{
    public bool IsChief { get; set; }
}
```

The C# `Student[]` is an array of Student. There is no specialization, hence no ambiguity: `[{"Name":"A", "Age":12},{"Name":"B", "Age":13}]`.

The C# `Person[]` can contain 3 different types of objects. Each item needs to be "typed":
`[["Student",{"Name":"A", "Age":12}],["Person",{"Name":"E"}],["Teacher",{"Name":"T","IsChief:false}]]`.

When the same array of Persons is known only as an object[] (or any other non-unique "base" type like the non-generic `ICollection`),
then the array itself requires its type:
`["Person[]",[["Student",{"Name":"A", "Age":12}],["Person",{"Name":"E"}],["Teacher",{"Name":"T","IsChief:false}]]]`

### The type name

The serialized type name is by default the full name of the type (namespace + type name). To support explicit (shorter, nicer) names
and ease migrations/evolutions, the [`ExternalName` attribute](../CK.StObj.Model/ExternalNameAttribute.cs) can be used:
```csharp
// In v3, a the Person class was Employee.
[ExternalName("Person", "Employee")]
class Person
{
   public string Name { get; set; }
}
```

Basic types use their alias or type name: "bool", "string, "int", "long", "ulong", "uint", "double", "float", "decimal", "byte", "sbyte", "short", "ushort", "DateTime",
"DateTimeOffset", "TimeSpan", "byte[]", "Guid" and "BigInteger".

For automatically handled types:
- Arrays use "T[]": "bool[]", "Guid[]".
- Lists use "L(T)": "L(uint)".
- Dictionaries use "M(TKey,TValue)": "M(int,string)".
- Sets use "S(T)": "S(decimal)".
- Value types use their nice bracket form: "(int,string,double)".

## Roundtrip-able serializations: "pure Json" and "ECMAScript safe"

JSON format defines no limitation on numeric values. 64 bits integer and [BigInteger](https://docs.microsoft.com/en-us/dotnet/api/system.numerics.biginteger)
can be write and read without loss in JSON.

Unfortunately, ECMAScript only handles [64 bits floats](https://en.wikipedia.org/wiki/Double-precision_floating-point_format): 64 bits integer (and larger)
cannot be write and read back safely. Only integer values between [Number.MIN_SAFE_INTEGER](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number/MIN_SAFE_INTEGER)
and [Number.MAX_SAFE_INTEGER](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number/MAX_SAFE_INTEGER) (53 bits) are safe on the ECMAScript side.

Among the basic types, the long (Int64), ulong (UInt64) and Decimal representations are not ECMAScript compliant. They must use a string representation, just like [JsonNumberHandling.WriteAsString](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonnumberhandling)
specifies it. There are in fact 2 different serializations: the "pure Json" and the "ECMAScript safe". Introducing this complexity only because of big integers handling
may seem overkill... However, by putting more thoughts in the "ECMAScript safe" mode, it can do much more than just correctly handling the serialization of these big integers.

### The "ECMAScript standard" mode 

To be "ECMAScript safe", it is enough to de/serialize big integers as strings. But we can go a little bit farther by thinking to the types that an ECMAScript client
expects. On the client side, a numeric can be:
- a [number](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number)
- or a [BigInt](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/BigInt)
- or a dedicated "type" (typically an ES6 class) that encapsulates the actual value (a "boxed number").

If we want to preserve typings between C# and ECMASCript client, the client must support a dedicated type for byte, sbyte, short, ushort, float (single), etc.
(these boxed numbers' main responsibility being to restrict the value to their domain definition). This can be considered overkill (and will deeply hurt
front end developers!). Actually C# types are not expected on the client side. Most often, front end developers want a simple `number` whatever the C#/Server counterpart
is (byte, sbyte, etc.).

This implies a kind of [type erasure](https://en.wikipedia.org/wiki/Type_erasure) that maps float, single, small integers up to the Int32 to `Number` and big
integers to `BigInt` (not the same as the the C# `BigInteger`). In this mode, client code manipulates a `power` property as `number` and if this property is
eventually a ushort (on the server side), it is up to the client to check this before sending it back (and the server will validate its inputs anyway).  

We then consider 3 different "modes" to serialize things:
  - **Server:**
    - This mode must not be used with an ECMAScript client since it will not be able to exchange big integers.
    - **Data representation:** Uses JSON capabilities to represent numbers without constraints.
    - **Type Mapping:** None. A '`byte` is a `byte`. A `Dictionary<float,sbyte>` is a `M(float,sbyte)`.
  - **ECMAScript safe:**
    - This mode guaranties that data representation can be read by an ECMAScript client.
    - **Data representation:** Big integers (long, ulong, decimal and System.Numerics.BigInteger) are written and read as strings.
    - **Type Mapping:** None (same as Server).
  - **ECMAScript standard:**
    - This mode simplifies the types for an ECMAScript client.
    - **Data representation:** Big integers are written and read as strings (same as ECMAScript safe).
    - **Type Mapping:** This mode introduces 2 purely client types that are "Number" and "BigInt". The float, single, small integers up to the Int32 are 
   exchanged as `Number` and big integers (long, ulong, BigIntegers) are exchanged as `BigInt`.



### Implementation notes about "ECMAScript safe" mode number handling

The [JsonNumberHandling](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonnumberhandling) is NOT available on the Utf8JsonWriter API.
It is handled by the "Serialization" layer. And the APIs to [write (WriteNumberValueAsString(ulong))](https://source.dot.net/#System.Text.Json/System/Text/Json/Writer/Utf8JsonWriter.WriteValues.UnsignedNumber.cs,112)
or [read (GetUInt64WithQuotes)](https://source.dot.net/#System.Text.Json/System/Text/Json/Reader/Utf8JsonReader.TryGet.cs,383) quoted long or unsigned long (same for Decimal) are not publicly
exposed on the Utf8Writer/Reader.

> We currently handle long, ulong, decimal, BigInteger and TimeSpan (BigInteger and TimeSpan have no direct support) by writing/reading strings.
> Waiting for https://github.com/dotnet/runtime/issues/54016 and https://github.com/dotnet/runtime/issues/1784 (for BigInteger) for a more efficient implementation.


```csharp
        [ExternalName( "BasicTypes" )]
        public interface IAllBasicTypes : IPoco
        {
            byte Byte { get; set; }
            sbyte SByte { get; set; }
            short Short { get; set; }
            ushort UShort { get; set; }
            int Integer { get; set; }
            uint UInteger { get; set; }
            long Long { get; set; }
            ulong ULong { get; set; }
            float Float { get; set; }
            double Double { get; set; }
            decimal Decimal { get; set; }
            BigInteger BigInt { get; set; }
            DateTime DateTime { get; set; }
            DateTimeOffset DateTimeOffset { get; set; }
            TimeSpan TimeSpan { get; set; }
            Guid Guid { get; set; }
        }

        [Test]
        public void all_basic_types_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllBasicTypes ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var nMax = s.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMax.Byte = Byte.MaxValue;
            nMax.SByte = SByte.MaxValue;
            nMax.Short = Int16.MaxValue;
            nMax.UShort = UInt16.MaxValue;
            nMax.Integer = Int32.MaxValue;
            nMax.UInteger = UInt32.MaxValue;
            nMax.Long = Int64.MaxValue;
            nMax.ULong = UInt64.MaxValue;
            nMax.Float = Single.MaxValue;
            nMax.Double = Double.MaxValue;
            nMax.Decimal = Decimal.MaxValue;
            nMax.BigInt = BigInteger.Parse( "12345678901234567890123456789012345678901234567890123456789012345678901234567890" );
            nMax.DateTime = Util.UtcMaxValue;
            nMax.DateTimeOffset = DateTimeOffset.MaxValue;
            nMax.TimeSpan = TimeSpan.MaxValue;
            nMax.Guid = Guid.Parse( "ffffffff-ffff-ffff-ffff-ffffffffffff" );

            var nMin = s.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMin.Byte = Byte.MinValue;
            nMin.SByte = SByte.MinValue;
            nMin.Short = Int16.MinValue;
            nMin.UShort = UInt16.MinValue;
            nMin.Integer = Int32.MinValue;
            nMin.UInteger = UInt32.MinValue;
            nMin.Long = Int64.MinValue;
            nMin.ULong = UInt64.MinValue;
            nMin.Float = Single.MinValue;
            nMin.Double = Double.MinValue;
            nMin.Decimal = Decimal.MinValue;
            nMin.BigInt = BigInteger.Parse( "-12345678901234567890123456789012345678901234567890123456789012345678901234567890" );
            nMin.DateTime = Util.UtcMinValue;
            nMin.DateTimeOffset = DateTimeOffset.MinValue;
            nMin.TimeSpan = TimeSpan.MinValue;
            nMin.Guid = Guid.Empty;

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text => TestHelper.Monitor.Info( $"INumerics(max) serialization: " + text ) );
            nMax2.Should().BeEquivalentTo( nMax );

            var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text => TestHelper.Monitor.Info( $"INumerics(min) serialization: " + text ) );
            nMin2.Should().BeEquivalentTo( nMin );
        }
```
The `nMax` representation below shows the number/string mappings for the basic types:
```json
["BasicTypes", {
	"Byte": 255,
	"SByte": 127,
	"Short": 32767,
	"UShort": 65535,
	"Integer": 2147483647,
	"UInteger": 4294967295,
	"Long": "9223372036854775807",
	"ULong": "18446744073709551615",
	"Float": 3.4028235E+38,
	"Double": 1.7976931348623157E+308,
	"Decimal": "79228162514264337593543950335",
	"BigInt": "12345678901234567890123456789012345678901234567890123456789012345678901234567890",
	"DateTime": "9999-12-31T23:59:59.9999999Z",
	"DateTimeOffset": "9999-12-31T23:59:59.9999999+00:00",
	"TimeSpan": "9223372036854775807",
	"Guid": "ffffffff-ffff-ffff-ffff-ffffffffffff"
}]
```

-----
<a id="n1" href="#r1"><sup>1</sup></a>: The classical approach to define Know Types is to mark them with a decorator/attribute, but this has limitations,
please read this [article about WCF Data Contract](https://docs.microsoft.com/en-us/archive/msdn-magazine/2011/february/msdn-magazine-data-contract-inheritance-known-types-and-the-generic-resolver)
(and note that the final solution of the author - the "[generic resolver](https://docs.microsoft.com/en-us/archive/msdn-magazine/2011/february/msdn-magazine-data-contract-inheritance-known-types-and-the-generic-resolver#the-generic-resolver)"
with its default constructor - defeats the purpose of the "Known Types" idea that is to restrict the set of allowed types to the minimum).

