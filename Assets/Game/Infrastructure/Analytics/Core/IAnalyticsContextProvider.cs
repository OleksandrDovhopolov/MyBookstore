using System.Collections.Generic;

namespace Analytics
{
    public interface IAnalyticsContextProvider
    {
        IReadOnlyDictionary<string, object> GetCommonParameters();
    }
}
