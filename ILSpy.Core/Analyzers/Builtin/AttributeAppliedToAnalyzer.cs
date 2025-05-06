// Copyright (c) 2018 Siegfried Pammer
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.Analyzers.Builtin
{
	[ExportAnalyzer(Header = "Applied To", Order = 10)]
	class AttributeAppliedToAnalyzer : IAnalyzer
	{
		public IEnumerable<ISymbol> Analyze(ISymbol analyzedSymbol, AnalyzerContext context)
		{
            if (!(analyzedSymbol is ITypeDefinition attributeType))
                return Array.Empty<ISymbol>();
            var scope = context.GetScopeOf(attributeType);
            // TODO: DeclSecurity attributes are not supported.
            return !IsBuiltinAttribute(attributeType, out var knownAttribute) ? HandleCustomAttribute(attributeType, scope) : HandleBuiltinAttribute(knownAttribute, scope).SelectMany(s => s);
        }

        bool IsBuiltinAttribute(ITypeDefinition attributeType, out KnownAttribute knownAttribute)
        {
	        knownAttribute = attributeType.IsBuiltinAttribute();
	        return knownAttribute switch
	        {
		        KnownAttribute.Serializable => true,
		        KnownAttribute.ComImport => true,
		        KnownAttribute.StructLayout => true,
		        KnownAttribute.DllImport => true,
		        KnownAttribute.PreserveSig => true,
		        KnownAttribute.MethodImpl => true,
		        KnownAttribute.FieldOffset => true,
		        KnownAttribute.NonSerialized => true,
		        KnownAttribute.MarshalAs => true,
		        KnownAttribute.PermissionSet => true,
		        KnownAttribute.Optional => true,
		        KnownAttribute.In => true,
		        KnownAttribute.Out => true,
		        KnownAttribute.IndexerName => true,
		        _ => false
	        };
        }

        IEnumerable<IEnumerable<ISymbol>> HandleBuiltinAttribute(KnownAttribute attribute, AnalyzerScope scope)
        {
            IEnumerable<ISymbol> ScanTypes(DecompilerTypeSystem ts)
            {
                return ts.MainModule.TypeDefinitions
                    .Where(t => t.HasAttribute(attribute));
            }

            IEnumerable<ISymbol> ScanMethods(DecompilerTypeSystem ts)
            {
                return ts.MainModule.TypeDefinitions
                    .SelectMany(t => t.Members.OfType<IMethod>())
                    .Where(m => m.HasAttribute(attribute))
                    .Select(m => m.AccessorOwner ?? m);
            }

            IEnumerable<ISymbol> ScanFields(DecompilerTypeSystem ts)
            {
                return ts.MainModule.TypeDefinitions
                    .SelectMany(t => t.Fields)
                    .Where(f => f.HasAttribute(attribute));
            }

            IEnumerable<ISymbol> ScanProperties(DecompilerTypeSystem ts)
            {
                return ts.MainModule.TypeDefinitions
                    .SelectMany(t => t.Properties)
                    .Where(p => p.HasAttribute(attribute));
            }

            IEnumerable<ISymbol> ScanParameters(DecompilerTypeSystem ts)
            {
                return ts.MainModule.TypeDefinitions
                    .SelectMany(t => t.Members.OfType<IMethod>())
                    .Where(m => m.Parameters.Any(p => p.HasAttribute(attribute)))
                    .Select(m => m.AccessorOwner ?? m);
            }

            foreach (Decompiler.Metadata.MetadataFile  module in scope.GetAllModules())
            {
                var ts = new DecompilerTypeSystem(module, ((PEFile)module).GetAssemblyResolver());

                switch (attribute)
                {
                    case KnownAttribute.Serializable:
                    case KnownAttribute.ComImport:
                    case KnownAttribute.StructLayout:
                        yield return ScanTypes(ts);
                        break;
                    case KnownAttribute.DllImport:
                    case KnownAttribute.PreserveSig:
                    case KnownAttribute.MethodImpl:
                        yield return ScanMethods(ts);
                        break;
                    case KnownAttribute.FieldOffset:
                    case KnownAttribute.NonSerialized:
                        yield return ScanFields(ts);
                        break;
                    case KnownAttribute.MarshalAs:
                        yield return ScanFields(ts);
                        yield return ScanParameters(ts);
                        goto case KnownAttribute.Out;
                    case KnownAttribute.Optional:
                    case KnownAttribute.In:
                    case KnownAttribute.Out:
                        yield return ScanParameters(ts);
                        break;
                    case KnownAttribute.IndexerName:
                        yield return ScanProperties(ts);
                        break;
                }
            }
        }

        IEnumerable<ISymbol> HandleCustomAttribute(ITypeDefinition attributeType, AnalyzerScope scope)
        {
			var genericContext = new GenericContext(); // type arguments do not matter for this analyzer.

			foreach (var module in scope.GetAllModules()) {
				var ts = new DecompilerTypeSystem(module, module.GetAssemblyResolver());
				var referencedParameters = new HashSet<ParameterHandle>();
				foreach (var customAttribute in from h in module.Metadata.CustomAttributes select module.Metadata.GetCustomAttribute(h) into customAttribute let attributeCtor = ts.MainModule.ResolveMethod(customAttribute.Constructor, genericContext) where attributeCtor.DeclaringTypeDefinition != null
					         && attributeCtor.ParentModule?.MetadataFile == attributeType.ParentModule?.MetadataFile
					         && attributeCtor.DeclaringTypeDefinition.MetadataToken == attributeType.MetadataToken select customAttribute)
				{
					if (customAttribute.Parent.Kind == HandleKind.Parameter) {
						referencedParameters.Add((ParameterHandle)customAttribute.Parent);
					} else {
						var parent = GetParentEntity(ts, customAttribute);
						if (parent != null)
							yield return parent;
					}
				}

				if (referencedParameters.Count <= 0) continue;
				foreach (var method in from h in module.Metadata.MethodDefinitions let md = module.Metadata.GetMethodDefinition(h) where md.GetParameters().Any(p => referencedParameters.Contains(p)) select ts.MainModule.ResolveMethod(h, genericContext) into method where method != null select method)
				{
					if (method.IsAccessor)
						yield return method.AccessorOwner;
					else
						yield return method;
				}
			}
		}

		ISymbol GetParentEntity(DecompilerTypeSystem ts, CustomAttribute customAttribute)
		{
			var metadata = ts.MainModule.MetadataFile?.Metadata;
			switch (customAttribute.Parent.Kind) {
				case HandleKind.MethodDefinition:
				case HandleKind.FieldDefinition:
				case HandleKind.PropertyDefinition:
				case HandleKind.EventDefinition:
				case HandleKind.TypeDefinition:
					return ts.MainModule.ResolveEntity(customAttribute.Parent);
				case HandleKind.AssemblyDefinition:
				case HandleKind.ModuleDefinition:
					return ts.MainModule;
				case HandleKind.GenericParameterConstraint:
					var gpc = metadata.GetGenericParameterConstraint((GenericParameterConstraintHandle)customAttribute.Parent);
					var gp = metadata.GetGenericParameter(gpc.Parameter);
					return ts.MainModule.ResolveEntity(gp.Parent);
				case HandleKind.GenericParameter:
					gp = metadata.GetGenericParameter((GenericParameterHandle)customAttribute.Parent);
					return ts.MainModule.ResolveEntity(gp.Parent);
				default:
					return null;
			}
		}

		public bool Show(ISymbol symbol)
		{
			return symbol is ITypeDefinition type && type.GetNonInterfaceBaseTypes()
				.Any(t => t.IsKnownType(KnownTypeCode.Attribute));
		}
	}
}
