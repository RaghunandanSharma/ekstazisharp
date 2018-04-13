using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using EkstaziSharp.Model;
using File = EkstaziSharp.Model.File;

namespace EkstaziSharp.Symbols
{
    internal static class CecilSymbolManageExtensions
    {
        public static MethodBody SafeGetMethodBody(this MethodDefinition methodDefinition)
        {
            try
            {
                if (methodDefinition.HasBody)
                {
                    return methodDefinition.Body;
                }
            }
            catch (Exception)
            {
                //Console.WriteLine("Exception whilst trying to get the body of method {0}", methodDefinition.FullName);
            }
            return null;
        }

    }

    internal class CecilSymbolManager : ISymbolManager
    {
        private const int StepOverLineCode = 0xFEEFEE;
        private AssemblyDefinition _sourceAssembly;
        private readonly Dictionary<int, MethodDefinition> _methodMap = new Dictionary<int, MethodDefinition>();

        public CecilSymbolManager()
        {
        }

        public string ModulePath { get; private set; }

        public string ModuleName { get; private set; }

        public string TargetDir { get; private set; }

        public void Initialise(string modulePath, string moduleName, string targetDir)
        {
            ModulePath = modulePath;
            ModuleName = moduleName;
            TargetDir = targetDir;
        }

        private SymbolFolder FindSymbolsFolder()
        {
            var origFolder = Path.GetDirectoryName(ModulePath);

            return FindSymbolsFolder(ModulePath, origFolder) ?? FindSymbolsFolder(ModulePath, TargetDir) ?? FindSymbolsFolder(ModulePath, Environment.CurrentDirectory);
        }

        private static SymbolFolder FindSymbolsFolder(string fileName, string targetfolder)
        {
            if (!string.IsNullOrEmpty(targetfolder) && Directory.Exists(targetfolder))
            {
                var name = Path.GetFileName(fileName);

                if (name != null)
                {
                    if (System.IO.File.Exists(Path.Combine(targetfolder,
                        Path.GetFileNameWithoutExtension(fileName) + ".pdb")))
                    {
                        if (System.IO.File.Exists(Path.Combine(targetfolder, name)))
                            return new SymbolFolder(targetfolder, new PdbReaderProvider());
                    }

                    if (System.IO.File.Exists(Path.Combine(targetfolder, fileName + ".mdb")))
                    {
                        if (System.IO.File.Exists(Path.Combine(targetfolder, name)))
                            return new SymbolFolder(targetfolder, new MdbReaderProvider());
                    }
                }
            }
            return null;
        }

        public AssemblyDefinition SourceAssembly
        {
            get
            {
                if (_sourceAssembly == null)
                {
                    var currentPath = Environment.CurrentDirectory;
                    try
                    {
                        var symbolFolder = FindSymbolsFolder();
                        var folder = symbolFolder.Maybe(_ => _.TargetFolder) ?? Environment.CurrentDirectory;

                        var parameters = new ReaderParameters
                        {
                            SymbolReaderProvider = symbolFolder.SymbolReaderProvider ?? new PdbReaderProvider(),
                            ReadingMode = ReadingMode.Deferred,
                            ReadSymbols = true
                        };
                        var fileName = Path.GetFileName(ModulePath) ?? string.Empty;
                        _sourceAssembly = AssemblyDefinition.ReadAssembly(Path.Combine(folder, fileName), parameters);

                        if (_sourceAssembly != null)
                            _sourceAssembly.MainModule.ReadSymbols(parameters.SymbolReaderProvider.GetSymbolReader(_sourceAssembly.MainModule, _sourceAssembly.MainModule.FullyQualifiedName));
                    }
                    catch (Exception)
                    {
                        // DLL's with no PDBs => no instrumentation
                        _sourceAssembly = null;
                    }
                    finally
                    {
                        Environment.CurrentDirectory = currentPath;
                    }
                    if (_sourceAssembly == null)
                    {
                        Console.WriteLine("Cannot Load {0} as no PDB/MDB could be loaded", ModulePath);
                    }
                }
                return _sourceAssembly;
            }
        }

        public File[] GetFiles()
        {
            var list = new List<File>();
            foreach (var instrumentableType in GetInstrumentableTypes())
            {
                list.AddRange(instrumentableType.Files);
            }
            return list.Distinct(new FileEqualityComparer()).Select(file => file).ToArray();
        }

