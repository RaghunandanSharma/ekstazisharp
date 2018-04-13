using Mono.Cecil;

namespace EkstaziSharp.Model
{
    public interface IInstrumentationModelBuilder
    {
        Module BuildModuleModel();
        
        bool CanInstrument { get; }

        AssemblyDefinition GetAssemblyDefinition { get; }
    }
}