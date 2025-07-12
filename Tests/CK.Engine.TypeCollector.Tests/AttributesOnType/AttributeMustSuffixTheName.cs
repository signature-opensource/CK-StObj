using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public class AttributeMustSuffixTheNamePT : PrimaryTypeAttribute
{
    public AttributeMustSuffixTheNamePT()
        : base( "Unused (name check fails)." )
    {
    }
}

public class AttributeMustSuffixTheNamePM : PrimaryMemberAttribute
{
    public AttributeMustSuffixTheNamePM()
        : base( "Unused (name check fails)." )
    {
    }
}

public class AttributeMustSuffixTheNameST : SecondaryTypeAttribute<AttributeMustSuffixTheNamePT>
{
    public AttributeMustSuffixTheNameST()
        : base( "Unused (name check fails)." )
    {
    }
}

public class AttributeMustSuffixTheNameSM : SecondaryMemberAttribute<AttributeMustSuffixTheNamePM>
{
    public AttributeMustSuffixTheNameSM()
        : base( "Unused (name check fails)." )
    {
    }
}

// Correct secondary name that targets an invalid named primary attribute.
// The implementation type is instantiated.
public class UselessTImpl : SecondaryTypeAttributeImpl { }

public class CorrectSecondaryOfInvalidPrimaryTAttribute : SecondaryTypeAttribute<AttributeMustSuffixTheNamePT>
{
    public CorrectSecondaryOfInvalidPrimaryTAttribute()
        : base( "CK.Engine.TypeCollector.Tests.UselessTImpl, CK.Engine.TypeCollector.Tests" )
    {
    }
}

public class UselessMImpl : SecondaryMemberAttributeImpl { }

public class CorrectSecondaryOfInvalidPrimaryMAttribute : SecondaryMemberAttribute<AttributeMustSuffixTheNamePM>
{
    public CorrectSecondaryOfInvalidPrimaryMAttribute()
        : base( "CK.Engine.TypeCollector.Tests.UselessMImpl, CK.Engine.TypeCollector.Tests" )
    {
    }
}