        public File[] GetFiles(Class[] instrumentableTypes)
        {
            var list = new List<File>();
            foreach (var instrumentableType in instrumentableTypes)
            {
                list.AddRange(instrumentableType.Files);
            }
            return list.Distinct(new FileEqualityComparer()).Select(file => file).ToArray();
        }

        public Class[] GetInstrumentableTypes()
        {
            if (SourceAssembly == null) return new Class[0];
            var classes = new List<Class>();
            IEnumerable<TypeDefinition> typeDefinitions = SourceAssembly.MainModule.Types;
            GetInstrumentableTypes(typeDefinitions, classes, ModuleName);
            return classes.ToArray();
        }

        private static void GetInstrumentableTypes(IEnumerable<TypeDefinition> typeDefinitions, List<Class> classes, string moduleName)
        {
            foreach (var typeDefinition in typeDefinitions)
            {
                if (typeDefinition.IsEnum) continue;
                if (typeDefinition.IsInterface && typeDefinition.IsAbstract) continue;
                var @class = new Class { FullName = typeDefinition.FullName };

                var list = new List<string>();
                if (true)
                {
                    var files = from methodDefinition in typeDefinition.Methods
                                where methodDefinition.SafeGetMethodBody() != null && methodDefinition.Body.Instructions != null
                                from instruction in methodDefinition.Body.Instructions
                                where instruction.SequencePoint != null
                                select instruction.SequencePoint.Document.Url;

                    list.AddRange(files.Distinct());
                }

                // only instrument types that are not structs and have instrumentable points
                if (!typeDefinition.IsValueType || list.Count > 0)
                {
                    @class.Files = list.Distinct().Select(file => new File { FullPath = file }).ToArray();
                    classes.Add(@class);
                }

                if (typeDefinition.HasNestedTypes)
                    GetInstrumentableTypes(typeDefinition.NestedTypes, classes, moduleName);
            }
        }

        public Method[] GetMethodsForType(Class type, File[] files)
        {
            var methods = new List<Method>();
            IEnumerable<TypeDefinition> typeDefinitions = SourceAssembly.MainModule.Types;
            GetMethodsForType(typeDefinitions, type.FullName, methods, files);
            return methods.ToArray();
        }

        private static string GetFirstFile(MethodDefinition definition)
        {
            if (definition.SafeGetMethodBody() != null && definition.Body.Instructions != null)
            {
                var filePath = definition.Body.Instructions
                    .FirstOrDefault(x => x.SequencePoint != null && x.SequencePoint.Document != null)
                    .Maybe(x => x.SequencePoint.Document.Url);
                return filePath;
            }
            return null;
        }

        private static void GetMethodsForType(IEnumerable<TypeDefinition> typeDefinitions, string fullName, List<Method> methods, File[] files)
        {
            foreach (var typeDefinition in typeDefinitions)
            {
                if (typeDefinition.FullName == fullName)
                {
                    BuildPropertyMethods(methods, files, typeDefinition);
                    BuildMethods(methods, files, typeDefinition);
                }
                if (typeDefinition.HasNestedTypes)
                    GetMethodsForType(typeDefinition.NestedTypes, fullName, methods, files);
            }
        }

        private static void BuildMethods(ICollection<Method> methods, File[] files, TypeDefinition typeDefinition)
        {
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                if (methodDefinition.IsAbstract) continue;
                if (methodDefinition.IsGetter) continue;
                if (methodDefinition.IsSetter) continue;

                var method = BuildMethod(files, methodDefinition);
                methods.Add(method);
            }
        }

        private static void BuildPropertyMethods(ICollection<Method> methods, File[] files, TypeDefinition typeDefinition)
        {
            foreach (var propertyDefinition in typeDefinition.Properties)
            {

                if (propertyDefinition.GetMethod != null && !propertyDefinition.GetMethod.IsAbstract)
                {
                    var method = BuildMethod(files, propertyDefinition.GetMethod);
                    methods.Add(method);
                }

                if (propertyDefinition.SetMethod != null && !propertyDefinition.SetMethod.IsAbstract)
                {
                    var method = BuildMethod(files, propertyDefinition.SetMethod);
                    methods.Add(method);
                }
            }
        }

