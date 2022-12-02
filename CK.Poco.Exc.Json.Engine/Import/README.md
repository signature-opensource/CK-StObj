# ImportCodeGenerator

>> [ExportCodeGenerator](../Export/README.md) should be read before this.

## Reading is not Writing

Reading is a bit more complicated than writing. Everything starts with a type name and the set of supported
type names is the same as the exported one. The reader functions are stored in a simple dictionary.
This static dictionary is built once when the StObjMap is loaded and will only be read (no concurrence issues).
 
These top-level reader functions returns an `object` and powers the static `ReadAny` root method that
reads the type name from the first cell of the required 2-cells array `["type",<value>]` and calls the
appropriate reader function.

These reader functions instantiate a non null object:
```csharp
delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.Export.PocoJsonImportOptions options );
```
They are limited to instantiate oblivious types: the type `IList<ISet<ICommand>>` is out of their scope because this
type is not an exported type name (it is exported as a "L(S(object))" that is `List<HashSet<object>>`).

These top-level functions cannot always be used internally: Poco collection types often use non regular collection
types (a `IList<IUserInfo>` where `IUserInfo : IPoco` is implemented by a generated specialized `List<IUserInfo>`
that supports an extended covariance). Moreover, Poco fields can be `readonly`: they cannot be assigned to a new instance,
only can they be filled with their content.

This why an Importer has more "readers" than an Exporter has "writers". An Exporter relies on the ObliviousType that
erase "internal types" to do its job whereas Importers have to deal with these internal types.

## Basic types
We need a function that reads a value. This function is typically inlined, it doesn't require
a dedicated method (the Utf8JsonReader provides some of them like `ReadGuid()`). By simply boxing the result `(object)`
we have the corresponding top-level reader function.
But we also need a function that can read the associated nullable value type (for instance to fill a `List<Guid?>`). This
must be done with a utility function:

static T ReadBasic_T( ref Utf8JsonReader r, PocoJsonImportOptions options )
{
  // Type dependent code comes here.
}

static T? NullReadBasic_T( ref Utf8JsonReader r, PocoJsonImportOptions options )
{
  if( r.TokenType == JsonTokenType.Null )
  {
    r.Read();
    return default;
  }
  return ReadBasic_T( ref r, options );
}

Here, the TopLevelReadBasic_T is ReadBasic_T.

## Records
For records that are value tuples with fields, the basic pattern is not optimal: we should be able to limit
useless copies by having a `ref` version of the read so we can read in place the value. And we need a specific
function for the null and for the top-level reader:

static void ReadRecord_T( ref Utf8JsonReader r, ref T v, PocoJsonImportOptions options )
{
  // Type dependent code comes here.
}

static void NullReadRecord_T( ref Utf8JsonReader r, ref T? v, PocoJsonImportOptions options )
{
  if( r.TokenType == JsonTokenType.Null )
  {
    r.Read();
    v = default;
  }
  else
  {
    ReadRecord_T( ref r, ref v, options );
  }
}

static T TopLevelReadRecord_T( ref Utf8JsonReader r, PocoJsonImportOptions options )
{
  var v = new T();
  InPlaceNullReader_T( ref r, ref v, options );
  return v;
}

## Reference types

There are only 2 kind of reference types in the Poco type system that must be handled here:
IPoco and Collections. The third kind of reference type is the AbstractIPoco and since it is
abstract we don't have to bother dealing with unexisting instances.

IPoco and Collections are quite similar here. Let's start with the IPoco that is simpler for one
reason: its `ReadJson` is a direct method of any IPoco (instead of a static independent helper).

Poco implementation has a generated default constructor that initializes its fields according to the
default values and nullability constraints. To avoid replicating this logic in a deserialization
constructor, an independent `ReadJson( ref Utf8JsonReader r, PocoJsonImportOptions options )`
method is implemented that must be called on the new instance: this method is an "in place" reader.
The top level reader must handle potentially null (whether a non null Poco is required is let to the
validation layer):

static TPoco? TopLevelReader_TPoco( ref Utf8JsonReader r, PocoJsonImportOptions options )
{
  if( r.TokenType == JsonTokenType.Null )
  {
    r.Read();
    return null;
  }
  var v = new TPoco();
  v.Read( ref r, options );
  return v;
}

Poco constructors instantiates non nullable subordinated IPoco. When reading:

public interface I1 : IPoco
{
  IOther Other { get; } 
}

The `Other` is already instantiated (with all its default values), the read must be done "in place".
If the property was a nullable (then it has necessarily a setter otherwise, this property would not be exchangeable
and serialization/deserialization would ignore it):

public interface I1 : IPoco
{
  IOther? Other { get; set; } 
}

In such case, the initial field value is null: reading "in place" only is not enough.
Do we need yet another variation that can allocate if needed?

static void NullReadReferenceType_T( ref Utf8JsonReader r, ref T? v, PocoJsonImportOptions options )
{
  if( r.TokenType == JsonTokenType.Null )
  {
    r.Read();
    v = null;
  }
  else
  {
    if( v == null ) v = new T();
    v.Read( ref r, options );
  }
}
Actually this is quite the same as the TopLevelReader_TPoco. If we are able to know whether a field
is initially null or not, we don't need this: `TopLevelReader_TPoco` or `_field.Read( ref r, options )`
is all we need.

The fact is that we know whether a field should use the top level reader or the in place read function:
- If the field's type is IPoco or a Collection:
  - If its DefaultValueInfo is RequiresInit then the instance has been created by the constructor: in place read
    must be done.
  - Else, the top-level reader must be called.

This can be extended to all the types
- If the field's type is an AbstractIPoco, a Union type or Any (`object`): the `ReadAny` root method must be used.
- If the field's type is a Record or an AnonymousRecord:
  - If the field's type is nullable, use the NullReadRecord_T reader.
  - Else use the ReadRecord_T reader
- If the field type is a Basic type:
  -  If the field's type is nullable, use the NullReadBasic_T.
  -  Else use the ReadBasic_T

To conclude, we only need for collections, the equivalent of the Poco's ReadJson instance method:

static void ReadCollection_T( ref Utf8JsonReader r, T collection, PocoJsonImportOptions options )
{
  // Type dependent reader.
}


## Reference type: collections and their "internal types"
One need both to create new instances of collections and to read them in place.
The pattern is similar to the record one except that no `ref` must be used since
`readonly` fields must be read.
The top level reader must handle null.

static void ReadCollection_T( Utf8JsonReader r, T v, PocoJsonImportOptions options )
{
  // Type dependent code comes here.
}

static void NullReadCollection_T( Utf8JsonReader r, ref T? v, PocoJsonImportOptions options )
{
  if( r.TokenType == JsonTokenType.Null )
  {
    r.Read();
    v = default;
  }
  else
  {
    ReadRecord_T( r, ref v, options );
  }
}

static T TopLevelReader_T( ref Utf8JsonReader r, PocoJsonImportOptions options )
{
  var v = new T();
  InPlaceNullReader_T( ref r, ref v, options );
  return v;
}


