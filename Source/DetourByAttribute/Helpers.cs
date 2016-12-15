﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HugsLib.Core;
using Verse;

namespace HugsLib.DetourByAttribute
{
    public static class Helpers
    {
        internal static BindingFlags AllBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                       BindingFlags.NonPublic;

        private static Dictionary<MethodInfo, MethodInfo> detours = new Dictionary<MethodInfo, MethodInfo>();

        internal static void DoDetours()
        {
            // get all loaded types
            var allTypes = Utils.HugsLibUtility.GetAllActiveAssemblies()
                .SelectMany( a => a.GetTypes() );

            // loop over all methods with the detour attribute set
            foreach ( MethodInfo source in allTypes
                .SelectMany( t => t.GetMethods( AllBindingFlags ) )
                .Where( m => m.HasAttribute<DetourMethodAttribute>() ) )
            {
                DetourMethodAttribute detourAttribute = source.GetCustomAttributes( typeof( DetourMethodAttribute ), false ).First() as DetourMethodAttribute;
                HandleDetour( detourAttribute, source );
            }
            
            // loop over all properties with the detour attribute set
            foreach ( PropertyInfo source in allTypes
                .SelectMany( t => t.GetProperties( AllBindingFlags ) )
                .Where( m => m.HasAttribute<DetourPropertyAttribute>() ) )
            {
                DetourPropertyAttribute detourAttribute = source.GetCustomAttributes( typeof( DetourPropertyAttribute ), false ).First() as DetourPropertyAttribute;
                HandleDetour( detourAttribute, source );
            }
        }

        private static void HandleDetour( DetourMethodAttribute sourceAttribute, MethodInfo targetInfo )
        {
            // we need to get the method info of the source (usually, vanilla) method. 
            // if it was specified in the attribute, this is easy. Otherwise, we'll have to do some digging.
            MethodInfo sourceInfo = sourceAttribute.WasSetByMethodInfo
                                        ? sourceAttribute.sourceMethodInfo
                                        : GetMatchingMethodInfo( sourceAttribute, targetInfo );

            // make sure we've got what we wanted.
            if ( sourceInfo == null )
                throw new NullReferenceException( "sourceMethodInfo could not be found based on attribute" );
            if ( targetInfo == null )
                throw new ArgumentNullException( nameof( targetInfo ) );

            // call the actual detour
            DetourProvider.TryCompatibleDetour( sourceInfo, targetInfo );
        }

        private static MethodInfo GetMatchingMethodInfo( DetourMethodAttribute sourceAttribute, MethodInfo targetInfo )
        {
            // we should only ever get here in case the attribute was not defined with a sourceMethodInfo, but let's check just in case.
            if ( sourceAttribute.WasSetByMethodInfo )
                return sourceAttribute.sourceMethodInfo;

            // aight, let's search by name
            MethodInfo[] candidates =
                sourceAttribute.sourceType.GetMethods( AllBindingFlags )
                               .Where( mi => mi.Name == sourceAttribute.sourceMethodName ).ToArray();

            // if we only get one result, we've got our method info - if the length is zero, the method doesn't exist.
            if ( candidates.Length == 0 )
                return null;
            if ( candidates.Length == 1 )
                return candidates.First();

            // this is where things get slightly complicated, we'll have to search by parameters.
            candidates = candidates.Where( mi =>
                                           mi.ReturnType == targetInfo.ReturnType &&
                                           mi.GetParameters()
                                             .Select( pi => pi.ParameterType )
                                             .SequenceEqual( targetInfo.GetParameters().Select( pi => pi.ParameterType ) ) )
                                   .ToArray();

            // if we only get one result, we've got our method info - if the length is zero, the method doesn't exist.
            if ( candidates.Length == 0 )
                return null;
            if ( candidates.Length == 1 )
                return candidates.First();

            // if we haven't returned anything by this point there were still multiple candidates. This is theoretically impossible,
            // unless I missed something.
            return null;
        }

        private static void HandleDetour( DetourPropertyAttribute sourceAttribute, PropertyInfo targetInfo )
        {
            // first, lets get the source propertyInfo - there's no ambiguity here.
            PropertyInfo sourceInfo = sourceAttribute.sourcePropertyInfo;

            // do our detours
            // if getter was flagged (so Getter | Both )
            if ( ( sourceAttribute.detourProperty & DetourProperty.Getter ) == DetourProperty.Getter )
                DetourProvider.TryCompatibleDetour( sourceInfo.GetGetMethod( true ), targetInfo.GetGetMethod( true ) );

            // if setter was flagged
            if ( ( sourceAttribute.detourProperty & DetourProperty.Setter ) == DetourProperty.Setter )
                DetourProvider.TryCompatibleDetour( sourceInfo.GetSetMethod( true ), targetInfo.GetSetMethod( true ) );
        }

        internal static string FullName( this MethodInfo methodInfo )
        {
            return methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
        }
    }
}
