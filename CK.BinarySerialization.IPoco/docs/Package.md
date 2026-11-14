Teaches the binary serializer to read and write any IPoco, as a length-prefixed JSON payload.
Referencing the package is enough: PocoSerializerResolver and PocoDeserializerResolver are registered automatically,
and the serialization contexts' services must contain the PocoDirectory.
