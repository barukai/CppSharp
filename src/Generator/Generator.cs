﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CppSharp.AST;

namespace CppSharp.Generators
{
    /// <summary>
    /// Kinds of language generators.
    /// </summary>
    public enum GeneratorKind
    {
        CLI = 1,
        CSharp = 2
    }

    /// <summary>
    /// Output generated by each backend generator.
    /// </summary>
    public struct GeneratorOutput
    {
        /// <summary>
        /// Translation unit associated with output.
        /// </summary>
        public TranslationUnit TranslationUnit;

        /// <summary>
        /// Text templates with generated output.
        /// </summary>
        public List<Template> Templates;
    }

    /// <summary>
    /// Generators are the base class for each language backend.
    /// </summary>
    public abstract class Generator : IDisposable
    {
        public static string CurrentOutputNamespace = string.Empty;

        public Driver Driver { get; private set; }

        protected Generator(Driver driver)
        {
            Driver = driver;
            CppSharp.AST.Type.TypePrinterDelegate += TypePrinterDelegate;
        }

        /// <summary>
        /// Called when a translation unit is generated.
        /// </summary>
        public Action<GeneratorOutput> OnUnitGenerated = delegate { };

        /// <summary>
        /// Setup any generator-specific passes here.
        /// </summary>
        public abstract bool SetupPasses();

        /// <summary>
        /// Setup any generator-specific processing here.
        /// </summary>
        public virtual void Process()
        {

        }

        /// <summary>
        /// Generates the outputs.
        /// </summary>
        public virtual List<GeneratorOutput> Generate()
        {
            var outputs = new List<GeneratorOutput>();

            var units = Driver.ASTContext.TranslationUnits.Where(
                u => u.IsGenerated && u.HasDeclarations && !u.IsSystemHeader && u.IsValid).ToList();
            if (Driver.Options.IsCSharpGenerator && Driver.Options.GenerateSingleCSharpFile)
                GenerateSingleTemplate(outputs);
            else
                GenerateTemplates(outputs, units);
            return outputs;
        }

        private void GenerateTemplates(List<GeneratorOutput> outputs, List<TranslationUnit> units)
        {
            foreach (var unit in units)
            {
                var includeDir = Path.GetDirectoryName(unit.FilePath);
                var templates = Generate(new[] { unit });
                if (templates.Count == 0)
                    return;

                CurrentOutputNamespace = unit.Module.OutputNamespace;
                foreach (var template in templates)
                {
                    template.Process();
                }

                var output = new GeneratorOutput
                {
                    TranslationUnit = unit,
                    Templates = templates
                };
                outputs.Add(output);

                OnUnitGenerated(output);
            }
        }

        private void GenerateSingleTemplate(ICollection<GeneratorOutput> outputs)
        {
            foreach (var module in Driver.Options.Modules)
            {
                CurrentOutputNamespace = module.OutputNamespace;
                var output = new GeneratorOutput
                {
                    TranslationUnit = new TranslationUnit
                    {
                        FilePath = string.Format("{0}.cs", module.OutputNamespace ?? module.LibraryName),
                        Module = module
                    },
                    Templates = Generate(module.Units)
                };
                output.Templates[0].Process();
                outputs.Add(output);

                OnUnitGenerated(output);
            }
        }

        /// <summary>
        /// Generates the outputs for a given translation unit.
        /// </summary>
        public abstract List<Template> Generate(IEnumerable<TranslationUnit> units);

        protected abstract string TypePrinterDelegate(CppSharp.AST.Type type);

        public static string GeneratedIdentifier(string id)
        {
            return "__" + id;
        }

        public void Dispose()
        {
            CppSharp.AST.Type.TypePrinterDelegate -= TypePrinterDelegate;
        }
    }
}