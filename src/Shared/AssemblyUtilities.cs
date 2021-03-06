﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains common reflection tasks
    /// </summary>
    internal static class AssemblyUtilities
    {
        // True when the cached method info objects have been set.
        private static bool s_initialized;

        // Cached method info
        private static MethodInfo s_assemblyNameCloneMethod;
        private static PropertyInfo s_assemblylocationProperty;
        private static MethodInfo s_cultureInfoGetCultureMethod;

#if !FEATURE_CULTUREINFO_GETCULTURES
        private static Lazy<CultureInfo[]> s_validCultures = new Lazy<CultureInfo[]>(() => GetValidCultures(), true);
#endif

        public static string GetAssemblyLocation(Assembly assembly)
        {
#if FEATURE_ASSEMBLY_LOCATION
            return assembly.Location;
#else
            // Assembly.Location is only available in .netstandard1.5, but MSBuild needs to target 1.3.
            // use reflection to access the property
            Initialize();

            if (s_assemblylocationProperty == null)
            {
                throw new NotSupportedException("Type Assembly does not have the Location property");
            }

            return (string)s_assemblylocationProperty.GetValue(assembly);
#endif
        }

#if CLR2COMPATIBILITY
        /// <summary>
        /// Shim for the lack of <see cref="System.Reflection.IntrospectionExtensions.GetTypeInfo"/> in .NET 3.5.
        /// </summary>
        public static Type GetTypeInfo(this Type t)
        {
            return t;
        }
#endif

        public static AssemblyName CloneIfPossible(this AssemblyName assemblyNameToClone)
        {
#if FEATURE_ASSEMBLYNAME_CLONE
            return (AssemblyName) assemblyNameToClone.Clone();
#else

            Initialize();

            if (s_assemblyNameCloneMethod == null)
            {
                return new AssemblyName(assemblyNameToClone.FullName);
            }

            // Try to Invoke the Clone method via reflection. If the method exists (it will on .NET
            // Core 2.0 or later) use that result, otherwise use new AssemblyName(FullName).
            return (AssemblyName) s_assemblyNameCloneMethod.Invoke(assemblyNameToClone, null) ??
                   new AssemblyName(assemblyNameToClone.FullName);
#endif
        }

        public static bool CultureInfoHasGetCultures()
        {
            return s_cultureInfoGetCultureMethod != null;
        }

        public static CultureInfo[] GetAllCultures()
        {
#if FEATURE_CULTUREINFO_GETCULTURES
            return CultureInfo.GetCultures(CultureTypes.AllCultures);
#else
            Initialize();

            if (!CultureInfoHasGetCultures())
            {
                throw new NotSupportedException("CultureInfo does not have the method GetCultures");
            }

            return s_validCultures.Value;
#endif
        }

        /// <summary>
        /// Initialize static fields. Doesn't need to be thread safe.
        /// </summary>
        private static void Initialize()
        {
            if (s_initialized) return;

            s_assemblyNameCloneMethod = typeof(AssemblyName).GetMethod("Clone");
            s_assemblylocationProperty = typeof(Assembly).GetProperty("Location", typeof(string));
            s_cultureInfoGetCultureMethod = typeof(CultureInfo).GetMethod("GetCultures");

            s_initialized = true;
        }

#if !FEATURE_CULTUREINFO_GETCULTURES
        private static CultureInfo[] GetValidCultures()
        {
            var cultureTypesType = s_cultureInfoGetCultureMethod?.GetParameters().FirstOrDefault()?.ParameterType;

            ErrorUtilities.VerifyThrow(cultureTypesType != null &&
                                       cultureTypesType.Name == "CultureTypes" &&
                                       Enum.IsDefined(cultureTypesType, "AllCultures"),
                                       "GetCulture is expected to accept CultureTypes.AllCultures");

            var allCulturesEnumValue = Enum.Parse(cultureTypesType, "AllCultures", true);

            var cultures = s_cultureInfoGetCultureMethod.Invoke(null, new[] {allCulturesEnumValue}) as CultureInfo[];

            ErrorUtilities.VerifyThrowInternalNull(cultures, "CultureInfo.GetCultures should work if all reflection checks pass");

            return cultures;
        }
#endif
    }
}
