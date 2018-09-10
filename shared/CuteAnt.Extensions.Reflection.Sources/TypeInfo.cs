#if NET40
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace System.Reflection
{
  /// <summary>Partial analog of TypeInfo existing in .NET 4.5 and higher.</summary>
  internal class TypeInfo
  {
    #region @@ Fields @@

    private readonly Type _type;

    #endregion

    #region @@ Constructors @@

    /// <summary>Creates type info by wrapping input type.</summary> <param name="type">Type to wrap.</param>
    public TypeInfo(Type type) => _type = type;

    public static readonly ConcurrentDictionary<Type, TypeInfo> _typeInfoCache = new ConcurrentDictionary<Type, TypeInfo>();

    public static TypeInfo FromType(Type type) => _typeInfoCache.GetOrAdd(type, t => new TypeInfo(t));

    #endregion

#pragma warning disable 1591 // "Missing XML-comment"

    #region -- Properties --

    public Assembly Assembly => _type.Assembly;
    public string AssemblyQualifiedName => _type.AssemblyQualifiedName;
    public TypeAttributes Attributes => _type.Attributes;
    public Type BaseType => _type.BaseType;
    public bool ContainsGenericParameters => _type.ContainsGenericParameters;
    public IEnumerable<CustomAttributeData> CustomAttributes => _type.GetCustomAttributesData();
    public IEnumerable<ConstructorInfo> DeclaredConstructors => _type.GetConstructors(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<EventInfo> DeclaredEvents => _type.GetEvents(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<FieldInfo> DeclaredFields => _type.GetFields(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<MemberInfo> DeclaredMembers => _type.GetMembers(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<MethodInfo> DeclaredMethods => _type.GetMethods(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<System.Reflection.TypeInfo> DeclaredNestedTypes
    {
      get
      {
        foreach (var t in _type.GetNestedTypes(BindingFlagsHelper.MSDeclaredOnlyLookup))
        {
          yield return t.GetTypeInfo();
        }
      }
    }
    public IEnumerable<PropertyInfo> DeclaredProperties => _type.GetProperties(BindingFlagsHelper.MSDeclaredOnlyLookup);
    public MethodBase DeclaringMethod => _type.DeclaringMethod;
    public Type DeclaringType => _type.DeclaringType;
    public GenericParameterAttributes GenericParameterAttributes => _type.GenericParameterAttributes;
    public int GenericParameterPosition => _type.GenericParameterPosition;
    public Type[] GenericTypeParameters
    {
      get
      {
        if (_type.IsGenericTypeDefinition)
        {
          return _type.GetGenericArguments();
        }
        else
        {
          return Type.EmptyTypes;
        }
      }
    }

    public Type[] GenericTypeArguments
    {
      get
      {
        if (_type.IsGenericType && !_type.IsGenericTypeDefinition)
        {
          return _type.GetGenericArguments();
        }
        else
        {
          return Type.EmptyTypes;
        }
      }
    }
    public Guid GUID => _type.GUID;
    public bool HasElementType => _type.HasElementType;
    public IEnumerable<Type> ImplementedInterfaces => _type.GetInterfaces();
    public bool IsAbstract => _type.IsAbstract;
    public bool IsAnsiClass => _type.IsAnsiClass;
    public bool IsArray => _type.IsArray;
    public bool IsAutoClass => _type.IsAutoClass;
    public bool IsAutoLayout => _type.IsAutoLayout;
    public bool IsByRef => _type.IsByRef;
    public bool IsClass => _type.IsClass;
    public bool IsCOMObject => _type.IsCOMObject;
    public bool IsContextful => _type.IsContextful;
    public bool IsEnum => _type.IsEnum;
    public bool IsExplicitLayout => _type.IsExplicitLayout;
    public bool IsGenericParameter => _type.IsGenericParameter;
    public bool IsGenericType => _type.IsGenericType;
    public bool IsGenericTypeDefinition => _type.IsGenericTypeDefinition;
    public bool IsImport => _type.IsImport;
    public bool IsInterface => _type.IsInterface;
    public bool IsLayoutSequential => _type.IsLayoutSequential;
    public bool IsMarshalByRef => _type.IsMarshalByRef;
    public bool IsNested => _type.IsNested;
    public bool IsNestedAssembly => _type.IsNestedAssembly;
    public bool IsNestedFamANDAssem => _type.IsNestedFamANDAssem;
    public bool IsNestedFamily => _type.IsNestedFamily;
    public bool IsNestedFamORAssem => _type.IsNestedFamORAssem;
    public bool IsNestedPrivate => _type.IsNestedPrivate;
    public bool IsNestedPublic => _type.IsNestedPublic;
    public bool IsNotPublic => _type.IsNotPublic;
    public bool IsPointer => _type.IsPointer;
    public bool IsPrimitive => _type.IsPrimitive;
    public bool IsPublic => _type.IsPublic;
    public bool IsSealed => _type.IsSealed;
    public bool IsSecurityCritical => _type.IsSecurityCritical;
    public bool IsSecuritySafeCritical => _type.IsSecuritySafeCritical;
    public bool IsSerializable => _type.IsSerializable;
    public bool IsSpecialName => _type.IsSpecialName;
    public bool IsUnicodeClass => _type.IsUnicodeClass;
    public bool IsValueType => _type.IsValueType;
    public bool IsVisible => _type.IsVisible;
    public MemberTypes MemberType => _type.MemberType;
    public int MetadataToken => _type.MetadataToken;
    public Module Module => _type.Module;
    public string Name => _type.Name;
    public string Namespace => _type.Namespace;
    public Type ReflectedType => _type.ReflectedType;
    public StructLayoutAttribute StructLayoutAttribute => _type.StructLayoutAttribute;
    public RuntimeTypeHandle TypeHandle => _type.TypeHandle;
    public ConstructorInfo TypeInitializer => _type.TypeInitializer;
    public Type UnderlyingSystemType => _type.UnderlyingSystemType;
    public bool IsConstructedGenericType => _type.IsConstructedGenericType();

    #endregion

    #region -- Methods --

    public Type AsType() => _type;

    //public IEnumerable<Attribute> GetCustomAttributes(Type attributeType, bool inherit)
    //{
    //  return _type.GetCustomAttributes(attributeType, inherit).Cast<Attribute>();
    //}

    //public bool IsAssignableFrom(TypeInfo typeInfo) => _type.IsAssignableFrom(typeInfo.AsType());
    public bool IsAssignableFrom(Type type) => _type.IsAssignableFrom(type);

    public bool IsAssignableFrom(TypeInfo typeInfo)
    {
      if (typeInfo == null)
      {
        return false;
      }
      if (this == typeInfo)
      {
        return true;
      }
      if (typeInfo.IsSubclassOf(_type))
      {
        return true;
      }
      if (IsInterface)
      {
        return typeInfo._type.ImplementInterface(_type);
      }
      if (this.IsGenericParameter)
      {
        Type[] genericParameterConstraints = this.GetGenericParameterConstraints();
        for (int i = 0; i < genericParameterConstraints.Length; i++)
        {
          if (!genericParameterConstraints[i].IsAssignableFrom(typeInfo._type))
          {
            return false;
          }
        }
        return true;
      }
      return false;
    }

    public bool IsSubclassOf(TypeInfo typeInfo) => _type.IsSubclassOf(typeInfo.AsType());
    public bool IsSubclassOf(Type type) => _type.IsSubclassOf(type);

    public int GetArrayRank() => _type.GetArrayRank();

    public EventInfo GetDeclaredEvent(String name) => _type.GetEvent(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
    public FieldInfo GetDeclaredField(String name) => _type.GetField(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
    public MethodInfo GetDeclaredMethod(String name) => _type.GetMethod(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
    public IEnumerable<MethodInfo> GetDeclaredMethods(String name)
    {
      foreach (MethodInfo method in _type.GetMethods(BindingFlagsHelper.MSDeclaredOnlyLookup))
      {
        if (string.Equals(method.Name, name, StringComparison.Ordinal)) { yield return method; }
      }
    }
    public System.Reflection.TypeInfo GetDeclaredNestedType(String name)
    {
      var nt = _type.GetNestedType(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
      if (nt == null)
      {
        return default(TypeInfo); //the extension method GetTypeInfo throws for null
      }
      else
      {
        return nt.GetTypeInfo();
      }
    }
    public PropertyInfo GetDeclaredProperty(String name) => _type.GetProperty(name, BindingFlagsHelper.MSDeclaredOnlyLookup);
    public Type[] GetGenericParameterConstraints() => _type.GetGenericParameterConstraints();

    public Type GetElementType() => _type.GetElementType();
    public Type GetGenericTypeDefinition() => _type.GetGenericTypeDefinition();

    public object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        => _type.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);

    public ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => _type.GetConstructors(bindingAttr);
    public ConstructorInfo[] GetConstructors() => _type.GetConstructors();

    public MethodInfo GetMethod(string name) => _type.GetMethod(name);
    public MethodInfo[] GetMethods(BindingFlags bindingAttr) => _type.GetMethods(bindingAttr);

    public FieldInfo GetField(string name, BindingFlags bindingAttr) => _type.GetField(name, bindingAttr);

    public FieldInfo[] GetFields(BindingFlags bindingAttr) => _type.GetFields(bindingAttr);

    public Type GetInterface(string name, bool ignoreCase) => _type.GetInterface(name, ignoreCase);

    public Type[] GetInterfaces() => _type.GetInterfaces();

    public EventInfo GetEvent(string name, BindingFlags bindingAttr) => _type.GetEvent(name, bindingAttr);

    public EventInfo[] GetEvents(BindingFlags bindingAttr) => _type.GetEvents(bindingAttr);

    public PropertyInfo GetProperty(string name) => _type.GetProperty(name);
    public PropertyInfo[] GetProperties(BindingFlags bindingAttr) => _type.GetProperties(bindingAttr);

    public Type[] GetNestedTypes(BindingFlags bindingAttr) => _type.GetNestedTypes(bindingAttr);

    public Type GetNestedType(string name, BindingFlags bindingAttr) => _type.GetNestedType(name, bindingAttr);

    public MemberInfo[] GetMembers(BindingFlags bindingAttr) => _type.GetMembers(bindingAttr);


    public object[] GetCustomAttributes(bool inherit) => _type.GetCustomAttributes(inherit);

    public object[] GetCustomAttributes(Type attributeType, bool inherit) => _type.GetCustomAttributes(attributeType, inherit);

    public bool IsDefined(Type attributeType, bool inherit) => _type.IsDefined(attributeType, inherit);

    #endregion

#pragma warning restore 1591 // "Missing XML-comment"
  }
}
#endif