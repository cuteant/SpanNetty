#if NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace System.Runtime.ExceptionServices
{
	// TOOD: remove unused members
	internal static class ReflectionHelper
	{
		internal static Type Type(String typeName)
		{
			try
			{
				return System.Type.GetType(typeName, false);
			}
			catch (ArgumentException)
			{
			}
			catch (TargetInvocationException)
			{
			}
			catch (TypeLoadException)
			{
			}
			catch (IOException)
			{
			}
			catch (BadImageFormatException)
			{
			}
			return null;
		}

		internal static MemberExpression Property(Type type, String propertyName)
		{
			if (type == null)
				return null;
			try
			{
				var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
				if (prop == null)
					return null;
				return Expression.Property(null, prop);
			}
			catch (ArgumentException)
			{
			}
			catch (AmbiguousMatchException)
			{
			}
			return null;
		}

		internal static MemberExpression Property(Expression instance, String propertyName)
		{
			if (instance == null)
				return null;
			try
			{
				return Expression.Property(instance, propertyName);
			}
			catch (ArgumentException)
			{
			}
			return null;
		}

		internal static T Compile<T>(Expression body, params ParameterExpression[] parameters) where T : class
		{
			if (body == null || parameters.Any(x => x == null))
				return null;
			try
			{
				return Expression.Lambda<T>(body, parameters).Compile();
			}
			catch (ArgumentException)
			{
			}
			return null;
		}

		internal static MethodCallExpression Call(Type type, String methodName, params Expression[] arguments)
		{
			if (type == null || arguments.Any(x => x == null))
				return null;
			try
			{
				return Expression.Call(type, methodName, null, arguments);
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}

		internal static MethodCallExpression Call(Expression instance, String methodName, params Expression[] arguments)
		{
			if (instance == null || arguments.Any(x => x == null))
				return null;
			try
			{
				return Expression.Call(instance, methodName, null, arguments);
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}

		internal static MethodCallExpression Call(Expression instance, String methodName, BindingFlags flags, params Expression[] arguments)
		{
			if (instance == null || arguments.Any(x => x == null))
				return null;
			MethodInfo method;
			try
			{
				method = instance.Type.GetMethod(methodName, flags);
			}
			catch (AmbiguousMatchException)
			{
				return null;
			}
			try
			{
				return Expression.Call(instance, method, arguments);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}

		internal static InvocationExpression Invoke(Expression instance, params Expression[] arguments)
		{
			if (instance == null || arguments.Any(x => x == null))
				return null;
			try
			{
				return Expression.Invoke(instance, arguments);
			}
			catch (ArgumentException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}

		internal static LambdaExpression Lambda(Type delegateType, Expression body, params ParameterExpression[] parameters)
		{
			if (delegateType == null || body == null || parameters.Any(x => x == null))
				return null;
			try
			{
				return Expression.Lambda(delegateType, body, parameters);
			}
			catch (ArgumentException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}

		internal static ConstantExpression Constant(Type type, object value)
		{
			if (type == null)
				return null;
			try
			{
				return Expression.Constant(value, type);
			}
			catch (ArgumentException)
			{
			}
			return null;
		}

		internal static UnaryExpression Convert(Expression instance, Type type)
		{
			if (instance == null || type == null)
				return null;
			try
			{
				return Expression.Convert(instance, type);
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}

		internal static T? EnumValue<T>(String name) where T : struct
		{
			try
			{
				return (T)Enum.Parse(typeof(T), name, true);
			}
			catch (ArgumentException)
			{
			}
			catch (OverflowException)
			{
			}
			return null;
		}

		internal static UnaryExpression EnumValue(Type type, String name)
		{
			try
			{
				var value = Enum.Parse(type, name, true);
				return Convert(Constant(type, value), type);
			}
			catch (ArgumentException)
			{
			}
			catch (OverflowException)
			{
			}
			return null;
		}

		internal static BinaryExpression Equal(Expression a, Expression b)
		{
			try
			{
				return Expression.Equal(a, b);
			}
			catch (InvalidOperationException)
			{
			}
			return null;
		}
	}
}
#endif