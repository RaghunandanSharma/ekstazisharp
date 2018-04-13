using System;
using System.IO;
using System.Security.Cryptography;
using Mono.Cecil;
using EkstaziSharp.Symbols;

namespace EkstaziSharp.Model
{
    internal class InstrumentationModelBuilder : IInstrumentationModelBuilder
    {
        private readonly ISymbolManager _symbolManager;

        public InstrumentationModelBuilder(ISymbolManager symbolManager)
        {
            _symbolManager = symbolManager;
        }

        public Module BuildModuleModel()
        {
            var module = CreateModule();
            return module;
        }

        private Module CreateModule()
        {
            var hash = string.Empty;
            if (System.IO.File.Exists(_symbolManager.ModulePath))
            {
                hash = HashFile(_symbolManager.ModulePath);
            }
            var module = new Module
                             {
                                 ModuleName = _symbolManager.ModuleName,
                                 FullName = _symbolManager.ModulePath,
                                 ModuleHash = hash
                             };
            module.Aliases.Add(_symbolManager.ModulePath);
            module.Classes = _symbolManager.GetInstrumentableTypes();
            module.Files = _symbolManager.GetFiles(module.Classes);
            foreach (var @class in module.Classes)
            {
                BuildClassModel(@class, module.Files);
            }

            return module;
        }

       /* public Module BuildModuleTestModel(Module module)
        {
            module = module ?? CreateModule();
            module.TrackedMethods = _symbolManager.GetTrackedMethods();
            return module;
        }*/

        private string HashFile(string sPath)
        {
            using (var sr = new StreamReader(sPath))
            using (var prov = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(prov.ComputeHash(sr.BaseStream));
            }
        }

        public bool CanInstrument
        {
            get { return _symbolManager.SourceAssembly != null; }
        }

        public AssemblyDefinition GetAssemblyDefinition {
            get { return _symbolManager.SourceAssembly; }
        }

        private void BuildClassModel(Class @class, File[] files)
        {
            var methods = _symbolManager.GetMethodsForType(@class, files);
            @class.Methods = methods;
        }
    }
}
