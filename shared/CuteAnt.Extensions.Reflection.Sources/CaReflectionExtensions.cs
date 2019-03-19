using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

namespace System.Reflection
{
  #region --- class BindingFlagsHelper ---

  /// <summary>BindingFlagsHelper</summary>
  internal static class BindingFlagsHelper
  {
    #region -- MS --

    /// <summary>MSRuntimeLookup - from ReferenceSource\mscorlib\system\type.cs</summary>
    internal const BindingFlags MSDefaultLookup = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    /// <summary>MSRuntimeLookup - from ReferenceSource\mscorlib\system\type.cs</summary>
    internal const BindingFlags MSDeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>MSRuntimeLookup - from ReferenceSource\mscorlib\system\reflection\RuntimeReflectionExtensions.cs</summary>
    internal const BindingFlags MSRuntimeLookup = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    #endregion

    #region -- Static --

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static</summary>
    internal const BindingFlags StaticDeclaredAndNonPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticDeclaredAndNonPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static</summary>
    internal const BindingFlags StaticDeclaredAndPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticDeclaredAndPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static</summary>
    internal const BindingFlags StaticDeclaredOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticDeclaredOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;


    /// <summary>BindingFlags.NonPublic | BindingFlags.Static</summary>
    internal const BindingFlags StaticNonPublicOnlyLookup = BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticNonPublicOnlyLookupIC = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.Static</summary>
    internal const BindingFlags StaticPublicOnlyLookup = BindingFlags.Public | BindingFlags.Static;

    /// <summary>BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticPublicOnlyLookupIC = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static</summary>
    internal const BindingFlags StaticLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticLookupIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;


    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static</summary>
    internal const BindingFlags StaticPublicOnlyLookupAll = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticPublicOnlyLookupAllIC = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static</summary>
    internal const BindingFlags StaticLookupAll = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags StaticLookupAllIC = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

    #endregion

    #region -- Instance --

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance</summary>
    internal const BindingFlags InstanceDeclaredAndNonPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstanceDeclaredAndNonPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance</summary>
    internal const BindingFlags InstanceDeclaredAndPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstanceDeclaredAndPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance</summary>
    internal const BindingFlags InstanceDeclaredOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstanceDeclaredOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;


    /// <summary>BindingFlags.NonPublic | BindingFlags.Instance</summary>
    internal const BindingFlags InstanceNonPublicOnlyLookup = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstanceNonPublicOnlyLookupIC = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.Instance</summary>
    internal const BindingFlags InstancePublicOnlyLookup = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstancePublicOnlyLookupIC = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance</summary>
    internal const BindingFlags InstanceLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags InstanceLookupIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;

    #endregion

    #region -- Default --

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static</summary>
    internal const BindingFlags DefaultDeclaredAndNonPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultDeclaredAndNonPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static</summary>
    internal const BindingFlags DefaultDeclaredAndPublicOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultDeclaredAndPublicOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static</summary>
    internal const BindingFlags DefaultDeclaredOnlyLookup = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultDeclaredOnlyLookupIC = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;


    /// <summary>BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance</summary>
    internal const BindingFlags DefaultNonPublicOnlyLookup = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultNonPublicOnlyLookupIC = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance</summary>
    internal const BindingFlags DefaultPublicOnlyLookup = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultPublicOnlyLookupIC = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance</summary>
    internal const BindingFlags DefaultLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultLookupIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;


    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance</summary>
    internal const BindingFlags DefaultPublicOnlyLookupAll = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultPublicOnlyLookupAllIC = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance</summary>
    internal const BindingFlags DefaultLookupAll = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase</summary>
    internal const BindingFlags DefaultLookupAllIC = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

    #endregion
  }

  #endregion

  internal static class CaReflectionExtensions
  {
    /// <summary>Value for lining method</summary>
    private const MethodImplOptions AggressiveInlining = (MethodImplOptions)256;

    #region -- Type --