        private static Method BuildMethod(IEnumerable<File> files, MethodDefinition methodDefinition)
        {
            var method = new Method
            {
                Name = methodDefinition.FullName,
                IsConstructor = methodDefinition.IsConstructor,
                IsStatic = methodDefinition.IsStatic,
                IsGetter = methodDefinition.IsGetter,
                IsSetter = methodDefinition.IsSetter,
                MetadataToken = methodDefinition.MetadataToken.ToInt32()
            };

            if (methodDefinition.SafeGetMethodBody() == null)
            {
                if (methodDefinition.IsNative)
                    method.MarkAsSkipped(SkippedMethod.NativeCode);
                else
                    method.MarkAsSkipped(SkippedMethod.Unknown);
                return method;
            }

            var definition = methodDefinition;
            method.FileRef = files.Where(x => x.FullPath == GetFirstFile(definition))
                .Select(x => new FileRef { UniqueId = x.UniqueId }).FirstOrDefault();
            return method;
        }

        public BranchPoint[] GetBranchPointsForToken(int token)
        {
            BuildMethodMap();
            var list = new List<BranchPoint>();
            GetBranchPointsForToken(token, list);
            return list.ToArray();
        }

        private void BuildMethodMap()
        {
            if (_methodMap.Count > 0) return;
            IEnumerable<TypeDefinition> typeDefinitions = SourceAssembly.MainModule.Types;
            BuildMethodMap(typeDefinitions);
        }

        private void BuildMethodMap(IEnumerable<TypeDefinition> typeDefinitions)
        {
            foreach (var typeDefinition in typeDefinitions)
            {
                foreach (var methodDefinition in typeDefinition.Methods
                    .Where(methodDefinition => methodDefinition.SafeGetMethodBody() != null && methodDefinition.Body.Instructions != null))
                {
                    _methodMap.Add(methodDefinition.MetadataToken.ToInt32(), methodDefinition);
                }
                if (typeDefinition.HasNestedTypes)
                {
                    BuildMethodMap(typeDefinition.NestedTypes);
                }
            }
        }

