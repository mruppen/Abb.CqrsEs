using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abb.CqrsEs.Internal
{
    internal static class DynamicMethodInvoker
    {
        private const BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly object s_lock = new object();
        private static volatile Dictionary<int, CompiledMethodInfo?> s_cachedMembers = new Dictionary<int, CompiledMethodInfo?>();

        internal static object? Invoke<T>(this T obj, string methodName, params object[] args)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var type = obj.GetType();
            var hash = Hash(type, methodName, args);
            var exists = s_cachedMembers.TryGetValue(hash, out var method);
            if (exists)
            {
                return method?.Invoke(obj, args);
            }

            lock (s_lock)
            {
                //Recheck if exist inside lock in case another thread has added it.
                exists = s_cachedMembers.TryGetValue(hash, out method);
                var dict = new Dictionary<int, CompiledMethodInfo?>(s_cachedMembers);
                if (exists)
                {
                    return method?.Invoke(obj, args);
                }

                var argtypes = GetArgTypes(args);
                var m = GetMember(type, methodName, argtypes);
                method = m == null ? null : new CompiledMethodInfo(m, type);

                dict.Add(hash, method);
                s_cachedMembers = dict;
                return method?.Invoke(obj, args);
            }
        }

        private static Type[] GetArgTypes(object[] args)
        {
            var argtypes = new Type[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var argtype = args[i].GetType();
                argtypes[i] = argtype;
            }
            return argtypes;
        }

        private static MethodInfo? GetMember(Type type, string name, Type[] argtypes)
        {
            while (true)
            {
                var methods = type.GetMethods(_bindingFlags).Where(m => m.Name == name).ToArray();
                var member = methods.FirstOrDefault(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(argtypes)) ??
                             methods.FirstOrDefault(m => m.GetParameters().Select(p => p.ParameterType).ToArray().Matches(argtypes));

                if (member != null)
                {
                    return member;
                }

                var t = type.GetTypeInfo().BaseType;
                if (t == null)
                {
                    return null;
                }

                type = t;
            }
        }

        private static int Hash(Type type, string methodname, object[] args)
        {
            var hash = 23;
            hash = hash * 31 + type.GetHashCode();
            hash = hash * 31 + methodname.GetHashCode();
            for (var index = 0; index < args.Length; index++)
            {
                var argtype = args[index].GetType();
                hash = hash * 31 + argtype.GetHashCode();
            }
            return hash;
        }

        private static bool Matches(this Type[] arr, Type[] args)
        {
            if (arr.Length != args.Length)
            {
                return false;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (!arr[i].IsAssignableFrom(args[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}