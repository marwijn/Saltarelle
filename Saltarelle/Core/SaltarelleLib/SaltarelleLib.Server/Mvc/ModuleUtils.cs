﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using dotless.Core;
using dotless.Core.configuration;
using System.Collections.ObjectModel;
using System.IO;

namespace Saltarelle.Mvc {
	public static class ModuleUtils {
		private static object scriptLock = new object();
		private static object cssLock    = new object();

		private static Dictionary<Assembly, ReadOnlyCollection<Assembly>> assemblyDependencies = new Dictionary<Assembly, ReadOnlyCollection<Assembly>>();
		private static Dictionary<Assembly, string> debugAssemblyScripts = new Dictionary<Assembly, string>();
		private static Dictionary<Assembly, string> releaseAssemblyScripts = new Dictionary<Assembly, string>();
		private static Dictionary<Assembly, string> assemblyCss = new Dictionary<Assembly, string>();
		private static IList<Assembly> GetDependencies(Assembly asm) {
			lock (scriptLock) {
				ReadOnlyCollection<Assembly> result;
				if (!assemblyDependencies.TryGetValue(asm, out result)) {
					var l = new List<Assembly>();
					foreach (var refName in asm.GetReferencedAssemblies()) {
						Assembly refAsm = Assembly.Load(refName);
						if (!debugAssemblyScripts.ContainsKey(refAsm)) {
							debugAssemblyScripts[refAsm] = GetAssemblyScriptAssumingLock(refAsm, true);
							releaseAssemblyScripts[refAsm] = GetAssemblyScriptAssumingLock(refAsm, false);
						}
						if (debugAssemblyScripts[refAsm] != null)
							l.Add(refAsm);
					}
					assemblyDependencies[asm] = result = l.AsReadOnly();
				}
				return result;
			}
		}
		
		private static string GetAssemblyScriptAssumingLock(Assembly asm, bool debug) {
			string scriptName = asm.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(debug ? "Script.js" : "Script.min.js"));
			if (scriptName != null) {
				using (var strm = asm.GetManifestResourceStream(scriptName))
				using (var rdr = new StreamReader(strm)) {
					return rdr.ReadToEnd();
				}
			}
			return null;
		}

		public static string GetAssemblyScriptContent(Assembly asm, bool debug) {
			string s;
			lock (scriptLock) {
				var x = (debug ? debugAssemblyScripts : releaseAssemblyScripts);
				if (!x.TryGetValue(asm, out s)) {
					x[asm] = s = GetAssemblyScriptAssumingLock(asm, debug);
				}
			}
			return s;
		}

		private static string GetLessVariableDefinitions(Assembly asm) {
			var url = GlobalServices.Provider.GetService<IUrlService>();
			return string.Join(Environment.NewLine,
			                   asm.GetCustomAttributes(typeof(CssResourceAttribute), false)
			                      .Cast<CssResourceAttribute>()
			                      .Select(a => "@" + a.Name + ": url(" + url.GetAssemblyResourceUrl(asm, a.PublicResourceName) + ");")
			           .Concat(asm.GetCustomAttributes(typeof(ImportCssResourceAttribute), false)
			                      .Cast<ImportCssResourceAttribute>()
			                      .Select(a => "@" + a.CssVariableName + ": url(" + url.GetAssemblyResourceUrl(a.ResourceAssembly, a.PublicResourceName) + ");"))
			           .ToArray());
		}
		
		private static string GetAssemblyCssAssumingLock(Assembly asm) {
			string resName = asm.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith("Module.less"));
			if (resName != null) {
				using (var strm = asm.GetManifestResourceStream(resName))
				using (var rdr = new StreamReader(strm)) {
					var engine = new EngineFactory().GetEngine(DotlessConfiguration.Default);
					return engine.TransformToCss(new StringSource().GetSource(GetLessVariableDefinitions(asm) + Environment.NewLine + rdr.ReadToEnd()));
				}
			}
			return null;
		}
		
		public static string GetAssemblyCss(Assembly assembly) {
			lock (cssLock) {
				string s;
				if (!assemblyCss.TryGetValue(assembly, out s)) {
					assemblyCss[assembly] = s = GetAssemblyCssAssumingLock(assembly);
				}
				return s;
			}
		}
		
		public static string GetCss(string lessSource, Assembly contextAssembly) {
			var engine = new EngineFactory().GetEngine(DotlessConfiguration.Default);
			return engine.TransformToCss(new StringSource().GetSource((contextAssembly != null ? GetLessVariableDefinitions(contextAssembly) + Environment.NewLine : "") + lessSource));
		}
		
		private static void AddAssembliesInCorrectOrder(Assembly asm, IList<Assembly> l) {
			if (!l.Contains(asm)) {
				// add all references (recursively) before adding the current one
				foreach (var dep in ModuleUtils.GetDependencies(asm))
					AddAssembliesInCorrectOrder(dep, l);
				if (ModuleUtils.GetAssemblyScriptContent(asm, true) != null)
					l.Add(asm);
			}
		}
		
		public static IList<Assembly> TopologicalSortAssembliesWithDependencies(IEnumerable<Assembly> list) {
			IList<Assembly> result = new List<Assembly>();
			foreach (Assembly a in list)
				AddAssembliesInCorrectOrder(a, result);
			return result;
		}
	}
}