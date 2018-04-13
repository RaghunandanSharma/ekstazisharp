using Mono.Cecil;
using EkstaziSharp.Model;
namespace EkstaziSharp.Symbols
{
    public interface ISymbolManager
    {
        string ModulePath { get; }

        string ModuleName { get; }

        File[] GetFiles();

        Class[] GetInstrumentableTypes();

        Method[] GetMethodsForType(Class type, File[] files);

        BranchPoint[] GetBranchPointsForToken(int token);

        AssemblyDefinition SourceAssembly { get; }

        File[] GetFiles(Class[] moduleClasses);
    }
}