        private void GetBranchPointsForToken(int token, List<BranchPoint> list)
        {
            var methodDefinition = GetMethodDefinition(token);
            if (methodDefinition == null) return;
            try
            {
                UInt32 ordinal = 0;

                foreach (var instruction in methodDefinition.SafeGetMethodBody().Instructions)
                {
                    if (instruction.OpCode.FlowControl != FlowControl.Cond_Branch)
                        continue;

                    if (BranchIsInGeneratedFinallyBlock(instruction, methodDefinition)) continue;

                    var pathCounter = 0;

                    // store branch origin offset
                    var branchOffset = instruction.Offset;
                    var closestSeqPt = FindClosestSequencePoints(methodDefinition.Body, instruction);
                    var branchingInstructionLine = closestSeqPt.Maybe(sp => sp.SequencePoint.StartLine, -1);
                    var document = closestSeqPt.Maybe(sp => sp.SequencePoint.Document.Url);

                    if (null == instruction.Next)
                        return;

                    // Add Default branch (Path=0)

                    // Follow else/default instruction
                    var @else = instruction.Next;

                    var pathOffsetList = GetBranchPath(@else);

                    // add Path 0
                    var path0 = new BranchPoint
                    {
                        StartLine = branchingInstructionLine,
                        Document = document,
                        Offset = branchOffset,
                        Ordinal = ordinal++,
                        Path = pathCounter++,
                        OffsetPoints =
                            pathOffsetList.Count > 1
                                ? pathOffsetList.GetRange(0, pathOffsetList.Count - 1)
                                : new List<int>(),
                        EndOffset = pathOffsetList.Last()
                    };
                    list.Add(path0);

                    // Add Conditional Branch (Path=1)
                    if (instruction.OpCode.Code != Code.Switch)
                    {
                        // Follow instruction at operand
                        var @then = instruction.Operand as Instruction;
                        if (@then == null)
                            return;

                        pathOffsetList = GetBranchPath(@then);

                        // Add path 1
                        var path1 = new BranchPoint
                        {
                            StartLine = branchingInstructionLine,
                            Document = document,
                            Offset = branchOffset,
                            Ordinal = ordinal++,
                            Path = pathCounter,
                            OffsetPoints =
                                pathOffsetList.Count > 1
                                    ? pathOffsetList.GetRange(0, pathOffsetList.Count - 1)
                                    : new List<int>(),
                            EndOffset = pathOffsetList.Last()
                        };
                        list.Add(path1);
                    }
                    else // instruction.OpCode.Code == Code.Switch
                    {
                        var branchInstructions = instruction.Operand as Instruction[];
                        if (branchInstructions == null || branchInstructions.Length == 0)
                            return;

                        // Add Conditional Branches (Path>0)
                        foreach (var @case in branchInstructions)
                        {
                            // Follow operand istruction
                            pathOffsetList = GetBranchPath(@case);

                            // add paths 1..n
                            var path1ToN = new BranchPoint
                            {
                                StartLine = branchingInstructionLine,
                                Document = document,
                                Offset = branchOffset,
                                Ordinal = ordinal++,
                                Path = pathCounter++,
                                OffsetPoints =
                                    pathOffsetList.Count > 1
                                        ? pathOffsetList.GetRange(0, pathOffsetList.Count - 1)
                                        : new List<int>(),
                                EndOffset = pathOffsetList.Last()
                            };
                            list.Add(path1ToN);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("An error occurred with 'GetBranchPointsForToken' for method '{0}'",
                        methodDefinition.FullName), ex);
            }
        }

        private static bool BranchIsInGeneratedFinallyBlock(Instruction branchInstruction, MethodDefinition methodDefinition)
        {
            if (!methodDefinition.Body.HasExceptionHandlers)
                return false;

            // a generated finally block will have no sequence points in its range
            var handlers = methodDefinition.Body.ExceptionHandlers
                .Where(e => e.HandlerType == ExceptionHandlerType.Finally)
                .ToList();

            return handlers
                .Where(e => branchInstruction.Offset >= e.HandlerStart.Offset)
                .Where(e => branchInstruction.Offset < e.HandlerEnd.Maybe(h => h.Offset, GetOffsetOfNextEndfinally(methodDefinition.Body, e.HandlerStart.Offset)))
                .OrderByDescending(h => h.HandlerStart.Offset)
                .Any(eh => !methodDefinition.Body.Instructions
                    .Where(i => i.SequencePoint != null && i.SequencePoint.StartLine != StepOverLineCode)
                    .Any(i => i.Offset >= eh.HandlerStart.Offset && i.Offset < eh.HandlerEnd.Maybe(h => h.Offset, GetOffsetOfNextEndfinally(methodDefinition.Body, eh.HandlerStart.Offset))));
        }

        private static int GetOffsetOfNextEndfinally(MethodBody body, int startOffset)
        {
            var lastOffset = body.Instructions.LastOrDefault().Maybe(i => i.Offset, int.MaxValue);
            return body.Instructions.FirstOrDefault(i => i.Offset >= startOffset && i.OpCode.Code == Code.Endfinally).Maybe(i => i.Offset, lastOffset);
        }

        private List<int> GetBranchPath(Instruction instruction)
        {
            var offsetList = new List<int>();

            if (instruction != null)
            {
                var point = instruction;
                offsetList.Add(point.Offset);
                while (point.OpCode == OpCodes.Br || point.OpCode == OpCodes.Br_S)
                {
                    var nextPoint = point.Operand as Instruction;
                    if (nextPoint != null)
                    {
                        point = nextPoint;
                        offsetList.Add(point.Offset);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return offsetList;
        }

        private Instruction FindClosestSequencePoints(MethodBody methodBody, Instruction instruction)
        {
            var sequencePointsInMethod = methodBody.Instructions.Where(HasValidSequencePoint).ToList();
            if (!sequencePointsInMethod.Any()) return null;
            var idx = sequencePointsInMethod.BinarySearch(instruction, new InstructionByOffsetCompararer());
            Instruction prev;
            if (idx < 0)
            {
                // no exact match, idx corresponds to the next, larger element
                var lower = Math.Max(~idx - 1, 0);
                prev = sequencePointsInMethod[lower];
            }
            else
            {
                // exact match, idx corresponds to the match
                prev = sequencePointsInMethod[idx];
            }

            return prev;
        }

        private bool HasValidSequencePoint(Instruction instruction)
        {
            return instruction.SequencePoint != null && instruction.SequencePoint.StartLine != StepOverLineCode;
        }

        private class InstructionByOffsetCompararer : IComparer<Instruction>
        {
            public int Compare(Instruction x, Instruction y)
            {
                return x.Offset.CompareTo(y.Offset);
            }
        }

        private MethodDefinition GetMethodDefinition(int token)
        {
            return !_methodMap.ContainsKey(token) ? null : _methodMap[token];
        }
    }
}
