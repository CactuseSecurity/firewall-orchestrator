using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Test
{
    internal class TestHelper
    {
        public static ReturnType InvokeMethod<ClassType, ReturnType>(string name, object?[]? parameters = null, ClassType? instance = null)
            where ClassType : class
        {
            MethodInfo? method = typeof(ClassType)
                .GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new Exception($"Method \"{name}\" could not be found in \"{typeof(ClassType)}\".");
            object? rawResult = method.Invoke(instance, parameters);

            if (rawResult == null) 
            {
                if (default(ReturnType) != null) // Not nullable
                {
                    throw new Exception($"Result of method \"{name}\" is null but return type  is not nullable.");
                }
                return default!;
            }
            else if (!rawResult.GetType().IsSubclassOf(typeof(ReturnType)))
            {
                throw new Exception($"Result of method is of type {rawResult.GetType} but was expected to be \"{typeof(ReturnType)}\".");
            }
            else
            {
                return (ReturnType)rawResult;
            }
        }
    }
}
