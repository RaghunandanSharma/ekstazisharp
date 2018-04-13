using System.Collections.Generic;
using System.Linq;
using EkstaziSharp.Symbols;
using log4net;

namespace EkstaziSharp.Model
{
    public class InstrumentationModelBuilderFactory : IInstrumentationModelBuilderFactory
    {
        public InstrumentationModelBuilderFactory()
        {
        }

        public IInstrumentationModelBuilder CreateModelBuilder(string modulePath, string moduleName)
        {
            var manager = new CecilSymbolManager();
            manager.Initialise(modulePath, moduleName, null);
            return new InstrumentationModelBuilder(manager);
        }

    }
}