    public static PropertyInfo GetTypeProperty(this Type type, string name)
    {
      return type.GetProperty(name, BindingFlagsHelper.MSRuntimeLookup);
    }
    public static EventInfo GetTypeEvent(this Type type, string name)
    {
      return type.GetEvent(name, BindingFlagsHelper.MSRuntimeLookup);
    }
    public static MethodInfo GetTypeMethod(this Type type, string name, Type[] parameters)
    {
      return type.GetMethod(name, BindingFlagsHelper.MSRuntimeLookup, null, parameters, null);
    }
    public static FieldInfo GetTypeField(this Type type, string name)
    {
      return type.GetField(name, BindingFlagsHelper.MSRuntimeLookup);
    }

    public static EventInfo GetDeclaredEvent(this Type type, string name)
    {
#if NET40
      return type.GetEvent(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
#else
      return type.GetTypeInfo().GetDeclaredEvent(name);
#endif
    }
    public static FieldInfo GetDeclaredField(this Type type, string name)
    {
#if NET40
      return type.GetField(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
#else
      return type.GetTypeInfo().GetDeclaredField(name);
#endif
    }
    public static MethodInfo GetDeclaredMethod(this Type type, string name)
    {
#if NET40
      return type.GetMethod(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
#else
      return type.GetTypeInfo().GetDeclaredMethod(name);
#endif
    }
    public static PropertyInfo GetDeclaredProperty(this Type type, string name)
    {
#if NET40
      return type.GetProperty(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
#else
      return type.GetTypeInfo().GetDeclaredProperty(name);
#endif
    }

    #region - GetTypeCode -

    /// <summary>获取类型代码</summary>
    /// <param name="type"></param>
    /// <returns></returns>
    [MethodImpl(AggressiveInlining)]
    public static TypeCode GetTypeCode(this Type type) => Type.GetTypeCode(type);

    #endregion

    #region - ImplementInterface -

    public static bool ImplementInterface(this Type type, Type interfaceType)
    {
      for (var currentType = type; currentType != null; currentType = currentType.BaseType)
      {
#if NET40
        var interfaces = currentType.GetInterfaces();
#else
        var interfaces = currentType.GetTypeInfo().ImplementedInterfaces;
#endif
        if (interfaces != null)
        {
          foreach (var t in interfaces)
          {
            if (t == interfaceType || (t != null && t.ImplementInterface(interfaceType)))
            {
              return true;
            }
          }
        }
      }

      return false;
    }

    #endregion

    #region - IsConstructedGenericType -

#if NET40
    /// <summary>Wraps input type into <see cref="TypeInfo"/> structure.</summary>
    /// <param name="type">Input type.</param> <returns>Type info wrapper.</returns>
    public static TypeInfo GetTypeInfo(this Type type)
    {
      return TypeInfo.FromType(type);
    }
#endif

    [MethodImpl(AggressiveInlining)]
    public static bool IsConstructedGenericType(this Type t)
    {
#if NET40
      return t.IsGenericType && !t.IsGenericTypeDefinition;
#else
      return t.IsConstructedGenericType;
#endif
    }

    #endregion

    #region - IsNull -

    /// <summary>IsNull.</summary>
    /// <param name="typeInfo">Input type.</param> <returns>Type info wrapper.</returns>
    [MethodImpl(AggressiveInlining)]
    public static bool IsNull(this TypeInfo typeInfo)
    {
#if NET40
      return null == typeInfo.AsType();
#else
      return null == typeInfo;
#endif
    }

    #endregion

    #region - IsNullableType -

    [MethodImpl(AggressiveInlining)]
    public static bool IsNullableType(this Type type)
    {
      return type.IsConstructedGenericType() && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    #endregion

    #region - IsInstanceOf -

    public static bool IsInstanceOf(this Type type, Type thisOrBaseType)
    {
      while (type != null)
      {
        if (type == thisOrBaseType) { return true; }

        type = type.BaseType;
      }
      return false;
    }

    #endregion

    #region - HasGenericType -

    public static bool HasGenericType(this Type type)
    {
      while (type != null)
      {
        if (type.IsGenericType) { return true; }

        type = type.BaseType;
      }
      return false;
    }

    #endregion

    #region - FirstGenericType -

    public static Type FirstGenericType(this Type type)
    {
      while (type != null)
      {
        if (type.IsGenericType) { return type; }

        type = type.BaseType;
      }
      return null;
    }

    #endregion

    #region - GetTypeWithGenericTypeDefinitionOfAny -

    public static Type GetTypeWithGenericTypeDefinitionOfAny(this Type type, params Type[] genericTypeDefinitions)
    {
      foreach (var genericTypeDefinition in genericTypeDefinitions)
      {
        var genericType = type.GetTypeWithGenericTypeDefinitionOf(genericTypeDefinition);
        if (genericType == null && type == genericTypeDefinition)
        {
          genericType = type;
        }

        if (genericType != null) { return genericType; }
      }
      return null;
    }

    #endregion

    #region - IsOrHasGenericInterfaceTypeOf -

    public static bool IsOrHasGenericInterfaceTypeOf(this Type type, Type genericTypeDefinition)
    {
      return (type.GetTypeWithGenericTypeDefinitionOf(genericTypeDefinition) != null)
          || (type == genericTypeDefinition);
    }

    #endregion

    #region - GetTypeWithGenericTypeDefinitionOf -

    public static Type GetTypeWithGenericTypeDefinitionOf(this Type type, Type genericTypeDefinition)
    {
      foreach (var t in type.GetInterfaces())
      {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == genericTypeDefinition)
        {
          return t;
        }
      }

      var genericType = type.FirstGenericType();
      if (genericType != null && genericType.GetGenericTypeDefinition() == genericTypeDefinition)
      {
        return genericType;
      }

      return null;
    }

    #endregion

    #region - GetTypeWithInterfaceOf -

    public static Type GetTypeWithInterfaceOf(this Type type, Type interfaceType)
    {
      if (type == interfaceType) return interfaceType;

      foreach (var t in type.GetInterfaces())
      {
        if (t == interfaceType) { return t; }
      }

      return null;
    }

    #endregion

    #region - HasInterface -

    public static bool HasInterface(this Type type, Type interfaceType)
    {
      foreach (var t in type.GetInterfaces())
      {
        if (t == interfaceType) { return true; }
      }
      return false;
    }

    #endregion

    #region - AllHaveInterfacesOfType -

    public static bool AllHaveInterfacesOfType(this Type assignableFromType, params Type[] types)
    {
      foreach (var type in types)
      {
        if (assignableFromType.GetTypeWithInterfaceOf(type) == null) return false;
      }
      return true;
    }

    #endregion

    #region - GetUnderlyingTypeCode -

    [MethodImpl(AggressiveInlining)]
    public static TypeCode GetUnderlyingTypeCode(this Type type)
    {
      return Type.GetTypeCode(Nullable.GetUnderlyingType(type) ?? type);
    }

    #endregion

    #region - GetTypeWithGenericInterfaceOf -

    public static Type GetTypeWithGenericInterfaceOf(this Type type, Type genericInterfaceType)
    {
      foreach (var t in type.GetInterfaces())
      {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == genericInterfaceType) { return t; }
      }

      if (!type.IsGenericType) return null;

      var genericType = type.FirstGenericType();
      return genericType.GetGenericTypeDefinition() == genericInterfaceType
              ? genericType
              : null;
    }

    #endregion

    #endregion

    #region -- MemberInfo --

    /// <summary>获取包含该成员的自定义特性的集合。</summary>
    /// <param name="member"></param>
    /// <returns></returns>
    [MethodImpl(AggressiveInlining)]
    public static IEnumerable<CustomAttributeData> CustomAttributesEx(this MemberInfo member)
    {
#if !NET40
      return member.CustomAttributes;
#else
      return member.GetCustomAttributesData();
#endif
    }

    #endregion

    #region -- MethodInfo --

#if NET40
    /// <summary>创建指定类型的委托从此方法的</summary>
    /// <param name="method">MethodInfo</param>
    /// <param name="delegateType">创建委托的类型</param>
    /// <returns></returns>
    public static Delegate CreateDelegate(this MethodInfo method, Type delegateType)
    {
      return Delegate.CreateDelegate(delegateType, method);
    }

    /// <summary>使用从此方法的指定目标创建指定类型的委托</summary>
    /// <param name="method">MethodInfo</param>
    /// <param name="delegateType">创建委托的类型</param>
    /// <param name="target">委托面向的对象</param>
    /// <returns></returns>
    public static Delegate CreateDelegate(this MethodInfo method, Type delegateType, Object target)
    {
      return Delegate.CreateDelegate(delegateType, target, method);
    }
#endif

    #endregion

    #region -- CustomAttributeData --

    /// <summary>获取属性的类型。</summary>
    /// <param name="attrdata"></param>
    /// <returns></returns>
    [MethodImpl(AggressiveInlining)]
    public static Type AttributeTypeEx(this CustomAttributeData attrdata)
    {
#if !NET40
      return attrdata.AttributeType;
#else
      return attrdata.Constructor.DeclaringType;
#endif
    }

    #endregion

    #region -- PropertyInfo --

#if NET40
    /// <summary>返回指定对象的属性值</summary>
    /// <param name="property"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static Object GetValue(this PropertyInfo property, Object obj)
    {
      return property.GetValue(obj, null);
    }

    /// <summary>设置指定对象的属性值</summary>
    /// <param name="property"></param>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public static void SetValue(this PropertyInfo property, Object obj, Object value)
    {
      property.SetValue(obj, value, null);
    }
#endif

    /// <summary>获取此属性的 get 访问器。</summary>
    /// <param name="property"></param>
    /// <returns></returns>
    [MethodImpl(AggressiveInlining)]
    public static MethodInfo GetMethod(this PropertyInfo property)
    {
#if !NET40
      return property.GetMethod;
#else
      return property.GetGetMethod(true);
#endif
    }

    /// <summary>获取此属性的 set 访问器。</summary>
    /// <param name="property"></param>
    /// <returns></returns>
    [MethodImpl(AggressiveInlining)]
    public static MethodInfo SetMethod(this PropertyInfo property)
    {
#if !NET40
      return property.SetMethod;
#else
      return property.GetSetMethod(true);
#endif
    }

    #endregion

    #region -- ParameterInfo --

    [MethodImpl(AggressiveInlining)]
    public static bool HasDefaultValue(this ParameterInfo pi)
    {
#if NET40
      const string _DBNullType = "System.DBNull";
      var defaultValue = pi.DefaultValue;
      if (null == defaultValue && pi.ParameterType.IsValueType)
      {
        defaultValue = Activator.CreateInstance(pi.ParameterType);
      }
      return null == defaultValue || !string.Equals(_DBNullType, defaultValue.GetType().FullName, StringComparison.Ordinal);
#else
      return pi.HasDefaultValue;
#endif
    }

    #endregion
  }

  internal static class CaPlatformExtensions
  {
    /// <summary>Value for lining method</summary>
    private const MethodImplOptions AggressiveInlining = (MethodImplOptions)256;

    #region -- GenericTypeArguments --

#if NET40
    [MethodImpl(AggressiveInlining)]
    public static Type[] GenericTypeArguments(this Type type)
    {
      return type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
    }
#endif

    #endregion

    #region -- GenericTypeParameters --

    [MethodImpl(AggressiveInlining)]
    public static Type[] GenericTypeParameters(this Type type)
    {
#if NET40
      return type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
#else
      return type.GetTypeInfo().GenericTypeParameters;
#endif
    }

    #endregion

    #region -- GetEmptyConstructor --

    [MethodImpl(AggressiveInlining)]
    public static ConstructorInfo GetEmptyConstructor(this Type type) => type.GetConstructor(Type.EmptyTypes);

    #endregion

    #region -- GetPublicMembers --

    [MethodImpl(AggressiveInlining)]
    public static MemberInfo[] GetPublicMembers(this Type type) => type.GetMembers(BindingFlagsHelper.InstancePublicOnlyLookup);

    #endregion

    #region -- GetAllPublicMembers --

    [MethodImpl(AggressiveInlining)]
    public static MemberInfo[] GetAllPublicMembers(this Type type) => type.GetMembers(BindingFlagsHelper.InstancePublicOnlyLookup);

    #endregion

    #region -- GetStaticMethod --

    [MethodImpl(AggressiveInlining)]
    public static MethodInfo GetStaticMethod(this Type type, string methodName) => type.GetMethod(methodName, BindingFlagsHelper.StaticLookup);

    [MethodImpl(AggressiveInlining)]
    public static MethodInfo GetStaticMethod(this Type type, string methodName, Type[] types = null)
    {
      return types == null
          ? type.GetMethod(methodName, BindingFlagsHelper.StaticPublicOnlyLookup)
          : type.GetMethod(methodName, BindingFlagsHelper.StaticPublicOnlyLookup, null, types, null);
    }

    #endregion

    #region -- GetInstanceMethod --

    [MethodImpl(AggressiveInlining)]
    public static MethodInfo GetInstanceMethod(this Type type, string methodName)
        => type.GetMethod(methodName, BindingFlagsHelper.InstanceLookup);

    #endregion

    #region -- GetImplementedInterfaces --

    [MethodImpl(AggressiveInlining)]
    public static IEnumerable<Type> GetImplementedInterfaces(this Type type)
    {
#if !NET40
      return type.GetTypeInfo().ImplementedInterfaces;
#else
      return type.GetInterfaces();
#endif
    }

    #endregion

    #region -- IsDynamic--

#if !NOTSUPPORT_EMIT_ASSEMBLYBUILDER
    public static bool IsDynamic(this Assembly assembly)
    {
      try
      {
        var isDyanmic = assembly is System.Reflection.Emit.AssemblyBuilder
            || string.IsNullOrEmpty(assembly.Location);
        return isDyanmic;
      }
      catch (NotSupportedException)
      {
        //Ignore assembly.Location not supported in a dynamic assembly.
        return true;
      }
    }
#endif

    #endregion

    #region -- InvokeMethod --

    [MethodImpl(AggressiveInlining)]
    public static object InvokeMethod(this Delegate fn, object instance, object[] parameters = null)
        => fn.Method.Invoke(instance, parameters ?? new object[] { });

    #endregion

    #region -- GetPublicStaticField --

    [MethodImpl(AggressiveInlining)]
    public static FieldInfo GetPublicStaticField(this Type type, string fieldName)
        => type.GetField(fieldName, BindingFlagsHelper.StaticPublicOnlyLookup);

    #endregion

    #region -- MakeDelegate --

    [MethodImpl(AggressiveInlining)]
    public static Delegate MakeDelegate(this MethodInfo mi, Type delegateType, bool throwOnBindFailure = true)
    {
#if !NET40
      return mi.CreateDelegate(delegateType);
#else
      return Delegate.CreateDelegate(delegateType, mi, throwOnBindFailure);
#endif
    }

    #endregion

    #region -- IsStandardClass --

    [MethodImpl(AggressiveInlining)]
    public static bool IsStandardClass(this Type type) => type.IsClass && !type.IsAbstract && !type.IsInterface;

    #endregion

    #region -- GetWritableFields --

    [MethodImpl(AggressiveInlining)]
    public static FieldInfo[] GetWritableFields(this Type type)
        => type.GetFields(BindingFlagsHelper.InstanceNonPublicOnlyLookup | BindingFlags.SetField);

    #endregion

    #region -- GetMethodInfo / PropertyGetMethod --

    [MethodImpl(AggressiveInlining)]
    public static MethodInfo GetMethodInfo(this Type type, string methodName, Type[] types = null)
    {
      return types == null
          ? type.GetMethod(methodName)
          : type.GetMethod(methodName, types);
    }

    #endregion

    #region -- IsUnderlyingEnum --

    [MethodImpl(AggressiveInlining)]
    public static bool IsUnderlyingEnum(this Type type) => type.IsEnum || type.UnderlyingSystemType.IsEnum;

    #endregion

    #region -- ElementType --

    [MethodImpl(AggressiveInlining)]
    public static Type ElementType(this Type type)
    {
#if PCL
      return type.GetTypeInfo().GetElementType();
#else
      return type.GetElementType();
#endif
    }

    #endregion

    #region -- GetCollectionType --

    [MethodImpl(AggressiveInlining)]
    public static Type GetCollectionType(this Type type)
    {
      return type.GetElementType()
          ?? type.GetGenericArguments().LastOrDefault(); //new[] { str }.Select(x => new Type()) => WhereSelectArrayIterator<string,Type>
    }

    #endregion
  }
}
