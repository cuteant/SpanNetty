#if NET40
using System;
using System.Collections.Generic;

namespace System.Reflection
{
  internal static class RuntimeReflectionExtensions
  {
    private static void CheckAndThrow(Type type)
    {
      if (type == null) throw new ArgumentNullException(nameof(type));
      //if (!(t is RuntimeType)) throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType"));
    }

    private static void CheckAndThrow(MethodInfo method)
    {
      if (method == null) throw new ArgumentNullException(nameof(method));
      //if (!(m is RuntimeMethodInfo)) throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeMethodInfo"));
    }

    public static IEnumerable<PropertyInfo> GetRuntimeProperties(this Type type)
    {
      CheckAndThrow(type);
      return type.GetProperties(BindingFlagsHelper.MSRuntimeLookup);
    }
    public static IEnumerable<EventInfo> GetRuntimeEvents(this Type type)
    {
      CheckAndThrow(type);
      return type.GetEvents(BindingFlagsHelper.MSRuntimeLookup);
    }

    public static IEnumerable<MethodInfo> GetRuntimeMethods(this Type type)
    {
      CheckAndThrow(type);
      return type.GetMethods(BindingFlagsHelper.MSRuntimeLookup);
    }

    public static IEnumerable<FieldInfo> GetRuntimeFields(this Type type)
    {
      CheckAndThrow(type);
      return type.GetFields(BindingFlagsHelper.MSRuntimeLookup);
    }

    public static PropertyInfo GetRuntimeProperty(this Type type, string name)
    {
      CheckAndThrow(type);
      return type.GetProperty(name);
    }
    public static EventInfo GetRuntimeEvent(this Type type, string name)
    {
      CheckAndThrow(type);
      return type.GetEvent(name);
    }
    public static MethodInfo GetRuntimeMethod(this Type type, string name, Type[] parameters)
    {
      CheckAndThrow(type);
      return type.GetMethod(name, parameters);
    }
    public static FieldInfo GetRuntimeField(this Type type, string name)
    {
      CheckAndThrow(type);
      return type.GetField(name);
    }
    public static MethodInfo GetRuntimeBaseDefinition(this MethodInfo method)
    {
      CheckAndThrow(method);
      return method.GetBaseDefinition();
    }

    internal static InterfaceMapping GetRuntimeInterfaceMap(this TypeInfo typeInfo, Type interfaceType)
    {
      var type = typeInfo.AsType();
      if (type == null) throw new ArgumentNullException(nameof(typeInfo));
      //if (!(typeInfo is RuntimeType)) throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType"));

      return type.GetInterfaceMap(interfaceType);
    }

    public static MethodInfo GetMethodInfo(this Delegate del)
    {
      if (del == null) throw new ArgumentNullException(nameof(del));

      return del.Method;
    }
  }
}
#endif