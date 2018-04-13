using System.Collections.Generic;

namespace EkstaziSharp.Model
{
    public interface IInstrumentationModelBuilderFactory
    {
        IInstrumentationModelBuilder CreateModelBuilder(string modulePath, string moduleName);
    }
}